/*
 * File:    ISlotRepository.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Data access contract for the EnergyBookingSlots collection.
 *          Currently the reads the node screens need: how many windows a
 *          station has, how many are still open, and the largest capacity
 *          already promised on it — the last of which is what stops a node's
 *          battery count being reduced below a window that already depends on
 *          it (NODE_CAPACITY_CONFLICT, docs/api-contract.md §3).
 *
 *          Slot writes belong here too and are added with the slot endpoints.
 *          reservedCount is never written through this interface: only
 *          ReservationService may change it, always together with the slot
 *          status, in one operation (docs/db-schema.md §3).
 */
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
}
