// -----------------------------------------------------------------------------
// File Name:        CompleteReservationRequest.cs
// Author:           Imadh
// Date Created:     25 September 2026
// Purpose:          Request body for PATCH /api/reservations/{id}/complete —
//                   the verificationId issued by a successful QR scan.
// -----------------------------------------------------------------------------
namespace SmartSolar.Api.Dtos;

public class CompleteReservationRequest
{
    // Proof that a scan happened. Without a live value here the transfer cannot
    // be finalised, which is what stops completion without a real QR scan.
    public string VerificationId { get; set; } = string.Empty;
}
