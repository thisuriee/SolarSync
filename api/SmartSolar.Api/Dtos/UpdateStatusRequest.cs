/*
 * File:    UpdateStatusRequest.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Body of PATCH /api/webusers/{id}/status — {status}. The allowed
 *          values and the last-active-Backoffice guard are in UserService.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class UpdateStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}
