/*
 * File:    Reservation.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Document model for the EnergyReservation collection. SlotStart is
 *          copied at creation so the 12-hour rule cannot be moved by a later
 *          slot edit (docs/db-schema.md §4).
 */
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SmartSolar.Api.Models;

public class Reservation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("prosumerNIC")]
    public string ProsumerNIC { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string StationId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string SlotId { get; set; } = string.Empty;

    public DateTime SlotStart { get; set; }   // UTC, copied at creation
    public DateTime SlotEnd { get; set; }     // UTC, copied at creation — QR expiry is SlotEnd + QrSettings.TokenExpiryHours
    public double EnergyKWh { get; set; }
    public string Status { get; set; } = string.Empty;   // Pending | Approved | Rejected | Cancelled | Completed

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CancelledAt { get; set; }

    public string? QrTokenHash { get; set; }   // SHA-256 only; plaintext never stored
    public DateTime? QrIssuedAt { get; set; }
    public DateTime? QrExpiresAt { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? VerifiedByOperatorId { get; set; }
    public DateTime? CompletedAt { get; set; }
}
