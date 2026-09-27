/*
 * File:    IStationRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Data access contract for the SolarStationInfo collection.
 *          FindById was added for the QR fulfilment vertical (Imadh); the
 *          listing, insert and replace were added for microgrid node
 *          management (Aman, 2026-09-27), and the proximity search for the
 *          nearby-stations map (Aman, 2026-09-28).
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public interface IStationRepository
{
    // Returns the station with the given id, or null when it does not exist.
    Task<SolarStation?> FindById(string id);

    // Returns stations ordered by name, narrowed by an exact status and a
    // case-insensitive match on station name or city. Both filters are
    // optional; null means "do not filter on this". The decision about which
    // status a given caller is allowed to see belongs to NodeService.
    Task<List<SolarStation>> FindAll(string? status, string? search);

    // Inserts a new station and returns the id the driver assigned.
    Task<string> Insert(SolarStation station);

    // Replaces a station document wholesale. Returns false when no document
    // matched, so the service can turn that into a 404.
    Task<bool> Replace(string id, SolarStation station);

    // Sets only status and updatedAt. A targeted update rather than a full
    // replace, so a status flip cannot clobber a field edited concurrently.
    Task<bool> UpdateStatus(string id, string status, DateTime updatedAt);

    // Stations within radiusKm of a point, in the given status, nearest first,
    // each carrying the distance the database measured. Ordering and distance
    // both come from the geospatial index; the caller neither sorts nor
    // measures anything. Capped at maxResults so a wide search cannot return
    // more markers than a map can usefully show.
    Task<List<NearbyStation>> FindNearby(
        double lat, double lng, double radiusKm, string status, int maxResults);
}
