/*
 * File:    JwtTokenGenerator.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Issues the HS256 access token described in docs/auth.md. Claims
 *          are emitted with their short names (sub, nic, role, name); the
 *          bearer setup in Program.cs sets RoleClaimType = "role" to match.
 */
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartSolar.Api.Configuration;

namespace SmartSolar.Api.Helpers;

public class JwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }

    // Builds and signs a token for the given user. "nic" is only added for
    // prosumers (null for web users). Expiry is UTC now + JwtSettings.ExpiryHours.
    public (string token, DateTime expiresAt) Generate(string userId, string? nic, string fullName, string role)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddHours(_settings.ExpiryHours);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new("role", role),
            new("name", fullName)
        };

        if (!string.IsNullOrWhiteSpace(nic))
        {
            claims.Add(new Claim("nic", nic));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
