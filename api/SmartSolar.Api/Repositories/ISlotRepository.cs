/*
 * File:    ISlotRepository.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Data access contract for the EnergyBookingSlots collection —
 *          the reads the node screens need, the overlap and availability
 *          queries the rules are built on, and the writes behind slot CRUD.
 *
 *          reservedCount is never written through this interface. Only the
 *          reservation side may change it, always together with the slot
 *          status, in one write (docs/db-schema.md §3). Replace preserves
 *          whatever the stored document holds.
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public interface ISlotRepository
{
    // Total booking windows belonging to a station, whatever their status.
    Task<long> CountByStation(string stationId);

    // Booking windows on a station currently in the given status.
    Task<long> CountByStationAndStatus(string stationId, string status);

    // The largest totalCapacity promised by any window on this station, or 0
    // when it has none. A station's battery count can never be reduced below
    // this without breaking a window that is already published.
    Task<int> MaxTotalCapacityForStation(string stationId);

    // One window by id, or null when it does not exist.
    Task<BookingSlot?> FindById(string id);

    // A station's windows ordered by start time, optionally narrowed to those
    // starting within a range. Served by {stationId: 1, slotStart: 1}.
    Task<List<BookingSlot>> FindByStation(string stationId, DateTime? from, DateTime? to);

    // Windows on the same station whose time range intersects [start, end),
    // excluding one id so a window being edited does not collide with itself.
    // Returns the matches; deciding what they mean belongs to the service.
    Task<List<BookingSlot>> FindOverlapping(
        string stationId, DateTime start, DateTime end, string? excludeSlotId);

    // Bookable windows across the given stations: still Open, with spare
    // capacity, and starting inside the supplied time range.
    Task<List<BookingSlot>> FindBookable(
        IReadOnlyCollection<string> stationIds, DateTime from, DateTime to);

    // Inserts a new window and returns the id the driver assigned.
    Task<string> Insert(BookingSlot slot);

    // Replaces a window wholesale. False when no document matched.
    Task<bool> Replace(string id, BookingSlot slot);

    // Removes a window. False when no document matched.
    Task<bool> Delete(string id);
}
