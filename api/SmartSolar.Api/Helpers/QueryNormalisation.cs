/*
 * File:    QueryNormalisation.cs
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: The query-string normalisation every reservation read shares — the
 *          ?status= filter, a date bound, and the page/pageSize clamp.
 *
 *          All three were private to ReservationService. The booking-history
 *          endpoint applies exactly the same three transformations, so
 *          they moved here rather than being written a second time. Two copies
 *          would drift invisibly: one route would accept a status casing or a
 *          page size the other refused, and the same request would behave
 *          differently depending on which route it took.
 *
 *          Nothing here touches the database or the caller's identity.
 */
using SmartSolar.Api.Models;
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Helpers;

public static class QueryNormalisation
{
    // Used when the caller asks for no particular page size.
    public const int DefaultPageSize = 20;

    // The ceiling on pageSize. One request can never pull the collection.
    public const int MaxPageSize = 100;

    // Blank -> no filter; otherwise must be one of the five statuses (any
    // casing), returned in its stored casing. Unknown -> 400.
    public static string? Status(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return ReservationStatuses.All
            .FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw ReservationErrors.InvalidStatusFilter(status);
    }

    // The contract sends UTC ("...Z"). A value with no offset is taken to be
    // UTC as well, rather than being shifted as if it were server-local.
    public static DateTime? AsUtc(DateTime? value)
    {
        if (value is null) return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    // Page numbers below 1 and sizes outside 1..MaxPageSize are clamped rather
    // than rejected, so a client that asks for page 0 still gets its first
    // page instead of a 400 it did not need.
    public static void Page(int? page, int? pageSize, out int effectivePage, out int effectiveSize)
    {
        effectivePage = Math.Max(page ?? 1, 1);
        effectiveSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
    }
}
