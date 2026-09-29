/*
 * File:    SlotResponse.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: The outward shape of an EnergyBookingSlots document.
 *
 *          reservedCount and status are reported but never accepted, so a
 *          client can show how full a window is without being able to say so.
 *          availableCapacity is derived here rather than stored: it is
 *          totalCapacity minus reservedCount and nothing else, and storing a
 *          value that is always computable is a second place for it to go
 *          wrong.
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Dtos;

public class SlotResponse
{
    public string Id { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;

    public DateTime SlotStart { get; set; }
    public DateTime SlotEnd { get; set; }

    public int TotalCapacity { get; set; }
    public int ReservedCount { get; set; }

    // Spare places left in this window. Never negative: the rules keep
    // reservedCount at or below totalCapacity, and clamping here means a
    // corrupted document shows 0 rather than a negative number on screen.
    public int AvailableCapacity { get; set; }

    public double EnergyPerSlotKWh { get; set; }
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Projects a stored window outward. The only mapping from the document
    // type to anything a client receives.
    public static SlotResponse FromSlot(BookingSlot slot)
    {
        return new SlotResponse
        {
            Id = slot.Id,
            StationId = slot.StationId,
            SlotStart = slot.SlotStart,
            SlotEnd = slot.SlotEnd,
            TotalCapacity = slot.TotalCapacity,
            ReservedCount = slot.ReservedCount,
            AvailableCapacity = Math.Max(0, slot.TotalCapacity - slot.ReservedCount),
            EnergyPerSlotKWh = slot.EnergyPerSlotKWh,
            Status = slot.Status,
            CreatedAt = slot.CreatedAt,
            UpdatedAt = slot.UpdatedAt
        };
    }
}
