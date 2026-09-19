/*
 * File:    BookingSlot.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Document model for the EnergyBookingSlots collection — a bookable
 *          window belonging to a station. ReservedCount is mutated only by
 *          ReservationService (docs/db-schema.md §3).
 */
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SmartSolar.Api.Models;

public class BookingSlot
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string StationId { get; set; } = string.Empty;

    public DateTime SlotStart { get; set; }   // UTC
    public DateTime SlotEnd { get; set; }     // UTC
    public int TotalCapacity { get; set; }
    public int ReservedCount { get; set; }
    public double EnergyPerSlotKWh { get; set; }
    public string Status { get; set; } = string.Empty;   // Open | Full | Closed

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
