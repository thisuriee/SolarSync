/*
 * File:    ReservationResponse.cs
 * Author:  Thisuri
 * Created: 2026-09-27
 * Purpose: The outward shape of an EnergyReservation document — every field a
 *          client needs for the detail screen and its audit trail (created,
 *          updated, approved, cancelled, completed timestamps). qrTokenHash is
 *          deliberately NOT exposed: even a hash of the token has no business
 *          leaving the API. The plaintext token is only ever returned by M4's
 *          GET /reservations/{id}/qr.
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Dtos;

public class ReservationResponse
{
    public string Id { get; set; } = string.Empty;
    public string ProsumerNIC { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public string SlotId { get; set; } = string.Empty;
    public DateTime SlotStart { get; set; }
    public DateTime SlotEnd { get; set; }
    public double EnergyKWh { get; set; }
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? QrIssuedAt { get; set; }
    public DateTime? QrExpiresAt { get; set; }
    public string? VerifiedByOperatorId { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Projects a stored reservation outward; the only mapping from the
    // document to anything a client receives.
    public static ReservationResponse FromReservation(Reservation r)
    {
        return new ReservationResponse
        {
            Id = r.Id,
            ProsumerNIC = r.ProsumerNIC,
            StationId = r.StationId,
            SlotId = r.SlotId,
            SlotStart = r.SlotStart,
            SlotEnd = r.SlotEnd,
            EnergyKWh = r.EnergyKWh,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            ApprovedBy = r.ApprovedBy,
            ApprovedAt = r.ApprovedAt,
            RejectionReason = r.RejectionReason,
            CancelledAt = r.CancelledAt,
            QrIssuedAt = r.QrIssuedAt,
            QrExpiresAt = r.QrExpiresAt,
            VerifiedByOperatorId = r.VerifiedByOperatorId,
            CompletedAt = r.CompletedAt
        };
    }
}
