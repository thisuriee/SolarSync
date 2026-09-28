/*
 * File:    NodeService.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Business rules for microgrid nodes (docs/api-contract.md §3):
 *          coordinate and capacity validation, the GeoJSON [longitude,
 *          latitude] mapping, the rule that a prosumer only ever sees active
 *          nodes, and the guard that stops a node's battery count being
 *          reduced below a booking window that already depends on it.
 *
 *          Throws BusinessRuleException; ExceptionHandlingMiddleware turns it
 *          into the problem object in docs/response-format.md. Nothing in
 *          this file returns an HTTP result, and nothing in the controller
 *          decides whether a rule passed.
 */
using MongoDB.Driver.GeoJsonObjectModel;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Repositories;

namespace SmartSolar.Api.Services;

public class NodeService : INodeService
{
    // How far a proximity search reaches when the caller does not say.
    // Twenty-five kilometres covers Colombo and the towns around it, which is
    // a useful first screenful without pulling in half the country.
    private const double DefaultSearchRadiusKm = 25.0;

    // The widest search that will be honoured. Not a technical limit — it
    // stops a client asking for every node in the country and calling it a
    // map, and it keeps an accidental radius of 100000 from being answered
    // seriously.
    private const double MaxSearchRadiusKm = 500.0;

    // The most markers one search returns. A map with hundreds of pins is
    // unreadable, and because the results arrive nearest-first this keeps the
    // closest ones rather than an arbitrary slice.
    private const int NearbySearchLimit = 50;

    private readonly IStationRepository _stations;
    private readonly ISlotRepository _slots;
    private readonly IReservationQueries _reservationQueries;

    public NodeService(
        IStationRepository stations,
        ISlotRepository slots,
        IReservationQueries reservationQueries)
    {
        _stations = stations;
        _slots = slots;
        _reservationQueries = reservationQueries;
    }

    // Rule: a prosumer only ever sees Active nodes, and the filter is applied
    // here rather than in either client. A status they ask for is discarded,
    // not honoured — filtering in the app would be bypassable by calling the
    // endpoint directly, which is the definition of a rule in the wrong place.
    public async Task<List<NodeResponse>> FindAll(string? status, string? search, string callerRole)
    {
        var effectiveStatus = ResolveStatusFilter(status, callerRole);

        var stations = await _stations.FindAll(effectiveStatus, search);

        return stations.Select(NodeResponse.FromStation).ToList();
    }

    // Returns a node with the counts the node screens need. The active
    // reservation count comes from the one shared predicate, so the number
    // shown here is the same number the deactivation rule will test.
    public async Task<NodeDetailResponse> FindById(string id, string callerRole)
    {
        var station = await LoadOrThrow(id);

        // The prosumer visibility rule applies to a direct read too, or the
        // list filter would be trivially sidestepped by requesting the id.
        if (IsProsumer(callerRole) && station.Status != StationStatuses.Active)
        {
            throw NotFound(id);
        }

        var slotCount = await _slots.CountByStation(id);
        var openSlotCount = await _slots.CountByStationAndStatus(id, SlotStatuses.Open);
        var activeReservations = await _reservationQueries.CountActiveForStation(id);

        return NodeDetailResponse.FromStation(station, slotCount, openSlotCount, activeReservations);
    }

