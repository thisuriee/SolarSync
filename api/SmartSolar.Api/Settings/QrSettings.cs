// -----------------------------------------------------------------------------
// File Name:        QrSettings.cs
// Author:           Imadh
// Date Created:     25 September 2026
// Purpose:          Configuration settings for QR token issuance, verification
//                   and completion in the Smart Solar Microgrid Trading System.
// -----------------------------------------------------------------------------
namespace SmartSolar.Api.Settings;

public class QrSettings
{
    // Hours after slotEnd that an issued QR token stays valid. Default 2 — a short
    // grace period for scanning a booking whose slot has already started.
    public int TokenExpiryHours { get; set; } = 2;

    // Minutes a verificationId from VerifyQrToken stays valid. Default 5 — kept
    // short so a captured verificationId cannot be replayed later.
    public int VerificationIdExpiryMinutes { get; set; } = 5;
}
