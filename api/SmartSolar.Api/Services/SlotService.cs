/*
 * File:    SlotService.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Business rules for booking windows (docs/api-contract.md §3):
 *          the window must be a real span of time, two windows on the same
 *          node may not overlap, capacity may not exceed the node's battery
 *          bays nor fall below what is already booked, and a window with
 *          reservations against it cannot be deleted.
 *
 *          Throws BusinessRuleException; ExceptionHandlingMiddleware turns it
 *          into the problem object in docs/response-format.md.
 *
 *          What this service never does is write reservedCount or status.
 *          Those two move together, in one operation, owned by the
 *          reservation side. Every write here carries the stored values
 *          forward untouched, and neither field appears on a request type.
 */
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Repositories;

namespace SmartSolar.Api.Services;

public class SlotService : ISlotService
{
    // How far ahead a window may be booked. The same horizon the reservation
    // side enforces when a booking is actually placed — surfaced here so a
    // prosumer is not shown windows they would then be refused.
    private const int BookingHorizonDays = 7;

    private readonly ISlotRepository _slots;
    private readonly IStationRepository _stations;
    private readonly IReservationQueries _reservationQueries;

    public SlotService(
        ISlotRepository slots,
        IStationRepository stations,
        IReservationQueries reservationQueries)
    {
        _slots = slots;
        _stations = stations;
        _reservationQueries = reservationQueries;
    }

    // Lists one node's windows. The node itself is resolved first so that
    // asking for the windows of a node that does not exist reports the
    // missing node rather than an empty list, which would be indistinguishable
    // from a node that simply has none.
    public async Task<List<SlotResponse>> FindByNode(string nodeId, DateTime? from, DateTime? to)
    {
        await LoadStationOrThrow(nodeId);

        var slots = await _slots.FindByStation(nodeId, from, to);

        return slots.Select(SlotResponse.FromSlot).ToList();
    }

    // Opens a bookable window on a node.
    //
    // THE RULES, in the order they are checked:
    //   the node exists
    //   the window is a real span of time — it ends after it begins
    //   capacity fits the node's physical battery bays
    //   no existing window on this node covers any of the same time
    //
    // reservedCount starts at 0 and status at Open, both set here. Neither is
    // on the request type, so neither can arrive from a client.
    //
    // A window may be opened on a node that is currently out of service.
    // Nothing can be booked against it while the node stays that way — the
    // availability query filters those out — so preparing windows ahead of a
    // node returning to service is harmless, and refusing it would block a
    // reasonable thing to want to do.
    public async Task<SlotResponse> Create(string nodeId, SlotCreateRequest request)
    {
        var station = await LoadStationOrThrow(nodeId);

        ValidateWindow(request.SlotStart, request.SlotEnd);
        ValidateEnergy(request.EnergyPerSlotKWh);
        EnsureCapacityFitsStation(station, request.TotalCapacity);
        await EnsureNoOverlap(nodeId, request.SlotStart, request.SlotEnd, excludeSlotId: null);

        var now = DateTime.UtcNow;

        var slot = new BookingSlot
        {
            StationId = nodeId,
            SlotStart = request.SlotStart,
            SlotEnd = request.SlotEnd,
            TotalCapacity = request.TotalCapacity,
            EnergyPerSlotKWh = request.EnergyPerSlotKWh,

            // Set here, never accepted from the caller.
            ReservedCount = 0,
            Status = SlotStatuses.Open,

            CreatedAt = now,
            UpdatedAt = now
        };

        await _slots.Insert(slot);

        return SlotResponse.FromSlot(slot);
    }

