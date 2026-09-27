/*
 * File:    UserProfileResponse.cs
 * Author:  Dahami
 * Created: 2026-09-26
 * Purpose: The safe outward shape of a UsersDetail document, used by
 *          /api/auth/me and (step 2) the web user and prosumer endpoints.
 *          It has no password field at all, so a User can only leave the API
 *          through FromUser — this is the "excluded at the DTO layer" rule.
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Dtos;

public class UserProfileResponse
{
    public string Id { get; set; } = string.Empty;
    public string? Nic { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? DeactivationRequestedAt { get; set; }
    public string? ActivatedBy { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Copies every field except PasswordHash. The only mapping from the
    // User document to anything a client receives.
    public static UserProfileResponse FromUser(User user)
    {
        return new UserProfileResponse
        {
            Id = user.Id,
            Nic = user.Nic,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Address = user.Address,
            Role = user.Role,
            Status = user.Status,
            DeactivationRequestedAt = user.DeactivationRequestedAt,
            ActivatedBy = user.ActivatedBy,
            ActivatedAt = user.ActivatedAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
