/*
 * File:    IProsumerReservationCounter.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: The single thing identity needs from the reservation vertical: how
 *          many ACTIVE reservations a prosumer holds. The predicate itself
 *          (status in {Pending, Approved} AND slotStart > utcNow) is owned by
 *          M3 (docs/api-contract.md §6) — identity never writes its own copy.
 */
namespace SmartSolar.Api.Services;

public interface IProsumerReservationCounter
{
    // Number of the prosumer's reservations that are still active, using
    // M3's shared active-reservation predicate.
    Task<long> CountActiveForProsumer(string nic);
}
