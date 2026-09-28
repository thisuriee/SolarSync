/*
 * File:    ReservationRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Mongo implementation of IReservationRepository. Pure data access —
 *          no business rules, no validation, no policy. QR methods by Imadh;
 *          reservation workflow methods (below the marker) by Thisuri.
 */
using MongoDB.Bson;
using MongoDB.Driver;
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly IMongoCollection<Reservation> _reservations;

    public ReservationRepository(IMongoCollection<Reservation> reservations)
    {
        _reservations = reservations;
    }

    // Finds a reservation by its ObjectId and returns the first match, or null.
    public async Task<Reservation?> FindReservationById(string id)
    {
        var filter = Builders<Reservation>.Filter.Eq(r => r.Id, id);
        return await _reservations.Find(filter).FirstOrDefaultAsync();
    }

    // Filters on QrTokenHash — how a scanned token is resolved to its booking.
    public async Task<Reservation?> FindByQrTokenHash(string qrTokenHash)
    {
        var filter = Builders<Reservation>.Filter.Eq(r => r.QrTokenHash, qrTokenHash);
        return await _reservations.Find(filter).FirstOrDefaultAsync();
    }

    // Sets the three QR fields on the reservation. Nothing else is touched.
    public async Task UpdateQrFields(string id, string qrTokenHash, DateTime qrIssuedAt, DateTime qrExpiresAt)
    {
        var filter = Builders<Reservation>.Filter.Eq(r => r.Id, id);
        var update = Builders<Reservation>.Update
            .Set(r => r.QrTokenHash, qrTokenHash)
            .Set(r => r.QrIssuedAt, qrIssuedAt)
            .Set(r => r.QrExpiresAt, qrExpiresAt);

        await _reservations.UpdateOneAsync(filter, update);
    }

    // Sets Status, VerifiedByOperatorId and CompletedAt — the completion write.
    public async Task MarkCompleted(string id, string operatorId, DateTime completedAt)
    {
        var filter = Builders<Reservation>.Filter.Eq(r => r.Id, id);
        var update = Builders<Reservation>.Update
            .Set(r => r.Status, "Completed")
            .Set(r => r.VerifiedByOperatorId, operatorId)
            .Set(r => r.CompletedAt, completedAt);

        await _reservations.UpdateOneAsync(filter, update);
    }

    // ---- Reservation workflow (Thisuri) ----

    // Inserts the document; the driver writes the generated _id back onto it.
    public async Task Insert(Reservation reservation)
    {
        await _reservations.InsertOneAsync(reservation);
    }

    // Builds status in statuses AND slotStart > slotStartAfter, plus any of the
    // three references supplied. An id that is not a valid ObjectId cannot
    // match anything, so it counts zero instead of failing serialisation.
    public async Task<long> CountInStatusesStartingAfter(
        IReadOnlyCollection<string> statuses, DateTime slotStartAfter,
        string? stationId = null, string? slotId = null, string? nic = null)
    {
        if ((stationId is not null && !ObjectId.TryParse(stationId, out _))
            || (slotId is not null && !ObjectId.TryParse(slotId, out _)))
        {
            return 0;
        }

        var builder = Builders<Reservation>.Filter;
        var filter = builder.In(r => r.Status, statuses) & builder.Gt(r => r.SlotStart, slotStartAfter);

        if (stationId is not null) filter &= builder.Eq(r => r.StationId, stationId);
        if (slotId is not null) filter &= builder.Eq(r => r.SlotId, slotId);
        if (nic is not null) filter &= builder.Eq(r => r.ProsumerNIC, nic);

        return await _reservations.CountDocumentsAsync(filter);
    }

    // No status or time filter: this answers "does anything still point at
    // this slot", not "is anything active". An invalid id counts zero.
    public async Task<long> CountBySlot(string slotId)
    {
        if (!ObjectId.TryParse(slotId, out _))
        {
            return 0;
        }

        return await _reservations.CountDocumentsAsync(
            Builders<Reservation>.Filter.Eq(r => r.SlotId, slotId));
    }

    // Served by the {prosumerNIC: 1, slotStart: -1} index.
    public async Task<List<Reservation>> FindByNic(string nic, string? status)
    {
        var builder = Builders<Reservation>.Filter;
        var filter = builder.Eq(r => r.ProsumerNIC, nic);

        if (status is not null) filter &= builder.Eq(r => r.Status, status);

        return await _reservations.Find(filter).SortByDescending(r => r.SlotStart).ToListAsync();
    }

    // Every filter is part of the Mongo query, and the count uses the same
    // filter, so totalCount always describes exactly the set being paged.
    public async Task<(List<Reservation> Items, long TotalCount)> FindPaged(
        string? status, string? stationId, string? nic, DateTime? from, DateTime? to, int skip, int limit)
    {
        if (stationId is not null && !ObjectId.TryParse(stationId, out _))
        {
            return (new List<Reservation>(), 0);
        }

        var builder = Builders<Reservation>.Filter;
        var filter = builder.Empty;

        if (status is not null) filter &= builder.Eq(r => r.Status, status);
        if (stationId is not null) filter &= builder.Eq(r => r.StationId, stationId);
        if (nic is not null) filter &= builder.Eq(r => r.ProsumerNIC, nic);
        if (from is not null) filter &= builder.Gte(r => r.SlotStart, from.Value);
        if (to is not null) filter &= builder.Lte(r => r.SlotStart, to.Value);

        var total = await _reservations.CountDocumentsAsync(filter);
        var items = await _reservations.Find(filter)
            .SortByDescending(r => r.SlotStart)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();

        return (items, total);
    }

    // Pending -> Approved, recording who approved it and when.
    public async Task<Reservation?> MarkApproved(string id, string expectedStatus, string approvedBy, DateTime utcNow)
    {
        var update = Builders<Reservation>.Update
            .Set(r => r.Status, ReservationStatuses.Approved)
            .Set(r => r.ApprovedBy, approvedBy)
            .Set(r => r.ApprovedAt, utcNow)
            .Set(r => r.UpdatedAt, utcNow);

        return await CompareAndSet(id, expectedStatus, null, update);
    }

    // Pending -> Rejected, keeping the operator's reason.
    public async Task<Reservation?> MarkRejected(string id, string expectedStatus, string reason, DateTime utcNow)
    {
        var update = Builders<Reservation>.Update
            .Set(r => r.Status, ReservationStatuses.Rejected)
            .Set(r => r.RejectionReason, reason)
            .Set(r => r.UpdatedAt, utcNow);

        return await CompareAndSet(id, expectedStatus, null, update);
    }

    // Pending/Approved -> Cancelled, stamping cancelledAt for the audit trail.
    public async Task<Reservation?> MarkCancelled(string id, string expectedStatus, DateTime utcNow)
    {
        var update = Builders<Reservation>.Update
            .Set(r => r.Status, ReservationStatuses.Cancelled)
            .Set(r => r.CancelledAt, utcNow)
            .Set(r => r.UpdatedAt, utcNow);

        return await CompareAndSet(id, expectedStatus, null, update);
    }

    // Re-copies stationId/slotStart/slotEnd from the new slot, so the 12-hour
    // rule keeps evaluating against the slot the booking now points at.
    public async Task<Reservation?> UpdateBooking(
        string id, string expectedStatus, string expectedSlotId,
        BookingSlot newSlot, double energyKWh, bool clearQr, DateTime utcNow)
    {
        var update = Builders<Reservation>.Update
            .Set(r => r.SlotId, newSlot.Id)
            .Set(r => r.StationId, newSlot.StationId)
            .Set(r => r.SlotStart, newSlot.SlotStart)
            .Set(r => r.SlotEnd, newSlot.SlotEnd)
            .Set(r => r.EnergyKWh, energyKWh)
            .Set(r => r.UpdatedAt, utcNow);

        if (clearQr)
        {
            update = update
                .Unset(r => r.QrTokenHash)
                .Unset(r => r.QrIssuedAt)
                .Unset(r => r.QrExpiresAt);
        }

        var slotMatches = Builders<Reservation>.Filter.Eq(r => r.SlotId, expectedSlotId);
        return await CompareAndSet(id, expectedStatus, slotMatches, update);
    }

    // The shared compare-and-set: filter on _id AND the status the caller last
    // saw, apply the update, return the document after it (null = lost the race).
    private async Task<Reservation?> CompareAndSet(
        string id, string expectedStatus, FilterDefinition<Reservation>? extra, UpdateDefinition<Reservation> update)
    {
        var builder = Builders<Reservation>.Filter;
        var filter = builder.Eq(r => r.Id, id) & builder.Eq(r => r.Status, expectedStatus);

        if (extra is not null) filter &= extra;

        return await _reservations.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<Reservation> { ReturnDocument = ReturnDocument.After });
    }
}
