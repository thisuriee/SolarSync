/*
 * File:    CreateReservationRequest.cs
 * Author:  Thisuri
 * Created: 2026-09-27
 * Purpose: Body of POST /api/reservations — {slotId, energyKWh, prosumerNIC?}.
 *          prosumerNIC is only honoured for a Backoffice caller booking on a
 *          prosumer's behalf; for a Prosumer the NIC always comes from the
 *          token and this field is ignored (ReservationService.ResolveBookingNic).
 *          There is deliberately no status, stationId or slotStart field:
 *          those are set or copied server-side.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class CreateReservationRequest
{
    [Required]
    public string SlotId { get; set; } = string.Empty;

    public double EnergyKWh { get; set; }

    public string? ProsumerNIC { get; set; }
}
