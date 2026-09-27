/*
 * File:    SlotRepository.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Mongo implementation of ISlotRepository. Pure data access — no
 *          business rules, no validation, no policy. The counts here are
 *          served by the {stationId: 1, slotStart: 1} index from
 *          docs/db-schema.md §3.
 */
using MongoDB.Bson;
using MongoDB.Driver;
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public class SlotRepository : ISlotRepository
{
    private readonly IMongoCollection<BookingSlot> _slots;

    public SlotRepository(IMongoCollection<BookingSlot> slots)
    {
        _slots = slots;
    }

    // Counts every window on the station. An id that is not a valid ObjectId
    // cannot match anything, so it short-circuits to zero.
    public async Task<long> CountByStation(string stationId)
    {
        if (!ObjectId.TryParse(stationId, out _))
        {
            return 0;
        }

        var filter = Builders<BookingSlot>.Filter.Eq(s => s.StationId, stationId);
        return await _slots.CountDocumentsAsync(filter);
    }

    // Counts the windows on the station currently in one status.
    public async Task<long> CountByStationAndStatus(string stationId, string status)
    {
        if (!ObjectId.TryParse(stationId, out _))
        {
            return 0;
        }

        var builder = Builders<BookingSlot>.Filter;
        var filter = builder.Eq(s => s.StationId, stationId) & builder.Eq(s => s.Status, status);

        return await _slots.CountDocumentsAsync(filter);
    }

    // Sorts descending on totalCapacity and reads the first document, so the
    // database does the work rather than the application loading every window
    // to take a maximum. Returns 0 when the station has no windows.
    public async Task<int> MaxTotalCapacityForStation(string stationId)
    {
        if (!ObjectId.TryParse(stationId, out _))
        {
            return 0;
        }

        var filter = Builders<BookingSlot>.Filter.Eq(s => s.StationId, stationId);

        var largest = await _slots
            .Find(filter)
            .SortByDescending(s => s.TotalCapacity)
            .Limit(1)
            .FirstOrDefaultAsync();

        return largest?.TotalCapacity ?? 0;
    }
}
