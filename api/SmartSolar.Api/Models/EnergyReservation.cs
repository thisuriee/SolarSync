/*
 * File:    EnergyReservation.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: MongoDB document for a reservation, as required by the QR
 *          fulfilment vertical. A partial view of the EnergyReservation
 *          collection; Models/Reservation.cs remains the shared model.
 */
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SmartSolar.Api.Models;

public class EnergyReservation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    // Stored as "prosumerNIC" — the field name the collection already uses.
    [BsonElement("prosumerNIC")]
    public string Nic { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string StationId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string SlotId { get; set; } = string.Empty;

    public DateTime SlotStart { get; set; }   // UTC
    public DateTime SlotEnd { get; set; }     // UTC

    public string Status { get; set; } = string.Empty;   // Pending | Approved | Rejected | Cancelled | Completed

    public string? QrTokenHash { get; set; }
    public DateTime? QrIssuedAt { get; set; }
    public DateTime? QrExpiresAt { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? VerifiedByOperatorId { get; set; }

    public DateTime? CompletedAt { get; set; }
}
