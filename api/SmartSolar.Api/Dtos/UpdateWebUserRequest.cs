/*
 * File:    UpdateWebUserRequest.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Body of PUT /api/webusers/{id} — the full editable object.
 *          Username (login identity) and password are not editable here.
 *          Role/status values and the last-active-Backoffice guard are
 *          checked in UserService.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class UpdateWebUserRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\+?[0-9]{9,15}$", ErrorMessage = "Phone must be 9–15 digits, optionally starting with +.")]
    public string Phone { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = string.Empty;
}
