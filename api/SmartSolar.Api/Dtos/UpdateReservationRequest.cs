/*
 * File:    UpdateReservationRequest.cs
 * Author:  Thisuri
 * Created: 2026-09-27
 * Purpose: Body of PUT /api/reservations/{id} — {slotId, energyKWh}. The
 *          owner cannot be changed by an update, so there is no NIC field;
 *          the 12-hour and 7-day rules are applied in ReservationService.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class UpdateReservationRequest
{
    [Required]
    public string SlotId { get; set; } = string.Empty;

    public double EnergyKWh { get; set; }
}
