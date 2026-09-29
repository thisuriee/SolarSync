/*
 * File:    StationStatuses.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: The exact status strings for SolarStationInfo and
 *          EnergyBookingSlots from docs/db-schema.md §2 and §3, written once.
 *          They are const so they can be used in [Authorize]-style attributes
 *          and in switch arms, and so a typo becomes a compile error instead
 *          of a filter that silently matches nothing. Mirrors UserRoles.cs,
 *          which holds the user role and status strings the same way.
 */
namespace SmartSolar.Api.Models;

public static class StationStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    // The complete set, for validating a ?status= query parameter.
    public static readonly string[] All = [Active, Inactive];
}

public static class SlotStatuses
{
    public const string Open = "Open";
    public const string Full = "Full";
    public const string Closed = "Closed";

    public static readonly string[] All = [Open, Full, Closed];
}

public static class ReservationStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";

    // The two statuses that make a reservation "active" for the purpose of the
    // shared predicate in docs/api-contract.md §6. Paired with slotStart > now.
    public static readonly string[] Active = [Pending, Approved];

    // The complete set, for validating a ?status= query parameter. Mirrors the
    // All array on StationStatuses and SlotStatuses above, so every status
    // group in the schema is enumerable the same way.
    public static readonly string[] All = [Pending, Approved, Rejected, Cancelled, Completed];
}
