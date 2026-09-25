/*
 * File:    StationRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Mongo implementation of IStationRepository. Pure data access — no
 *          business rules, no validation, no policy.
 */
using MongoDB.Driver;
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public class StationRepository : IStationRepository
{
    private readonly IMongoCollection<SolarStation> _stations;

    public StationRepository(IMongoCollection<SolarStation> stations)
    {
        _stations = stations;
    }

    // Looks the station up by its ObjectId.
    public async Task<SolarStation?> FindById(string id)
    {
        var filter = Builders<SolarStation>.Filter.Eq(s => s.Id, id);
        return await _stations.Find(filter).FirstOrDefaultAsync();
    }
}
