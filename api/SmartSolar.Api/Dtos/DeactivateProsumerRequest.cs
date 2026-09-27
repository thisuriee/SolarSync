/*
 * File:    DeactivateProsumerRequest.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Optional body of PATCH /api/prosumers/{nic}/deactivate — {reason?}.
 *          The reason is stored on the account as an audit note.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class DeactivateProsumerRequest
{
    [StringLength(300)]
    public string? Reason { get; set; }
}
