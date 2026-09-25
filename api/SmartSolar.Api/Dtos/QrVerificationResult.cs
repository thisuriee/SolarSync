// -----------------------------------------------------------------------------
// File Name:        QrVerificationResult.cs
// Author:           Imadh
// Date Created:     25 September 2026
// Purpose:          Return type for IQrService.VerifyQrToken — the booking
//                   details the operator confirms on screen, plus the
//                   short-lived verificationId that authorises completion.
// -----------------------------------------------------------------------------
namespace SmartSolar.Api.Dtos;

public class QrVerificationResult
{
    public string ReservationId { get; set; } = string.Empty;
    public string ProsumerNic { get; set; } = string.Empty;
    public string ProsumerName { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;

    // UTC booking window the operator is confirming.
    public DateTime SlotStart { get; set; }
    public DateTime SlotEnd { get; set; }

    public double EnergyKWh { get; set; }

    // Single-use proof that a scan happened. Valid for
    // QrSettings.VerificationIdExpiryMinutes and required by CompleteReservation.
    public string VerificationId { get; set; } = string.Empty;
    public DateTime VerificationExpiresAt { get; set; }
}
