/*
 * File:    JwtSettings.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Strongly-typed binding for the "JwtSettings" section — HS256
 *          secret, issuer, audience and token lifetime (docs/auth.md).
 */
namespace SmartSolar.Api.Configuration;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryHours { get; set; }
}
