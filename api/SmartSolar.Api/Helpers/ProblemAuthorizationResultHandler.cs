/*
 * File:    ProblemAuthorizationResultHandler.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Makes framework-level auth failures ([Authorize] / [Authorize(Roles)])
 *          return the same problem object as every other error, with a code
 *          clients can switch on. Without it the framework answers with an
 *          empty 401/403 body, so a Grid Operator hitting /activate would get
 *          a 403 with no AUTH_FORBIDDEN_ROLE code (docs/api-contract.md §2).
 */
// Extension point documented at:
// https://learn.microsoft.com/en-us/aspnet/core/security/authorization/customizingauthorizationmiddlewareresponse
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace SmartSolar.Api.Helpers;

public class ProblemAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    // Rule: wrong role -> 403 AUTH_FORBIDDEN_ROLE; no/expired/invalid token ->
    // 401 AUTH_INVALID_TOKEN (clients clear the session). Throwing lets
    // ExceptionHandlingMiddleware, which wraps authorization, write the body.
    // Successful requests go through the framework's default handler.
    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            throw new BusinessRuleException(
                "AUTH_FORBIDDEN_ROLE",
                "Not permitted",
                "Your role is not permitted to perform this action.",
                StatusCodes.Status403Forbidden);
        }

        if (authorizeResult.Challenged)
        {
            throw new BusinessRuleException(
                "AUTH_INVALID_TOKEN",
                "Not signed in",
                "Your session is missing or has expired. Please log in again.",
                StatusCodes.Status401Unauthorized);
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
