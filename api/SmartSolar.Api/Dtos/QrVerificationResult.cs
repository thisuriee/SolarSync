// -----------------------------------------------------------------------------
// File Name:        QrVerificationResult.cs
// Author:           Imadh
// Date Created:     25 September 2026
// Purpose:          Return type for IQrService.VerifyQrToken — the booking
//                   details the operator confirms on screen, plus the
//                   short-lived verificationId that authorises completion.
// -----------------------------------------------------------------------------
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Dtos;

public class QrVerificationResult
{
    // The reservation being fulfilled; only ever returned in its Approved state.
    public Reservation Reservation { get; set; } = new();

    // The prosumer who owns the reservation, resolved from the reservation's NIC.
    public User Prosumer { get; set; } = new();

    // The microgrid node the energy transfer takes place at.
    public SolarStation Station { get; set; } = new();

    // Single-use proof that the QR was scanned; required by CompleteReservation.
    public string VerificationId { get; set; } = string.Empty;
}
