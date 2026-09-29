/*
 * File:    SlotCapacityRepository.cs
 * Author:  Thisuri
 * Created: 2026-09-27
 * Purpose: Mongo implementation of ISlotCapacityRepository. The claim and the
 *          release are each ONE FindOneAndUpdate whose filter carries the
 *          capacity condition and whose update is an aggregation pipeline, so
 *          the check and the write happen inside the database as one step.
 *
 *          Why this matters: with read-then-write, two prosumers booking the
 *          last space both read reservedCount = capacity - 1, both pass, and
 *          the slot is overbooked. Here the second request's filter no longer
 *          matches ($expr reservedCount < totalCapacity is false), so it gets
 *          null and the service answers 409 RESERVATION_SLOT_FULL.
 *
 *          Field names are the camelCase stored names (BsonConventions).
 */
using MongoDB.Bson;
using MongoDB.Driver;
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public class SlotCapacityRepository : ISlotCapacityRepository
{
    private readonly IMongoCollection<BookingSlot> _slots;

    public SlotCapacityRepository(IMongoCollection<BookingSlot> slots)
    {
        _slots = slots;
    }

    // Reads one slot by id. An id that is not a valid ObjectId cannot match
    // anything, so it returns null instead of failing serialisation.
    public async Task<BookingSlot?> FindById(string slotId)
    {
        if (!ObjectId.TryParse(slotId, out _))
        {
            return null;
        }

        return await _slots.Find(s => s.Id == slotId).FirstOrDefaultAsync();
    }

    // Claim: the filter only matches an Open slot with a free space; the
    // pipeline adds one and sets status from the NEW count in the same write.
    // Inside one $set stage every "$field" reads the document as it was
    // before the stage, so "$reservedCount" in the status expression is the
    // old value — hence the +1 there too.
    public async Task<BookingSlot?> TryClaim(string slotId, DateTime utcNow)
    {
        if (!ObjectId.TryParse(slotId, out _))
        {
            return null;
        }

        var builder = Builders<BookingSlot>.Filter;
        var filter = builder.Eq(s => s.Id, slotId)
            & builder.Eq(s => s.Status, SlotStatuses.Open)
            & new BsonDocument("$expr",
                new BsonDocument("$lt", new BsonArray { "$reservedCount", "$totalCapacity" }));

        var newCount = new BsonDocument("$add", new BsonArray { "$reservedCount", 1 });

        var set = new BsonDocument("$set", new BsonDocument
        {
            { "reservedCount", newCount },
            { "status", new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$gte", new BsonArray { newCount, "$totalCapacity" }),
                    SlotStatuses.Full,
                    SlotStatuses.Open
                }) },
            { "updatedAt", utcNow }
        });

        var update = Builders<BookingSlot>.Update.Pipeline(
            PipelineDefinition<BookingSlot, BookingSlot>.Create(new[] { set }));

        return await _slots.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<BookingSlot> { ReturnDocument = ReturnDocument.After });
    }

    // Release: the filter refuses to go below zero; the pipeline takes one
    // off and reopens the slot unless an operator has Closed it, which a
    // cancellation must never undo.
    public async Task<bool> Release(string slotId, DateTime utcNow)
    {
        if (!ObjectId.TryParse(slotId, out _))
        {
            return false;
        }

        var builder = Builders<BookingSlot>.Filter;
        var filter = builder.Eq(s => s.Id, slotId) & builder.Gt(s => s.ReservedCount, 0);

        var set = new BsonDocument("$set", new BsonDocument
        {
            { "reservedCount", new BsonDocument("$subtract", new BsonArray { "$reservedCount", 1 }) },
            { "status", new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray { "$status", SlotStatuses.Closed }),
                    SlotStatuses.Closed,
                    SlotStatuses.Open
                }) },
            { "updatedAt", utcNow }
        });

        var update = Builders<BookingSlot>.Update.Pipeline(
            PipelineDefinition<BookingSlot, BookingSlot>.Create(new[] { set }));

        var released = await _slots.FindOneAndUpdateAsync(filter, update);
        return released is not null;
    }
}
