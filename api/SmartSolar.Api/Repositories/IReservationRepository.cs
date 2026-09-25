/*
 * File:    IReservationRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Data access contract for the QR fulfilment vertical — the lookup
 *          and the QR field write that QR issuance needs.
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public interface IReservationRepository
{
    // Returns the reservation with the given id, or null when it does not exist.
    Task<EnergyReservation?> FindReservationById(string id);

    // Writes the issued QR token hash and its validity window onto a reservation.
    Task UpdateQrFields(string id, string qrTokenHash, DateTime qrIssuedAt, DateTime qrExpiresAt);
}
