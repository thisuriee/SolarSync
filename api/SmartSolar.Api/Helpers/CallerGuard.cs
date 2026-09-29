/*
 * File:    CallerGuard.cs
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: The two service-layer checks every vertical repeats — is this role
 *          allowed to do this, and what is this caller's NIC.
 *
 *          Both were private to ReservationService. The dashboards vertical
 *          needs each of them: the history endpoint serves any role, but a
 *          prosumer is scoped to the token's own NIC regardless of the nic
 *          parameter. One copy means the AUTH_INVALID_TOKEN raised for a
 *          broken session reads identically wherever it surfaces.
 *
 *          These are re-checks. The controller's [Authorize] attribute has
 *          already refused the request; these hold the rule even if an
 *          attribute is ever dropped from a controller.
 */
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Helpers;

public static class CallerGuard
{
    // Throws 403 AUTH_FORBIDDEN_ROLE unless the caller holds one of roles.
    // The detail states the rule in the caller's terms, as the contract asks.
    public static void RequireRole(CallerIdentity caller, string detail, params string[] roles)
    {
        if (!roles.Contains(caller.Role))
        {
            throw UserErrors.ForbiddenRole(detail);
        }
    }

    // A prosumer token always carries a nic claim; if it does not, the session
    // is broken and the client must log in again. Returns the NIC so callers
    // cannot forget the check and dereference a null.
    public static string RequireNic(CallerIdentity caller)
    {
        if (string.IsNullOrWhiteSpace(caller.Nic))
        {
            throw new BusinessRuleException(
                "AUTH_INVALID_TOKEN",
                "Invalid session",
                "Your session is invalid. Please log in again.",
                StatusCodes.Status401Unauthorized);
        }

        return caller.Nic;
    }
}
