/*
 * File:    ReservationService.cs
 * Author:  Thisuri
 * Created: 2026-09-27
 * Purpose: Every business rule of the reservation workflow
 *          (docs/api-contract.md §4) and the three shared pieces from §6:
 *
 *          1. The active-reservation predicate — status in {Pending, Approved}
 *             AND slotStart > utcNow — written once (CountActive) and exposed
 *             through both IReservationQueries (M2: node deactivate, slot
 *             delete) and IProsumerReservationCounter (M1: prosumer deactivate).
 *          2. reservedCount mutation — only through ISlotCapacityRepository's
 *             atomic TryClaim / Release, called only from this class.
 *          3. The status transition guard — EnsureTransition, public static so
 *             M4's completion path can call the same table.
 *
 *          R1 (7-day window) and R2 (12-hour notice) each live in exactly one
 *          private method, and their numbers in exactly one constant.
 *
 *          Throws BusinessRuleException; ExceptionHandlingMiddleware formats
 *          it. Nothing here returns an HTTP result, and every time comparison
 *          uses DateTime.UtcNow (slot times are stored in UTC; Sri Lanka is
 *          UTC+5:30, so a local clock would be wrong by 5.5 hours).
 */
using MongoDB.Bson;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Repositories;

namespace SmartSolar.Api.Services;

public class ReservationService : IReservationService, IReservationQueries, IProsumerReservationCounter
{
    // R1: a slot may be booked at most this far ahead. Change it here only.
    public static readonly TimeSpan BookingWindow = TimeSpan.FromDays(7);

    // R2: updates and cancellations must be at least this long before slotStart.
    public static readonly TimeSpan NoticePeriod = TimeSpan.FromHours(12);

    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    // The transition table from docs/api-contract.md §4. A status that is not
    // a key (Rejected, Cancelled, Completed) is terminal.
    private static readonly Dictionary<string, string[]> LegalTransitions = new()
    {
        [ReservationStatuses.Pending] = [ReservationStatuses.Approved, ReservationStatuses.Rejected, ReservationStatuses.Cancelled],
        [ReservationStatuses.Approved] = [ReservationStatuses.Cancelled, ReservationStatuses.Completed]
    };

    private static readonly string[] AllStatuses =
    [
        ReservationStatuses.Pending, ReservationStatuses.Approved, ReservationStatuses.Rejected,
        ReservationStatuses.Cancelled, ReservationStatuses.Completed
    ];

    private readonly IReservationRepository _reservations;
    private readonly ISlotCapacityRepository _slots;
    private readonly IStationRepository _stations;
    private readonly IUserRepository _users;

    public ReservationService(
        IReservationRepository reservations,
        ISlotCapacityRepository slots,
        IStationRepository stations,
        IUserRepository users)
    {
        _reservations = reservations;
        _slots = slots;
        _stations = stations;
        _users = users;
    }

    // =========================================================================
    // Shared piece 3 — the status transition guard
    // =========================================================================

    // Throws 409 RESERVATION_INVALID_STATE unless from -> to is in the table.
    // The single place the lifecycle is defined; approve, reject, cancel here
    // and M4's complete all go through it.
    public static void EnsureTransition(string from, string to)
    {
        if (!LegalTransitions.TryGetValue(from, out var allowed) || !allowed.Contains(to))
        {
            throw ReservationErrors.InvalidState(from, to);
        }
    }

    // =========================================================================
    // Shared piece 1 — the active-reservation predicate
    // =========================================================================

    // Rule (§6): active reservations on a station block node deactivation.
    public Task<long> CountActiveForStation(string stationId)
    {
        return CountActive(stationId: stationId);
    }

    // Rule (§6): active reservations on a slot block slot deletion.
    public Task<long> CountActiveForSlot(string slotId)
    {
        return CountActive(slotId: slotId);
    }

    // Rule (§6): active reservations held by a prosumer block deactivation.
    // Normalised so "…v" and "…V" are the same prosumer.
    public Task<long> CountActiveForProsumer(string nic)
    {
        if (string.IsNullOrWhiteSpace(nic))
        {
            return Task.FromResult(0L);
        }

        return CountActive(nic: IdentityFormat.NormaliseNic(nic));
    }

