/*
 * File:    SlotRepository.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Mongo implementation of ISlotRepository. Pure data access — no
 *          business rules, no validation, no policy. The queries here are
 *          served by the {stationId: 1, slotStart: 1} and {slotStart: 1}
 *          indexes from docs/db-schema.md §3.
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

    // Looks one window up by id, returning null for an id that could never
    // be an ObjectId rather than letting the driver throw on the way out.
    public async Task<BookingSlot?> FindById(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return null;
        }

        var filter = Builders<BookingSlot>.Filter.Eq(s => s.Id, id);
        return await _slots.Find(filter).FirstOrDefaultAsync();
    }

    // Lists a station's windows in start order, narrowed to those starting
    // inside the supplied bounds when either is given.
    public async Task<List<BookingSlot>> FindByStation(string stationId, DateTime? from, DateTime? to)
    {
        if (!ObjectId.TryParse(stationId, out _))
        {
            return [];
        }

        var builder = Builders<BookingSlot>.Filter;
        var filter = builder.Eq(s => s.StationId, stationId);

        if (from.HasValue)
        {
            filter &= builder.Gte(s => s.SlotStart, from.Value);
        }

        if (to.HasValue)
        {
            filter &= builder.Lte(s => s.SlotStart, to.Value);
        }

        return await _slots.Find(filter).SortBy(s => s.SlotStart).ToListAsync();
    }

    // Finds windows on the same station that intersect [start, end).
    //
    // Two half-open ranges overlap when each begins before the other ends:
    // existing.slotStart < end AND start < existing.slotEnd. Because the
    // ranges are half-open, a window ending exactly when another begins is
    // adjacent rather than overlapping — which is the behaviour an operator
    // expects when filling a day back to back.
    public async Task<List<BookingSlot>> FindOverlapping(
        string stationId, DateTime start, DateTime end, string? excludeSlotId)
    {
        if (!ObjectId.TryParse(stationId, out _))
        {
            return [];
        }

        var builder = Builders<BookingSlot>.Filter;

        var filter = builder.Eq(s => s.StationId, stationId)
                   & builder.Lt(s => s.SlotStart, end)
                   & builder.Gt(s => s.SlotEnd, start);

        // On an update the window being edited is still stored, so without
        // this it would always be found overlapping itself and no edit could
        // ever be saved.
        if (!string.IsNullOrWhiteSpace(excludeSlotId) && ObjectId.TryParse(excludeSlotId, out _))
        {
            filter &= builder.Ne(s => s.Id, excludeSlotId);
        }

        return await _slots.Find(filter).SortBy(s => s.SlotStart).ToListAsync();
    }

    // Windows that can still be booked: on one of the given stations, Open,
    // with a free place, and starting inside the range.
    //
    // The spare-place test compares two fields of the same document, which a
    // plain filter cannot express, so it goes through $expr and is evaluated
    // by the database. Doing it in memory would mean fetching every window in
    // the range and discarding most of them.
    public async Task<List<BookingSlot>> FindBookable(
        IReadOnlyCollection<string> stationIds, DateTime from, DateTime to)
    {
        var usable = stationIds.Where(id => ObjectId.TryParse(id, out _)).ToList();

        if (usable.Count == 0)
        {
            return [];
        }

        var builder = Builders<BookingSlot>.Filter;

        var filter = builder.In(s => s.StationId, usable)
                   & builder.Eq(s => s.Status, SlotStatuses.Open)
                   & builder.Gt(s => s.SlotStart, from)
                   & builder.Lte(s => s.SlotStart, to)
                   & new BsonDocumentFilterDefinition<BookingSlot>(
                       new BsonDocument("$expr",
                           new BsonDocument("$lt",
                               new BsonArray { "$reservedCount", "$totalCapacity" })));

        return await _slots.Find(filter).SortBy(s => s.SlotStart).ToListAsync();
    }

    // Inserts the document; the driver writes the generated _id back onto the
    // model, which is what the caller returns to the client.
    public async Task<string> Insert(BookingSlot slot)
    {
        await _slots.InsertOneAsync(slot);
        return slot.Id;
    }

    // Full-document replace. MatchedCount of zero means the id did not exist,
    // which the service reports as a 404.
    public async Task<bool> Replace(string id, BookingSlot slot)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return false;
        }

        var filter = Builders<BookingSlot>.Filter.Eq(s => s.Id, id);
        var result = await _slots.ReplaceOneAsync(filter, slot);

        return result.MatchedCount > 0;
    }

    // Removes one window.
    public async Task<bool> Delete(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return false;
        }

        var filter = Builders<BookingSlot>.Filter.Eq(s => s.Id, id);
        var result = await _slots.DeleteOneAsync(filter);

        return result.DeletedCount > 0;
    }
}
