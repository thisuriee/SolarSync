// -----------------------------------------------------------------------------
// File Name:        VerifyQrRequest.cs
// Author:           Imadh
// Date Created:     25 September 2026
// Purpose:          Request body for POST /api/fulfilment/verify-qr — the
//                   plaintext token decoded from the prosumer's QR code.
// -----------------------------------------------------------------------------
namespace SmartSolar.Api.Dtos;

public class VerifyQrRequest
{
    // Plaintext QR token. The only value the client supplies; identity, expiry
    // and reservation state are all resolved server-side.
    public string Token { get; set; } = string.Empty;
}