    // Registers a node. Status, createdBy and the timestamps are set here and
    // are absent from the request DTO, so a client has no field to set them
    // with. A new node starts Active: it has been registered, and nothing can
    // yet be booked against it.
    public async Task<NodeResponse> Create(NodeCreateRequest request, string createdByUserId)
    {
        ValidateCoordinates(request.Lat, request.Lng);
        ValidateCapacity(request.CapacityKWh, request.TotalBatterySlots);
        ValidateSchedule(request.OperatingSchedule);

        var now = DateTime.UtcNow;

        var station = new SolarStation
        {
            StationName = request.StationName.Trim(),
            AddressLine = request.AddressLine.Trim(),
            City = request.City.Trim(),
            Location = ToGeoJsonPoint(request.Lat, request.Lng),
            CapacityKWh = request.CapacityKWh,
            TotalBatterySlots = request.TotalBatterySlots,
            Status = StationStatuses.Active,
            OperatingSchedule = request.OperatingSchedule.Select(s => s.ToEntry()).ToList(),
            CreatedBy = createdByUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _stations.Insert(station);

        return NodeResponse.FromStation(station);
    }

    // Updates the editable fields of a node.
    //
    // Rule NODE_CAPACITY_CONFLICT: totalBatterySlots cannot be reduced below
    // the largest capacity already promised by one of this node's booking
    // windows. Physically, the windows are promising bays that would no
    // longer exist.
    //
    // Status, createdBy and createdAt are copied from the stored document.
    // Status in particular has its own transition rules and its own routes;
    // letting a full update carry it would be a second way to change it, and
    // one that skips the active-reservation check.
    public async Task<NodeResponse> Update(string id, NodeUpdateRequest request)
    {
        var existing = await LoadOrThrow(id);

        ValidateCoordinates(request.Lat, request.Lng);
        ValidateCapacity(request.CapacityKWh, request.TotalBatterySlots);
        ValidateSchedule(request.OperatingSchedule);

        var largestSlotCapacity = await _slots.MaxTotalCapacityForStation(id);

        if (request.TotalBatterySlots < largestSlotCapacity)
        {
            var requested = Pluralise(request.TotalBatterySlots, "battery slot");

            throw new BusinessRuleException(
                "NODE_CAPACITY_CONFLICT",
                "Battery slot count conflict",
                $"This node cannot be reduced to {requested}: an existing booking slot "
                    + $"already allocates {largestSlotCapacity}.",
                StatusCodes.Status409Conflict);
        }

        var updated = new SolarStation
        {
            Id = existing.Id,
            StationName = request.StationName.Trim(),
            AddressLine = request.AddressLine.Trim(),
            City = request.City.Trim(),
            Location = ToGeoJsonPoint(request.Lat, request.Lng),
            CapacityKWh = request.CapacityKWh,
            TotalBatterySlots = request.TotalBatterySlots,
            OperatingSchedule = request.OperatingSchedule.Select(s => s.ToEntry()).ToList(),

            // Preserved from the stored document, never taken from the body.
            Status = existing.Status,
            CreatedBy = existing.CreatedBy,
            CreatedAt = existing.CreatedAt,

            UpdatedAt = DateTime.UtcNow
        };

        await _stations.Replace(id, updated);

        return NodeResponse.FromStation(updated);
    }

    // Nodes in service near a point, nearest first.
    //
    // THE RULES:
    //   the point is a real place on Earth — latitude within +/-90, longitude
    //     within +/-180
    //   the radius is positive and not absurd
    //   only nodes in service are returned, whoever is asking
    //
    // The last of those is not a role filter like the one on the node listing.
    // This search answers "where could I charge", and a hub that is shut is
    // not an answer to that question for anyone — so staff see the same set a
    // prosumer does. Staff who want the full picture have the node listing,
    // which does honour their role.
    //
    // Ordering and distance both come from the database. Nothing here sorts
    // the results or measures anything: the geospatial index does both, and a
    // client that recomputed the distance would be doing work the server is
    // responsible for, differently in each app.
    public async Task<List<NearbyNodeResponse>> FindNearby(double lat, double lng, double? radiusKm)
    {
        ValidateCoordinates(lat, lng);

        var radius = ResolveRadius(radiusKm);

        var found = await _stations.FindNearby(
            lat, lng, radius, StationStatuses.Active, NearbySearchLimit);

        return found
            .Select(s => NearbyNodeResponse.FromStation(s, s.DistanceKm))
            .ToList();
    }

    // Takes a node out of service.
    //
    // THE RULE: deactivation is refused while active reservations still point
    // at this node. A reservation is active when it is Pending or Approved and
    // its slot has not started yet — the shared definition, read through
    // IReservationQueries rather than re-implemented here, so this check and
    // the one guarding slot deletion can never disagree about what "active"
    // means.
    //
    // The refusal carries the count in its message. The clients display that
    // sentence verbatim, and a number no client could have worked out for
    // itself is what makes it evident the decision was taken on the server.
    //
    // Deactivating an already-inactive node succeeds and changes nothing. The
    // caller asked for a state that already holds, and treating a repeated
    // request as an error would turn two officers clicking at once into a
    // spurious failure.
    //
    // Nothing is written to the node's booking windows. Their availability
    // follows from the node's status wherever they are listed or booked, so
    // stamping "closed" onto each one would duplicate a fact that can then
    // drift — and slot status is not this service's to write.
    public async Task<NodeResponse> Deactivate(string id)
    {
        var station = await LoadOrThrow(id);

        if (station.Status == StationStatuses.Inactive)
        {
            return NodeResponse.FromStation(station);
        }

        var activeReservations = await _reservationQueries.CountActiveForStation(id);

        if (activeReservations > 0)
        {
            // Written out in both forms rather than assembled from fragments:
            // the clients print this sentence as-is, so subject and verb have
            // to agree in each case.
            var detail = activeReservations == 1
                ? "This node cannot be deactivated while 1 active reservation still "
                    + "references it. Cancel or complete it first."
                : $"This node cannot be deactivated while {activeReservations} active "
                    + "reservations still reference it. Cancel or complete them first.";

            throw new BusinessRuleException(
                "NODE_HAS_ACTIVE_RESERVATIONS",
                "Node has active reservations",
                detail,
                StatusCodes.Status409Conflict);
        }

        return await ChangeStatus(station, StationStatuses.Inactive);
    }

    // Returns a node to service.
    //
    // There is deliberately no counterpart to the deactivation guard.
    // Deactivating can strand a booking somebody is relying on tomorrow;
    // activating cannot invalidate anything, because nothing can have been
    // booked against a node while it was out of service. Reactivating also
    // restores its existing booking windows as they were, which is the
    // consequence of this service never having written to them.
    public async Task<NodeResponse> Activate(string id)
    {
        var station = await LoadOrThrow(id);

        if (station.Status == StationStatuses.Active)
        {
            return NodeResponse.FromStation(station);
        }

        return await ChangeStatus(station, StationStatuses.Active);
    }

    // Applies a status transition through the targeted repository update, so
    // the write touches status and updatedAt only and cannot revert a field
    // somebody edited in between. The in-memory copy is moved to match, so
    // the response reflects what was stored without a second read.
    private async Task<NodeResponse> ChangeStatus(SolarStation station, string status)
    {
        var updatedAt = DateTime.UtcNow;

        await _stations.UpdateStatus(station.Id, status, updatedAt);

        station.Status = status;
        station.UpdatedAt = updatedAt;

        return NodeResponse.FromStation(station);
    }

    // Loads a station or throws the 404 the clients render. Kept in one place
    // so every route reports a missing node identically.
    private async Task<SolarStation> LoadOrThrow(string id)
    {
        return await _stations.FindById(id) ?? throw NotFound(id);
    }

    // Decides which status filter actually reaches the database.
    // A prosumer is pinned to Active whatever they asked for; staff get the
    // filter they requested, and an unrecognised value is rejected rather
    // than silently returning everything.
    private static string? ResolveStatusFilter(string? requested, string callerRole)
    {
        if (IsProsumer(callerRole))
        {
            return StationStatuses.Active;
        }

        if (string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

        var match = StationStatuses.All
            .FirstOrDefault(s => string.Equals(s, requested.Trim(), StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new BusinessRuleException(
                "NODE_INVALID_STATUS_FILTER",
                "Unknown status filter",
                $"status must be one of: {string.Join(", ", StationStatuses.All)}.",
                StatusCodes.Status400BadRequest);
        }

        return match;
    }

    // Rule: a search radius must be a positive, sensible distance. Absent
    // means the default rather than an error, so the simplest possible
    // request — a latitude and a longitude — works on its own.
    private static double ResolveRadius(double? radiusKm)
    {
        if (!radiusKm.HasValue)
        {
            return DefaultSearchRadiusKm;
        }

        var radius = radiusKm.Value;

        if (double.IsNaN(radius) || radius <= 0)
        {
            throw new BusinessRuleException(
                "NODE_INVALID_RADIUS",
                "Invalid search radius",
                "radiusKm must be greater than 0.",
                StatusCodes.Status400BadRequest);
        }

        if (radius > MaxSearchRadiusKm)
        {
            throw new BusinessRuleException(
                "NODE_INVALID_RADIUS",
                "Invalid search radius",
                $"radiusKm must not exceed {MaxSearchRadiusKm:0} km.",
                StatusCodes.Status400BadRequest);
        }

        return radius;
    }

    // True when the caller holds the Prosumer role.
    private static bool IsProsumer(string callerRole)
    {
        return string.Equals(callerRole, UserRoles.Prosumer, StringComparison.Ordinal);
    }

    // Rule: latitude within +/-90 and longitude within +/-180. Also checked by
    // DataAnnotations on the DTO, which produces the field-level 400. This is
    // the copy that survives the DTO being changed or the service being called
    // from somewhere else, and it is the one that is actually the rule.
    private static void ValidateCoordinates(double lat, double lng)
    {
        if (double.IsNaN(lat) || lat < -90 || lat > 90)
        {
            throw new BusinessRuleException(
                "NODE_INVALID_COORDINATES",
                "Invalid coordinates",
                "Latitude must be between -90 and 90 degrees.",
                StatusCodes.Status400BadRequest);
        }

        if (double.IsNaN(lng) || lng < -180 || lng > 180)
        {
            throw new BusinessRuleException(
                "NODE_INVALID_COORDINATES",
                "Invalid coordinates",
                "Longitude must be between -180 and 180 degrees.",
                StatusCodes.Status400BadRequest);
        }
    }

    // Rule: a node must hold some energy and have at least one battery bay.
    // The battery count is the ceiling every booking window's capacity is
    // later measured against.
    private static void ValidateCapacity(double capacityKWh, int totalBatterySlots)
    {
        if (double.IsNaN(capacityKWh) || capacityKWh <= 0)
        {
            throw new BusinessRuleException(
                "NODE_INVALID_CAPACITY",
                "Invalid capacity",
                "capacityKWh must be greater than 0.",
                StatusCodes.Status400BadRequest);
        }

        if (totalBatterySlots < 1)
        {
            throw new BusinessRuleException(
                "NODE_INVALID_CAPACITY",
                "Invalid capacity",
                "totalBatterySlots must be at least 1.",
                StatusCodes.Status400BadRequest);
        }
    }

    // Rule: a node opens on at least one day, each day appears once, and each
    // row closes after it opens. Times are compared as "HH:mm" strings, which
    // sort correctly because both parts are zero-padded and fixed width.
    private static void ValidateSchedule(List<OperatingScheduleDto> schedule)
    {
        if (schedule.Count == 0)
        {
            throw new BusinessRuleException(
                "NODE_INVALID_SCHEDULE",
                "Invalid operating schedule",
                "A node must have at least one operating schedule entry.",
                StatusCodes.Status400BadRequest);
        }

        var duplicateDay = schedule
            .GroupBy(entry => entry.DayOfWeek)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateDay is not null)
        {
            throw new BusinessRuleException(
                "NODE_INVALID_SCHEDULE",
                "Invalid operating schedule",
                $"Day {duplicateDay.Key} appears more than once in the operating schedule.",
                StatusCodes.Status400BadRequest);
        }

        foreach (var entry in schedule)
        {
            if (string.CompareOrdinal(entry.CloseTime, entry.OpenTime) <= 0)
            {
                throw new BusinessRuleException(
                    "NODE_INVALID_SCHEDULE",
                    "Invalid operating schedule",
                    $"Closing time must be after opening time (day {entry.DayOfWeek}: "
                        + $"{entry.OpenTime}-{entry.CloseTime}).",
                    StatusCodes.Status400BadRequest);
            }
        }
    }

    // Maps a latitude and longitude pair into the stored GeoJSON point.
    //
    // GeoJSON orders coordinates [longitude, latitude] — x before y — and the
    // 2dsphere index depends on it. Reversed, a Colombo node is written as a
    // point in the Indian Ocean, and the geospatial query still succeeds and
    // still returns plausible sorted distances, so nothing surfaces until the
    // map is empty. This single conversion is why the API takes two plain
    // numbers from its clients instead of a GeoJSON object.
    private static GeoJsonPoint<GeoJson2DGeographicCoordinates> ToGeoJsonPoint(double lat, double lng)
    {
        return new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
            new GeoJson2DGeographicCoordinates(lng, lat));
    }

    // Formats a count with its noun. The clients render these messages
    // verbatim, so "1 battery slots" would be visible to a user.
    private static string Pluralise(long count, string noun)
    {
        return count == 1 ? $"1 {noun}" : $"{count} {noun}s";
    }

    // The 404 every node route reports for an unknown or malformed id.
    private static BusinessRuleException NotFound(string id)
    {
        return new BusinessRuleException(
            "NODE_NOT_FOUND",
            "Node not found",
            $"No microgrid node exists with id '{id}'.",
            StatusCodes.Status404NotFound);
    }
}
