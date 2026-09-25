/*
 * File:    ReservationRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Mongo implementation of IReservationRepository. Pure data access —
 *          no business rules, no validation, no policy.
 */
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
}