    // Updates a window's times, capacity and energy.
    //
    // Two capacity rules apply, in opposite directions:
    //   upward   — capacity may not exceed the node's battery bays, because
    //              the places would not physically exist
    //   downward — capacity may not fall below what is already booked, because
    //              those bookings would have nowhere to go
    //
    // The overlap check runs again, excluding this window, or every edit would
    // find the window colliding with itself.
    //
    // reservedCount and status are carried over from the stored document. The
    // replacement is built from what is in the database, not from what arrived
    // in the request, so there is no path by which a client could move them.
    public async Task<SlotResponse> Update(string id, SlotUpdateRequest request)
    {
        var existing = await LoadSlotOrThrow(id);
        var station = await LoadStationOrThrow(existing.StationId);

        ValidateWindow(request.SlotStart, request.SlotEnd);
        ValidateEnergy(request.EnergyPerSlotKWh);
        EnsureCapacityFitsStation(station, request.TotalCapacity);

        if (request.TotalCapacity < existing.ReservedCount)
        {
            throw new BusinessRuleException(
                "SLOT_CAPACITY_CONFLICT",
                "Capacity below existing bookings",
                $"This booking slot cannot be reduced to {request.TotalCapacity}: "
                    + $"{Pluralise(existing.ReservedCount, "booking")} already placed on it.",
                StatusCodes.Status409Conflict);
        }

        await EnsureNoOverlap(existing.StationId, request.SlotStart, request.SlotEnd, excludeSlotId: id);

        var updated = new BookingSlot
        {
            Id = existing.Id,
            StationId = existing.StationId,
            SlotStart = request.SlotStart,
            SlotEnd = request.SlotEnd,
            TotalCapacity = request.TotalCapacity,
            EnergyPerSlotKWh = request.EnergyPerSlotKWh,

            // Carried over from the stored document, never from the request.
            ReservedCount = existing.ReservedCount,
            Status = existing.Status,

            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };

        await _slots.Replace(id, updated);

        return SlotResponse.FromSlot(updated);
    }

    // Removes a booking window.
    //
    // THE RULE: refused while reservations reference it.
    //
    // The contract names the case that matters most — an ACTIVE reservation,
    // someone who is going to turn up — and that is the one whose message
    // tells the caller what to do about it.
    //
    // A window whose only reservations are historical is refused as well. Those
    // bookings still carry this window's id, and deleting it would leave them
    // pointing at nothing. Consistency of the references between the four
    // collections is a property the system is expected to hold, and a completed
    // transfer that can no longer say which window it happened in has lost
    // something real. Closing a window is the way to retire it; deleting one is
    // for windows that never mattered.
    public async Task Delete(string id)
    {
        var slot = await LoadSlotOrThrow(id);

        var activeReservations = await _reservationQueries.CountActiveForSlot(id);

        if (activeReservations > 0)
        {
            var blocking = activeReservations == 1
                ? "1 active reservation still references it. Cancel or complete it first."
                : $"{activeReservations} active reservations still reference it. "
                    + "Cancel or complete them first.";

            throw new BusinessRuleException(
                "SLOT_HAS_RESERVATIONS",
                "Slot has active reservations",
                $"This booking slot cannot be deleted while {blocking}",
                StatusCodes.Status409Conflict);
        }

        var anyReservations = await _reservationQueries.CountForSlot(id);

        if (anyReservations > 0)
        {
            // Both forms written out, because the clients print this verbatim
            // and subject and verb have to agree in each.
            var referring = anyReservations == 1
                ? "1 past reservation still refers to it, and deleting it would leave that "
                    + "booking pointing at a slot that no longer exists."
                : $"{anyReservations} past reservations still refer to it, and deleting it "
                    + "would leave those bookings pointing at a slot that no longer exists.";

            throw new BusinessRuleException(
                "SLOT_HAS_RESERVATIONS",
                "Slot has reservation history",
                $"This booking slot cannot be deleted: {referring} "
                    + "Close the slot instead of deleting it.",
                StatusCodes.Status409Conflict);
        }

        await _slots.Delete(id);
    }

    // Windows a prosumer could book right now.
    //
    // THE RULES:
    //   the node is Active — a window at a hub that is out of service is not
    //     bookable, which is what makes taking a node out of service mean
    //     anything to someone looking for somewhere to charge
    //   the window is still Open
    //   it has a free place — reservedCount below totalCapacity
    //   it starts in the future and within the booking horizon
    //
    // The horizon is shown here and enforced again when a booking is actually
    // placed. This copy is a courtesy so nobody is offered a window they would
    // then be refused; the copy on the booking path is the rule, because that
    // is the one a request cannot go around.
    public async Task<List<SlotResponse>> FindBookable(string? nodeId, DateTime? date)
    {
        var stationIds = await ResolveBookableStations(nodeId);

        if (stationIds.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var from = now;
        var to = now.AddDays(BookingHorizonDays);

        // A requested day narrows the range, but never widens it past the
        // horizon or back into the past.
        if (date.HasValue)
        {
            var dayStart = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);

            from = dayStart > from ? dayStart : from;
            to = dayEnd < to ? dayEnd : to;

            if (from >= to)
            {
                return [];
            }
        }

        var slots = await _slots.FindBookable(stationIds, from, to);

        return slots.Select(SlotResponse.FromSlot).ToList();
    }

