/*
 * File:    QrSettings.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Strongly-typed binding for the "QrSettings" section — how long an
 *          issued QR token remains valid after slotEnd, and how long the
 *          verificationId returned by VerifyQrToken stays valid.
 *
 *          VerificationIdExpiryMinutes added by Imadh, 2026-09-25.
 */
namespace SmartSolar.Api.Configuration;

public class QrSettings
{
    // Hours after slotEnd that an issued QR token stays valid. Default 2 — a short
    // grace period for scanning a booking whose slot has already started.
    public int TokenExpiryHours { get; set; } = 2;

    // Minutes a verificationId from VerifyQrToken stays valid. Default 5 — kept
    // short so a captured verificationId cannot be replayed later.
    public int VerificationIdExpiryMinutes { get; set; } = 5;
}
