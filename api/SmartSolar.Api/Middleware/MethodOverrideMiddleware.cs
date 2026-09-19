/*
 * File:    MethodOverrideMiddleware.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Android's HttpURLConnection cannot send PATCH. An authenticated
 *          POST carrying "X-HTTP-Method-Override: PATCH" is rewritten to
 *          PATCH here. Must run after UseAuthentication (so User is
 *          populated) and before UseRouting (so the rewrite affects
 *          endpoint selection).
 */
namespace SmartSolar.Api.Middleware;

public class MethodOverrideMiddleware
{
    private const string HeaderName = "X-HTTP-Method-Override";
    private readonly RequestDelegate _next;

    public MethodOverrideMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // Rewrites POST -> PATCH only when the header value is exactly "PATCH"
    // and the caller is authenticated. Gating on authentication stops the
    // header being a way to reach any PATCH route anonymously. No other
    // verb is honoured.
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method)
            && context.User?.Identity?.IsAuthenticated == true
            && context.Request.Headers.TryGetValue(HeaderName, out var value)
            && value.ToString() == "PATCH")
        {
            context.Request.Method = HttpMethods.Patch;
        }

        await _next(context);
    }
}
