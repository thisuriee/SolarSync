/*
 * File:    IStationRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Data access contract for the QR fulfilment vertical — resolves the
 *          station a reservation belongs to.
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public interface IStationRepository
{
    // Returns the station with the given id, or null when it does not exist.
    Task<SolarStation?> FindById(string id);
}
