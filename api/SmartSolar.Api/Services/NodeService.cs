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
    private static string Pluralise(int count, string noun)
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
