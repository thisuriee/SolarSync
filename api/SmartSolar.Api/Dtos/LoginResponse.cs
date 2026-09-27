/*
 * File:    LoginResponse.cs
 * Author:  Dahami
 * Created: 2026-09-26
 * Purpose: Response of POST /api/auth/login, shaped exactly as
 *          docs/api-contract.md §2: {token, expiresAt, user:{id, nic?, fullName, role}}.
 *          Both clients route to the correct home screen from user.role.
 */
using System.Text.Json.Serialization;

namespace SmartSolar.Api.Dtos;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    // UTC; serialised as ISO-8601 with a trailing Z.
    public DateTime ExpiresAt { get; set; }

    public LoginUser User { get; set; } = new();
}

public class LoginUser
{
    public string Id { get; set; } = string.Empty;

    // Prosumers only — omitted from the JSON for web users.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nic { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