    // Works out which stations bookable windows may come from: one named
    // node, or every active node. A named node that exists but is out of
    // service yields nothing rather than an error — it is a real node, it
    // simply has nothing on offer.
    private async Task<List<string>> ResolveBookableStations(string? nodeId)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            var station = await LoadStationOrThrow(nodeId);

            return station.Status == StationStatuses.Active ? [station.Id] : [];
        }

        var active = await _stations.FindAll(StationStatuses.Active, null);

        return active.Select(s => s.Id).ToList();
    }

    // Rule: a window must end after it begins. A zero-length or inverted
    // window could never be booked and would sort unpredictably against its
    // neighbours in the overlap check.
    private static void ValidateWindow(DateTime start, DateTime end)
    {
        if (end <= start)
        {
            throw new BusinessRuleException(
                "SLOT_INVALID_WINDOW",
                "Invalid booking window",
                "slotEnd must be after slotStart.",
                StatusCodes.Status400BadRequest);
        }
    }

    // Rule: a window must offer some energy, or there is nothing to book.
    private static void ValidateEnergy(double energyPerSlotKWh)
    {
        if (double.IsNaN(energyPerSlotKWh) || energyPerSlotKWh <= 0)
        {
            throw new BusinessRuleException(
                "SLOT_INVALID_CAPACITY",
                "Invalid slot capacity",
                "energyPerSlotKWh must be greater than 0.",
                StatusCodes.Status400BadRequest);
        }
    }

    // Rule: a window cannot offer more places than the node has battery bays.
    // Physically, the extra bookings would have nowhere to plug in. This is
    // the same invariant the node update guards from the other side, which is
    // why neither direction can be used to get around the other.
    private static void EnsureCapacityFitsStation(SolarStation station, int totalCapacity)
    {
        if (totalCapacity < 1)
        {
            throw new BusinessRuleException(
                "SLOT_INVALID_CAPACITY",
                "Invalid slot capacity",
                "totalCapacity must be at least 1.",
                StatusCodes.Status400BadRequest);
        }

        if (totalCapacity > station.TotalBatterySlots)
        {
            throw new BusinessRuleException(
                "NODE_CAPACITY_CONFLICT",
                "Capacity exceeds the node",
                $"This booking slot cannot offer {Pluralise(totalCapacity, "place")}: "
                    + $"the node has {Pluralise(station.TotalBatterySlots, "battery slot")}.",
                StatusCodes.Status409Conflict);
        }
    }

    // Rule: two windows on the same node may not cover any of the same time,
    // because they would be selling the same batteries twice over. Windows
    // that merely touch — one ending exactly as the next begins — are fine.
    private async Task EnsureNoOverlap(string stationId, DateTime start, DateTime end, string? excludeSlotId)
    {
        var clashes = await _slots.FindOverlapping(stationId, start, end, excludeSlotId);

        if (clashes.Count == 0)
        {
            return;
        }

        var first = clashes[0];

        throw new BusinessRuleException(
            "SLOT_OVERLAP",
            "Overlapping booking slot",
            $"This node already has a booking slot from {Format(first.SlotStart)} to "
                + $"{Format(first.SlotEnd)}, which overlaps the requested window.",
            StatusCodes.Status409Conflict);
    }

    // Loads a station or throws the 404 the node routes report, so a window
    // hanging off a node that does not exist fails the same way everywhere.
    private async Task<SolarStation> LoadStationOrThrow(string stationId)
    {
        return await _stations.FindById(stationId)
            ?? throw new BusinessRuleException(
                "NODE_NOT_FOUND",
                "Node not found",
                $"No microgrid node exists with id '{stationId}'.",
                StatusCodes.Status404NotFound);
    }

    // Loads a window or throws the 404 every slot route reports.
    private async Task<BookingSlot> LoadSlotOrThrow(string id)
    {
        return await _slots.FindById(id)
            ?? throw new BusinessRuleException(
                "SLOT_NOT_FOUND",
                "Booking slot not found",
                $"No booking slot exists with id '{id}'.",
                StatusCodes.Status404NotFound);
    }

    // Formats an instant for a message. UTC with a trailing Z, so the sentence
    // says plainly which zone it means rather than leaving the reader to guess.
    private static string Format(DateTime instant)
    {
        return instant.ToUniversalTime().ToString("yyyy-MM-dd HH:mm'Z'");
    }

    // Formats a count with its noun. The clients render these messages
    // verbatim, so "1 bookings" would be visible to a user.
    private static string Pluralise(long count, string noun)
    {
        return count == 1 ? $"1 {noun}" : $"{count} {noun}s";
    }
}
