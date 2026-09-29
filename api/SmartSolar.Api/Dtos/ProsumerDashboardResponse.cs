/*
 * File:    ProsumerDashboardResponse.cs
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: The prosumer dashboard (docs/api-contract.md §5). Every count is
 *          computed by the database from the token's NIC — the client never
 *          counts a list it fetched and never hardcodes a number, which is the
 *          "Poor" band of the rubric.
 *
 *          All three counts and nextBooking are read against one captured
 *          instant, so none of them can disagree about what "upcoming" means
 *          because a second ticked over between the queries.
 */
namespace SmartSolar.Api.Dtos;

public class ProsumerDashboardResponse
{
    // Pending or Approved, and still in the future — the shared active
    // predicate from docs/api-contract.md §6, not a second definition of it.
    public long ActiveCount { get; set; }

    // Every Pending booking this prosumer holds, whenever it is for. A Pending
    // booking whose slot has passed still needs an operator decision, so it
    // stays visible here; it is deliberately NOT part of ActiveCount.
    public long PendingCount { get; set; }

    // Approved and still in the future — the count the rubric names explicitly.
    public long ApprovedFutureCount { get; set; }

    // The soonest upcoming Approved booking, or null when there is none.
    // Reuses the reservation projection, so the client parses one shape.
    public ReservationResponse? NextBooking { get; set; }
}
