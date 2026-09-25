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
    private readonly IMongoCollection<EnergyReservation> _reservations;

    public ReservationRepository(IMongoCollection<EnergyReservation> reservations)
    {
        _reservations = reservations;
    }

    // Finds a reservation by its ObjectId and returns the first match, or null.
    public async Task<EnergyReservation?> FindReservationById(string id)
    {
        var filter = Builders<EnergyReservation>.Filter.Eq(r => r.Id, id);
        return await _reservations.Find(filter).FirstOrDefaultAsync();
    }

    // Sets the three QR fields on the reservation. Nothing else is touched.
    public async Task UpdateQrFields(string id, string qrTokenHash, DateTime qrIssuedAt, DateTime qrExpiresAt)
    {
        var filter = Builders<EnergyReservation>.Filter.Eq(r => r.Id, id);
        var update = Builders<EnergyReservation>.Update
            .Set(r => r.QrTokenHash, qrTokenHash)
            .Set(r => r.QrIssuedAt, qrIssuedAt)
            .Set(r => r.QrExpiresAt, qrExpiresAt);

        await _reservations.UpdateOneAsync(filter, update);
    }
}
