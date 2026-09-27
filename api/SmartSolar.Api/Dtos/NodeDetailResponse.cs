/*
 * File:    NodeDetailResponse.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: GET /api/nodes/{id} — a node plus the slot summary and active
 *          reservation count the contract asks for (docs/api-contract.md §3).
 *
 *          ActiveReservationCount is the same number the deactivation rule
 *          tests, read through the one shared predicate. Surfacing it here
 *          lets the web node screen tell an officer why a deactivation is
 *          going to be refused before they click, without the client ever
 *          working that out for itself.
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Dtos;

public class NodeDetailResponse : NodeResponse
{
    // Every booking window belonging to this node, whatever its status.
    public long SlotCount { get; set; }

    // Windows still open for booking — the useful half of SlotCount.
    public long OpenSlotCount { get; set; }

    // Reservations that are Pending or Approved and start in the future.
    // Non-zero means deactivating this node will be refused.
    public long ActiveReservationCount { get; set; }

    // Projects a station plus its counts. The counts are supplied by the
    // service, which is the only layer allowed to ask two collections a
    // question about one resource.
    public static NodeDetailResponse FromStation(
        SolarStation station, long slotCount, long openSlotCount, long activeReservationCount)
    {
        var response = new NodeDetailResponse
        {
            SlotCount = slotCount,
            OpenSlotCount = openSlotCount,
            ActiveReservationCount = activeReservationCount
        };

        response.Fill(station);
        return response;
    }
}
