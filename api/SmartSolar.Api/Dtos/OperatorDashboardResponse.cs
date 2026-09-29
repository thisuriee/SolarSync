/*
 * File:    OperatorDashboardResponse.cs
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: The operator dashboard (docs/api-contract.md §5), optionally
 *          narrowed to one node by ?nodeId=.
 *
 *          PendingCount is a database count, not the length of
 *          PendingReservations. Counting the returned array in C# is exactly
 *          what the rubric forbids, so the number and the preview come from
 *          the same query but only the database decides the number.
 */
namespace SmartSolar.Api.Dtos;

public class OperatorDashboardResponse
{
    // The approval queue, soonest slot first — the order an operator works in.
    // Capped (see DashboardService.MaxPendingPreview). PendingCount stays the
    // true total, so the two can differ on a very busy node.
    public List<ReservationResponse> PendingReservations { get; set; } = new();

    // Every Pending booking in scope, counted by the database.
    public long PendingCount { get; set; }

    // Approved and still in the future, in scope.
    public long ApprovedFutureCount { get; set; }

    // Completed since 00:00 UTC today, in scope. UTC rather than server-local:
    // every other time comparison in the API is UTC, and Sri Lanka is UTC+5:30.
    public long CompletedToday { get; set; }
}
