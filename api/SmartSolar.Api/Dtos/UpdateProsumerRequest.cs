/*
 * File:    UpdateProsumerRequest.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Body of PUT /api/prosumers/{nic}. Deliberately has NO nic, role or
 *          status property: if a client sends them, the JSON binder has
 *          nowhere to put them, so they are silently ignored and never applied
 *          (docs/api-contract.md §2).
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class UpdateProsumerRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\+?[0-9]{9,15}$", ErrorMessage = "Phone must be 9–15 digits, optionally starting with +.")]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 3)]
    public string Address { get; set; } = string.Empty;
}
