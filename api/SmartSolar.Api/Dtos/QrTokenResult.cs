// -----------------------------------------------------------------------------
// File Name:        QrTokenResult.cs
// Author:           Imadh
// Date Created:     25 September 2026
// Purpose:          Return type for IQrService.IssueQrForReservation — the
//                   plaintext QR token and the instant at which it expires.
// -----------------------------------------------------------------------------
namespace SmartSolar.Api.Dtos;

public class QrTokenResult
{
    // Plaintext token rendered as the QR code; only its SHA-256 hash is persisted.
    public string Token { get; set; } = string.Empty;

    // UTC expiry of the token, computed as slotEnd + QrSettings.TokenExpiryHours.
    public DateTime ExpiresAt { get; set; }
}