    // THE predicate: status in {Pending, Approved} AND slotStart > utcNow.
    // Every count above, and the duplicate-booking check, composes onto this,
    // so they can never disagree about what "active" means.
    private Task<long> CountActive(string? stationId = null, string? slotId = null, string? nic = null)
    {
        return _reservations.CountInStatusesStartingAfter(
            ReservationStatuses.Active, DateTime.UtcNow, stationId, slotId, nic);
    }

    // =========================================================================
    // Create
    // =========================================================================

    // Rule order: who is booking -> are they Active -> slot exists -> R1 ->
    // node Active -> slot open -> energy -> no duplicate -> atomic claim ->
    // insert. Cheap checks first; the claim is last because it is the only
    // step that writes, and it is compensated if the insert fails.
    public async Task<ReservationResponse> Create(CreateReservationRequest request, CallerIdentity caller)
    {
        EnsureRole(caller, "Only prosumers and Backoffice staff can create reservations.",
            UserRoles.Prosumer, UserRoles.Backoffice);

        var nic = ResolveBookingNic(request.ProsumerNIC, caller);
        await EnsureProsumerActive(nic);

        var slot = await LoadSlot(request.SlotId);
        var now = DateTime.UtcNow;

        EnsureWithinBookingWindow(slot, now);
        await EnsureNodeActive(slot.StationId);
        EnsureSlotOpen(slot);
        EnsureValidEnergy(request.EnergyKWh, slot);
        await EnsureNoDuplicate(nic, slot.Id);

        // Shared piece 2: the authoritative capacity check. Null means another
        // request took the last space after our read — one 201, one 409.
        _ = await _slots.TryClaim(slot.Id, now) ?? throw ReservationErrors.SlotFull(closed: false);

        var reservation = new Reservation
        {
            ProsumerNIC = nic,
            StationId = slot.StationId,
            SlotId = slot.Id,
            SlotStart = slot.SlotStart,   // copied: R2 is evaluated against this
            SlotEnd = slot.SlotEnd,
            EnergyKWh = request.EnergyKWh,
            Status = ReservationStatuses.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            await _reservations.Insert(reservation);
        }
        catch
        {
            // Compensate: the space was claimed but no booking holds it.
            await _slots.Release(slot.Id, DateTime.UtcNow);
            throw;
        }

        return ReservationResponse.FromReservation(reservation);
    }

    // =========================================================================
    // Reads
    // =========================================================================

    // Rule: any role may read a reservation, but a prosumer only their own
    // (403, never 404, so the refusal is visible).
    public async Task<ReservationResponse> FindById(string id, CallerIdentity caller)
    {
        var reservation = await LoadReservation(id);
        EnsureOwnerOrRole(caller, reservation, UserRoles.Backoffice, UserRoles.GridOperator);

        return ReservationResponse.FromReservation(reservation);
    }

    // Rule: /mine has no nic parameter — the NIC is the token's, full stop.
    public async Task<List<ReservationResponse>> FindMine(string? status, CallerIdentity caller)
    {
        EnsureRole(caller, "Only prosumers have their own reservations.", UserRoles.Prosumer);

        var reservations = await _reservations.FindByNic(RequireCallerNic(caller), NormaliseStatusFilter(status));

        return reservations.Select(ReservationResponse.FromReservation).ToList();
    }

    // Rule: staff only; every filter goes into the Mongo query, never applied
    // in memory. Page size is clamped so one request cannot pull the collection.
    public async Task<PagedResult<ReservationResponse>> Search(
        string? status, string? nodeId, string? nic, DateTime? from, DateTime? to,
        int? page, int? pageSize, CallerIdentity caller)
    {
        EnsureRole(caller, "Only Backoffice and Grid Operator staff can search reservations.",
            UserRoles.Backoffice, UserRoles.GridOperator);

        var effectivePage = Math.Max(page ?? 1, 1);
        var effectiveSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);

