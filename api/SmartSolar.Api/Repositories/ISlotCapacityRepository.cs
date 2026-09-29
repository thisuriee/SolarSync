/*
 * File:    ISlotCapacityRepository.cs
 * Author:  Thisuri
 * Created: 2026-09-27
 * Purpose: The ONLY writer of EnergyBookingSlots.reservedCount
 *          (docs/api-contract.md §6, docs/db-schema.md §3). Every change to
 *          reservedCount happens here, together with the slot status flip,
 *          in one atomic FindOneAndUpdate — never as a read followed by a
 *          write. Kept separate from M2's ISlotRepository so slot CRUD cannot
 *          grow a second path that touches the counter.
 *
 *          Also holds the one slot read the reservation workflow needs.
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public interface ISlotCapacityRepository
{
    // Returns the booking window with the given id, or null when there is none
    // (including when the id is not a valid ObjectId).
    Task<BookingSlot?> FindById(string slotId);

    // Atomically takes one space on an Open slot that still has capacity, and
    // flips it to Full when that was the last space. Returns the updated slot,
    // or null when the slot was Closed, Full, or gone — the caller turns null
    // into 409 RESERVATION_SLOT_FULL.
    Task<BookingSlot?> TryClaim(string slotId, DateTime utcNow);

    // Atomically gives one space back and reopens a Full slot. A Closed slot
    // stays Closed. Returns false when there was nothing to release.
    Task<bool> Release(string slotId, DateTime utcNow);
}
