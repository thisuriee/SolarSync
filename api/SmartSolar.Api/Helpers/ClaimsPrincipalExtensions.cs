/*
 * File:    ClaimsPrincipalExtensions.cs
 * Author:  Dahami
 * Created: 2026-09-26
 * Purpose: Reads the caller's identity from the validated JWT — the only
 *          source of identity in the API (docs/api-contract.md: "identity
 *          never travels in the payload"). Claim names match
 *          JwtTokenGenerator; Program.cs keeps them unmapped
 *          (MapInboundClaims = false).
 */
using System.Security.Claims;

namespace SmartSolar.Api.Helpers;

public static class ClaimsPrincipalExtensions
{
    // Returns the caller's Mongo _id from "sub". Fails closed with 401 if the
    // token somehow lacks it, rather than acting as an anonymous user.
    public static string GetUserId(this ClaimsPrincipal user)
    {
        return RequireClaim(user, "sub");
    }

    // Returns the caller's NIC from "nic", or null for web users (the claim
    // is only issued to prosumers).
    public static string? GetNic(this ClaimsPrincipal user)
    {
        var value = user.FindFirst("nic")?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    // Returns the caller's role from "role" — the same claim that
    // [Authorize(Roles = ...)] checks. Used for the service-layer re-checks.
    public static string GetRole(this ClaimsPrincipal user)
    {
        return RequireClaim(user, "role");
    }

    // Reads a mandatory claim or throws 401 so the client clears its session.
    private static string RequireClaim(ClaimsPrincipal user, string type)
    {
        var value = user.FindFirst(type)?.Value;

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                "AUTH_INVALID_TOKEN",
                "Invalid session",
                "Your session is invalid. Please log in again.",
                StatusCodes.Status401Unauthorized);
        }

        return value;
    }
}
