/*
 * File:    ExceptionHandlingMiddleware.cs
 * Author:  Dahami
 * Created: 2026-09-19
 * Purpose: Global exception handler — first in the pipeline. Translates
 *          BusinessRuleException into its own status/code and everything
 *          else into a generic 500. Services throw, this formats; no
 *          controller ever returns Conflict(new {...}) by hand.
 */
using System.Diagnostics;
using SmartSolar.Api.Helpers;

namespace SmartSolar.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // Wraps the rest of the pipeline in try/catch. Business rule failures
    // keep their own status; anything unexpected becomes a 500 with the
    // stack trace in the log only, never in the body.
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessRuleException ex)
        {
            await WriteProblem(context, ex.StatusCode, ex.Code, ex.Title, ex.Detail);
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception. traceId={TraceId}", traceId);

            await WriteProblem(
                context,
                StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR",
                "An unexpected error occurred",
                "The request could not be completed. Quote the traceId when reporting this.");
        }
    }

    // The single place that writes the problem object, with field names
    // matching docs/response-format.md exactly. Status is set here and only
    // here — once the response has started it cannot be changed.
    private static Task WriteProblem(HttpContext ctx, int status, string code, string title, string detail)
    {
        if (ctx.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        ctx.Response.Clear();
        ctx.Response.StatusCode = status;

        var problem = new
        {
            type = $"https://smartsolar/errors/{code.ToLowerInvariant().Replace('_', '-')}",
            title,
            status,
            code,
            detail,
            traceId = Activity.Current?.Id ?? ctx.TraceIdentifier,
            errors = (object?)null
        };

        return ctx.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
