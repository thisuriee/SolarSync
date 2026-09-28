/*
 * File:    IReservationRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Data access contract for the EnergyReservation collection. The
 *          lookup, QR field write and completion were added for the QR
 *          fulfilment vertical (Imadh); insert, the compare-and-set status
 *          writes and the queries for the reservation workflow (Thisuri,
 *          2026-09-27).
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public interface IReservationRepository
{
    // Returns the reservation with the given id, or null when it does not exist.
    Task<Reservation?> FindReservationById(string id);

    // Returns the reservation carrying the given QR token hash, or null.
    Task<Reservation?> FindByQrTokenHash(string qrTokenHash);

    // Writes the issued QR token hash and its validity window onto a reservation.
    Task UpdateQrFields(string id, string qrTokenHash, DateTime qrIssuedAt, DateTime qrExpiresAt);

    // Marks the reservation completed, recording the operator and the timestamp.
    Task MarkCompleted(string id, string operatorId, DateTime completedAt);

    // ---- Reservation workflow (Thisuri) ----

    // Inserts a new reservation. The driver fills reservation.Id on success.
    Task Insert(Reservation reservation);

    // Counts reservations in one of the given statuses whose slotStart is
    // after slotStartAfter, narrowed by whichever of stationId / slotId / nic
    // is supplied. The shared active-reservation predicate is built on this.
    Task<long> CountInStatusesStartingAfter(
        IReadOnlyCollection<string> statuses, DateTime slotStartAfter,
        string? stationId = null, string? slotId = null, string? nic = null);

    // Counts every reservation referencing a slot, whatever its status or time.
    Task<long> CountBySlot(string slotId);

    // One prosumer's reservations, optionally in one status, latest slot first.
    Task<List<Reservation>> FindByNic(string nic, string? status);

    // One page of reservations, filtered in the query (never in memory).
    // from/to bound slotStart inclusively. Latest slot first.
    Task<(List<Reservation> Items, long TotalCount)> FindPaged(
        string? status, string? stationId, string? nic, DateTime? from, DateTime? to, int skip, int limit);

    // Compare-and-set writes: each one only matches while the reservation is
    // still in expectedStatus, and returns the updated document, or null when
    // someone else changed it first. This is what stops a double cancel from
    // releasing capacity twice.
    Task<Reservation?> MarkApproved(string id, string expectedStatus, string approvedBy, DateTime utcNow);
    Task<Reservation?> MarkRejected(string id, string expectedStatus, string reason, DateTime utcNow);
    Task<Reservation?> MarkCancelled(string id, string expectedStatus, DateTime utcNow);

    // Moves a booking to a (possibly different) slot and/or amount. Also
    // matches on expectedSlotId so a concurrent slot change is detected.
    // clearQr wipes the QR fields so a stale code cannot be used for the new slot.
    Task<Reservation?> UpdateBooking(
        string id, string expectedStatus, string expectedSlotId,
        BookingSlot newSlot, double energyKWh, bool clearQr, DateTime utcNow);
}
