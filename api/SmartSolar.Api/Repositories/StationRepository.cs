/*
 * File:    StationRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Mongo implementation of IStationRepository. Pure data access — no
 *          business rules, no validation, no policy. Listing, insert, replace
 *          and the status update were added for microgrid node management
 *          (Aman, 2026-09-27).
 */
using System.Text.RegularExpressions;
using MongoDB.Bson;
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

    // Looks the station up by its ObjectId. An id that is not a valid
    // ObjectId cannot exist, so it returns null rather than letting the
    // driver throw a format exception on the way to the database.
    public async Task<SolarStation?> FindById(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return null;
        }

        var filter = Builders<SolarStation>.Filter.Eq(s => s.Id, id);
        return await _stations.Find(filter).FirstOrDefaultAsync();
    }

    // Applies the status and search filters in the query itself, so the
    // database returns only the matching rows. The status filter is served by
    // the {status: 1} index from docs/db-schema.md §2.
    public async Task<List<SolarStation>> FindAll(string? status, string? search)
    {
        var builder = Builders<SolarStation>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= builder.Eq(s => s.Status, status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Escaped, so a search term containing regex metacharacters is
            // matched literally instead of being executed as a pattern.
            var pattern = new BsonRegularExpression(Regex.Escape(search.Trim()), "i");

            filter &= builder.Or(
                builder.Regex(s => s.StationName, pattern),
                builder.Regex(s => s.City, pattern));
        }

        return await _stations
            .Find(filter)
            .SortBy(s => s.StationName)
            .ToListAsync();
    }

    // Inserts the document; the driver writes the generated _id back onto the
    // model, which is what the caller returns to the client.
    public async Task<string> Insert(SolarStation station)
    {
        await _stations.InsertOneAsync(station);
        return station.Id;
    }

    // Full-document replace for PUT. MatchedCount of zero means the id did
    // not exist, which the service reports as a 404.
    public async Task<bool> Replace(string id, SolarStation station)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return false;
        }

        var filter = Builders<SolarStation>.Filter.Eq(s => s.Id, id);
        var result = await _stations.ReplaceOneAsync(filter, station);

        return result.MatchedCount > 0;
    }

    // Proximity search, run entirely by the database against the 2dsphere
    // index on location (docs/db-schema.md §2).
    //
    // $geoNear is used rather than a plain $near filter because it returns the
    // distance it measured. With $near the application would have to work the
    // distance out again from the coordinates, which is the calculation the
    // index exists to avoid, and the phone would then be doing geometry.
    //
    // Four things about this stage are easy to get wrong:
    //
    //   - it MUST be the first stage of the pipeline. MongoDB rejects it
    //     anywhere else, which is why the status filter goes in its own
    //     "query" field instead of a $match before it
    //   - "coordinates" is [longitude, latitude], the same order the
    //     documents are stored in
    //   - "maxDistance" is in METRES, so the caller's kilometres are
    //     multiplied up
    //   - "distanceMultiplier" scales the measured distance on the way out,
    //     so the field is already in kilometres and nothing downstream has to
    //     remember which unit it is in
    //
    // The stage sorts nearest-first by construction, so the $limit that
    // follows keeps the closest results rather than an arbitrary set — and no
    // caller should re-sort what comes back.
    //
    // This query requires the 2dsphere index to exist. Without it MongoDB
    // refuses the stage outright rather than falling back to a scan, which is
    // the right failure: a silent collection scan here would be invisible
    // until it was slow.
    public async Task<List<NearbyStation>> FindNearby(
        double lat, double lng, double radiusKm, string status, int maxResults)
    {
        var stages = new[]
        {
            new BsonDocument("$geoNear", new BsonDocument
            {
                {
                    "near", new BsonDocument
                    {
                        { "type", "Point" },
                        { "coordinates", new BsonArray { lng, lat } }
                    }
                },
                { "distanceField", "distanceKm" },
                { "distanceMultiplier", 0.001 },
                { "maxDistance", radiusKm * 1000.0 },
                { "spherical", true },
                { "query", new BsonDocument("status", status) }
            }),
            new BsonDocument("$limit", maxResults)
        };

        var pipeline = PipelineDefinition<SolarStation, NearbyStation>.Create(stages);

        return await _stations.Aggregate(pipeline).ToListAsync();
    }

    // Touches status and updatedAt only, leaving every other field alone.
    public async Task<bool> UpdateStatus(string id, string status, DateTime updatedAt)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return false;
        }

        var filter = Builders<SolarStation>.Filter.Eq(s => s.Id, id);
        var update = Builders<SolarStation>.Update
            .Set(s => s.Status, status)
            .Set(s => s.UpdatedAt, updatedAt);

        var result = await _stations.UpdateOneAsync(filter, update);
        return result.MatchedCount > 0;
    }
}
