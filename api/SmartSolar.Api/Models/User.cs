/*
 * File:    User.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Document model for the UsersDetail collection. All three roles
 *          live here, discriminated by Role (docs/db-schema.md §1).
 */
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SmartSolar.Api.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string? Nic { get; set; }                 // prosumers only, immutable after creation
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string PasswordHash { get; set; } = string.Empty;   // never leaves the DTO layer
    public string Role { get; set; } = string.Empty;           // Backoffice | GridOperator | Prosumer
    public string Status { get; set; } = string.Empty;         // Pending | Active | Deactivated
    public DateTime? DeactivationRequestedAt { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? ActivatedBy { get; set; }
    public DateTime? ActivatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
