/*
 * File:    RejectReservationRequest.cs
 * Author:  Thisuri
 * Created: 2026-09-27
 * Purpose: Body of PATCH /api/reservations/{id}/reject — {reason}. A reason is
 *          mandatory so the prosumer always sees why they were refused.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class RejectReservationRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}
