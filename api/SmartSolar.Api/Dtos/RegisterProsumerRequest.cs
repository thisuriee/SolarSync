/*
 * File:    RegisterProsumerRequest.cs
 * Author:  Dahami
 * Created: 2026-09-26
 * Purpose: Body of POST /api/auth/register-prosumer. Deliberately has NO role
 *          or status property: both are forced server-side (Prosumer / Pending),
 *          so a client editing the request has no field to set them with.
 *          Shape checks here produce the automatic 400; the NIC format and
 *          uniqueness rules live in AuthService.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class RegisterProsumerRequest
{
    [Required]
    public string Nic { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    // Digits with an optional leading +, e.g. 0771234567 or +94771234567.
    [Required, RegularExpression(@"^\+?[0-9]{9,15}$", ErrorMessage = "Phone must be 9–15 digits, optionally starting with +.")]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 3)]
    public string Address { get; set; } = string.Empty;

    [Required, RegularExpression(@"^[A-Za-z0-9._]{3,30}$", ErrorMessage = "Username must be 3–30 letters, digits, dots or underscores.")]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;
}
