/*
 * File:    SlotUpdateRequest.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: Body of PUT /api/slots/{id} — a full update of a bookable window's
 *          editable fields.
 *
 *          Like the create request it carries no reservedCount and no status.
 *          The contract says both are ignored when they arrive; the cleanest
 *          way to ignore a field is to leave it off the type, so there is
 *          nothing to bind, nothing to forget to strip, and no defensive code
 *          to get wrong later.
 *
 *          stationId is absent too: a window belongs to the node it was
 *          created against and cannot be moved to another one, because that
 *          would silently relocate any booking already placed on it.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class SlotUpdateRequest
{
    [Required]
    public DateTime SlotStart { get; set; }

    [Required]
    public DateTime SlotEnd { get; set; }

    [Range(1, 1000, ErrorMessage = "totalCapacity must be at least 1.")]
    public int TotalCapacity { get; set; }

    [Range(0.0001, 1_000_000.0, ErrorMessage = "energyPerSlotKWh must be greater than 0.")]
    public double EnergyPerSlotKWh { get; set; }
}
