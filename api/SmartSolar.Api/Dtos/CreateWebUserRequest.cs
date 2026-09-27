/*
 * File:    CreateWebUserRequest.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Body of POST /api/webusers. Shape checks here produce the automatic
 *          400; the rule that role must be Backoffice or GridOperator (never
 *          Prosumer) is enforced in UserService so it returns USER_ROLE_INVALID.
 *          No status field — a web user created by Backoffice starts Active.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class CreateWebUserRequest
{
    [Required, RegularExpression(@"^[A-Za-z0-9._]{3,30}$", ErrorMessage = "Username must be 3–30 letters, digits, dots or underscores.")]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\+?[0-9]{9,15}$", ErrorMessage = "Phone must be 9–15 digits, optionally starting with +.")]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}