        var (items, total) = await _reservations.FindPaged(
            NormaliseStatusFilter(status),
            string.IsNullOrWhiteSpace(nodeId) ? null : nodeId.Trim(),
            string.IsNullOrWhiteSpace(nic) ? null : IdentityFormat.NormaliseNic(nic),
            AsUtc(from),
            AsUtc(to),
            (effectivePage - 1) * effectiveSize,
            effectiveSize);

        return PagedResult<ReservationResponse>.Create(
            items.Select(ReservationResponse.FromReservation).ToList(), effectivePage, effectiveSize, total);
    }

    // =========================================================================
    // Update
    // =========================================================================

    // Rule: R2 on the CURRENT booking; if the slot changes, R1 + node + open +
    // duplicate + capacity on the NEW slot. Claim the new space BEFORE
    // releasing the old one, so a full target slot leaves the booking exactly
    // as it was instead of stranding the prosumer with nothing.
    public async Task<ReservationResponse> Update(string id, UpdateReservationRequest request, CallerIdentity caller)
    {
        EnsureRole(caller, "Only prosumers and Backoffice staff can update reservations.",
            UserRoles.Prosumer, UserRoles.Backoffice);

        var reservation = await LoadReservation(id);
        EnsureOwnerOrRole(caller, reservation, UserRoles.Backoffice);

        if (!ReservationStatuses.Active.Contains(reservation.Status))
        {
            throw ReservationErrors.NotEditable(reservation.Status);
        }

        var now = DateTime.UtcNow;
        EnsureNoticePeriod(reservation, now);

        var slot = await LoadSlot(request.SlotId);
        EnsureValidEnergy(request.EnergyKWh, slot);

        if (slot.Id == reservation.SlotId)
        {
            // Energy-only change: no capacity moves, and the copied slot times
            // are kept as they are (a later slot edit must not move them).
            var keepSlot = new BookingSlot
            {
                Id = reservation.SlotId,
                StationId = reservation.StationId,
                SlotStart = reservation.SlotStart,
                SlotEnd = reservation.SlotEnd
            };

            var changed = await _reservations.UpdateBooking(
                reservation.Id, reservation.Status, reservation.SlotId, keepSlot, request.EnergyKWh, clearQr: false, now)
                ?? throw ReservationErrors.ChangedConcurrently();

            return ReservationResponse.FromReservation(changed);
        }

        EnsureWithinBookingWindow(slot, now);
        await EnsureNodeActive(slot.StationId);
        EnsureSlotOpen(slot);
        await EnsureNoDuplicate(reservation.ProsumerNIC, slot.Id);

        _ = await _slots.TryClaim(slot.Id, now) ?? throw ReservationErrors.SlotFull(closed: false);

        // An Approved booking stays Approved (the guard has no Approved ->
        // Pending edge), but its QR was for the old slot, so it is cleared and
        // M4's GET /reservations/{id}/qr issues a fresh one.
        var clearQr = reservation.Status == ReservationStatuses.Approved;

        var updated = await _reservations.UpdateBooking(
            reservation.Id, reservation.Status, reservation.SlotId, slot, request.EnergyKWh, clearQr, now);

        if (updated is null)
        {
            // Someone cancelled/changed it meanwhile: give the new space back.
            await _slots.Release(slot.Id, DateTime.UtcNow);
            throw ReservationErrors.ChangedConcurrently();
        }

        await _slots.Release(reservation.SlotId, now);

        return ReservationResponse.FromReservation(updated);
    }

    // =========================================================================
    // Status transitions
    // =========================================================================

    // Rule: guard (Pending/Approved -> Cancelled), then R2, then a
    // compare-and-set on the status we read. Capacity is released only if our
    // write won, so a double cancel cannot hand the space back twice.
    public async Task<ReservationResponse> Cancel(string id, CallerIdentity caller)
    {
        var reservation = await LoadReservation(id);
        EnsureOwnerOrRole(caller, reservation, UserRoles.GridOperator, UserRoles.Backoffice);

        EnsureTransition(reservation.Status, ReservationStatuses.Cancelled);

        var now = DateTime.UtcNow;
        EnsureNoticePeriod(reservation, now);

        var cancelled = await _reservations.MarkCancelled(reservation.Id, reservation.Status, now)
            ?? throw ReservationErrors.ChangedConcurrently();

        await _slots.Release(reservation.SlotId, now);

        return ReservationResponse.FromReservation(cancelled);
    }

    // Rule: GridOperator only (re-checked here, not just by the attribute),
    // Pending -> Approved only. approvedBy is the operator's token sub. The QR
    // is issued lazily by M4's GET /reservations/{id}/qr.
    public async Task<ReservationResponse> Approve(string id, CallerIdentity caller)
    {
        EnsureRole(caller, "Only Grid Operators can approve reservations.", UserRoles.GridOperator);

        var reservation = await LoadReservation(id);
        EnsureTransition(reservation.Status, ReservationStatuses.Approved);

        var approved = await _reservations.MarkApproved(reservation.Id, reservation.Status, caller.UserId, DateTime.UtcNow)
            ?? throw ReservationErrors.ChangedConcurrently();

        return ReservationResponse.FromReservation(approved);
    }

    // Rule: GridOperator only, Pending -> Rejected only, reason recorded, and
    // the space goes back to the slot (only if our compare-and-set won).
    public async Task<ReservationResponse> Reject(string id, string reason, CallerIdentity caller)
    {
        EnsureRole(caller, "Only Grid Operators can reject reservations.", UserRoles.GridOperator);

        var reservation = await LoadReservation(id);
        EnsureTransition(reservation.Status, ReservationStatuses.Rejected);

        var now = DateTime.UtcNow;
        var rejected = await _reservations.MarkRejected(reservation.Id, reservation.Status, reason.Trim(), now)
            ?? throw ReservationErrors.ChangedConcurrently();

        await _slots.Release(reservation.SlotId, now);

        return ReservationResponse.FromReservation(rejected);
    }

    // =========================================================================
    // Rules R1 and R2 — one method each
    // =========================================================================

    // R1: slotStart <= utcNow + 7 days (inclusive at exactly 7 days), and the
    // slot must not have started. Used by create and by update-to-new-slot.
    private static void EnsureWithinBookingWindow(BookingSlot slot, DateTime utcNow)
    {
        if (slot.SlotStart <= utcNow)
        {
            throw ReservationErrors.SlotAlreadyStarted();
        }

        if (slot.SlotStart > utcNow.Add(BookingWindow))
        {
            throw ReservationErrors.OutsideWindow(slot.SlotStart, BookingWindow);
        }
    }

    // R2: reservation.SlotStart - utcNow >= 12 hours, using the booking's
    // COPIED slotStart so a later slot edit cannot move the deadline. Used by
    // both update and cancel.
    private static void EnsureNoticePeriod(Reservation reservation, DateTime utcNow)
    {
        var untilStart = reservation.SlotStart - utcNow;

        if (untilStart < NoticePeriod)
        {
            throw ReservationErrors.NoticePeriod(untilStart, NoticePeriod);
        }
    }

    // =========================================================================
    // Other checks
    // =========================================================================

    // Rule: identity never travels in the payload. A prosumer books as the
    // token's NIC and any body NIC is ignored; only Backoffice may name one.
    private static string ResolveBookingNic(string? bodyNic, CallerIdentity caller)
    {
        if (caller.Role == UserRoles.Prosumer)
        {
            return RequireCallerNic(caller);
        }

        if (string.IsNullOrWhiteSpace(bodyNic))
        {
            throw ReservationErrors.ProsumerNicInvalid();
        }

        var nic = IdentityFormat.NormaliseNic(bodyNic);

        if (!IdentityFormat.IsValidNic(nic))
        {
            throw ReservationErrors.ProsumerNicInvalid();
        }

        return nic;
    }

    // Rule: only an Active prosumer may hold a booking.
    private async Task EnsureProsumerActive(string nic)
    {
        var prosumer = await _users.FindByNic(nic);

        if (prosumer is null || prosumer.Role != UserRoles.Prosumer)
        {
            throw UserErrors.NotFound($"prosumer with NIC {nic}");
        }

        if (prosumer.Status != UserStatuses.Active)
        {
            throw ReservationErrors.ProsumerInactive(prosumer.Status);
        }
    }

    // Rule: only an Active node may be booked.
    private async Task EnsureNodeActive(string stationId)
    {
        var station = await _stations.FindById(stationId);

        if (station is null || station.Status != StationStatuses.Active)
        {
            throw ReservationErrors.NodeInactive();
        }
    }

    // Early, friendly version of the capacity rule so a Closed slot gets its
    // own message. Not the real guard — TryClaim is, because this read can be
    // stale by the time we write.
    private static void EnsureSlotOpen(BookingSlot slot)
    {
        if (slot.Status == SlotStatuses.Closed)
        {
            throw ReservationErrors.SlotFull(closed: true);
        }

        if (slot.Status != SlotStatuses.Open || slot.ReservedCount >= slot.TotalCapacity)
        {
            throw ReservationErrors.SlotFull(closed: false);
        }
    }

    // Rule (proposed, to confirm with the group): 0 < energyKWh <= the
    // slot's energyPerSlotKWh.
    private static void EnsureValidEnergy(double energyKWh, BookingSlot slot)
    {
        if (double.IsNaN(energyKWh) || energyKWh <= 0 || energyKWh > slot.EnergyPerSlotKWh)
        {
            throw ReservationErrors.InvalidEnergy(slot.EnergyPerSlotKWh);
        }
    }

    // Rule: one active booking per prosumer per slot — the shared predicate
    // narrowed to this NIC and slot.
    private async Task EnsureNoDuplicate(string nic, string slotId)
    {
        if (await CountActive(slotId: slotId, nic: nic) > 0)
        {
            throw ReservationErrors.Duplicate();
        }
    }

    // Rule: a prosumer may only touch their own booking; staff roles listed
    // may touch any. Anything else is 403 AUTH_FORBIDDEN_ROLE — never a 404.
    private static void EnsureOwnerOrRole(CallerIdentity caller, Reservation reservation, params string[] staffRoles)
    {
        if (caller.Role == UserRoles.Prosumer)
        {
            if (RequireCallerNic(caller) != reservation.ProsumerNIC)
            {
                throw UserErrors.ForbiddenRole("You can only access your own reservations.");
            }

            return;
        }

        if (!staffRoles.Contains(caller.Role))
        {
            throw UserErrors.ForbiddenRole("Your role is not permitted to perform this action on a reservation.");
        }
    }

    // Service-layer role re-check, so the rule holds even if an [Authorize]
    // attribute is ever dropped from the controller.
    private static void EnsureRole(CallerIdentity caller, string detail, params string[] roles)
    {
        if (!roles.Contains(caller.Role))
        {
            throw UserErrors.ForbiddenRole(detail);
        }
    }

    // A prosumer token always carries a nic claim; if it does not, the
    // session is broken and the client must log in again.
    private static string RequireCallerNic(CallerIdentity caller)
    {
        if (string.IsNullOrWhiteSpace(caller.Nic))
        {
            throw new BusinessRuleException(
                "AUTH_INVALID_TOKEN",
                "Invalid session",
                "Your session is invalid. Please log in again.",
                StatusCodes.Status401Unauthorized);
        }

        return caller.Nic;
    }

    // Loads a reservation or throws 404. A malformed id is a 404 too, rather
    // than a 500 from the ObjectId serialiser.
    private async Task<Reservation> LoadReservation(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            throw ReservationErrors.NotFound();
        }

        return await _reservations.FindReservationById(id) ?? throw ReservationErrors.NotFound();
    }

    // Loads a booking window or throws 404 SLOT_NOT_FOUND.
    private async Task<BookingSlot> LoadSlot(string slotId)
    {
        return await _slots.FindById(slotId) ?? throw ReservationErrors.SlotNotFound();
    }

    // Blank -> no filter; otherwise must be one of the five statuses (any
    // casing), returned in its stored casing. Unknown -> 400.
    private static string? NormaliseStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return AllStatuses.FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw ReservationErrors.InvalidStatusFilter(status);
    }

    // The contract sends UTC ("...Z"). A value with no offset is taken to be
    // UTC as well, rather than being shifted as if it were server-local.
    private static DateTime? AsUtc(DateTime? value)
    {
        if (value is null) return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
