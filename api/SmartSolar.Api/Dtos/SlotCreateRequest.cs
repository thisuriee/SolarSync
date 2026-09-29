/*
 * File:    SlotCreateRequest.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Body of POST /api/nodes/{nodeId}/slots — a new bookable window on
 *          a microgrid node.
 *
 *          It has no reservedCount and no status property on purpose. Both
 *          are set server-side (0 and Open), and thereafter reservedCount
 *          moves only with the slot status, in one write, owned by the
 *          reservation side. A field a client cannot bind is a field a client
 *          cannot corrupt — which is cheaper than remembering to strip it.
 *
 *          The station is named by the route, not the body, so a window can
 *          never be created against one node while claiming another.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class SlotCreateRequest
{
    // UTC instant. Clients send ISO-8601 with a trailing Z and convert for
    // display only; every rule in the system is evaluated in UTC.
    [Required]
    public DateTime SlotStart { get; set; }

    [Required]
    public DateTime SlotEnd { get; set; }

    // How many prosumers may share this window. Bounded above by the node's
    // battery bay count, which SlotService checks against the parent station.
    [Range(1, 1000, ErrorMessage = "totalCapacity must be at least 1.")]
    public int TotalCapacity { get; set; }

    [Range(0.0001, 1_000_000.0, ErrorMessage = "energyPerSlotKWh must be greater than 0.")]
    public double EnergyPerSlotKWh { get; set; }
}
