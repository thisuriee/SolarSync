/*
 * File:    DashboardService.cs
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: The reads behind the two dashboards and the booking history
 *          (docs/api-contract.md §5).
 *
 *          Every count in here is executed by MongoDB. Nothing is counted in
 *          C# from a fetched list: a count taken from a page of results is the
 *          count of that page, not of the data, and the rubric names a
 *          hard-coded or client-counted number as the failure mode. This class
 *          assembles DTOs; the database decides the numbers.
 *
 *          activeCount is not computed here at all. It asks
 *          IReservationQueries for the shared active-reservation predicate, so
 *          "active" cannot come to mean two different things in one system
 *          (docs/api-contract.md §6).
 *
 *          Throws BusinessRuleException; ExceptionHandlingMiddleware formats
 *          it. Every time comparison uses DateTime.UtcNow — slot times are
 *          stored in UTC and Sri Lanka is UTC+5:30, so a local clock would be
 *          wrong by five and a half hours.
 */
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Repositories;

namespace SmartSolar.Api.Services;

public class DashboardService : IDashboardService
{
    // The most pending rows the operator dashboard returns in one response.
    // PendingCount stays the database's true total, so a node with a longer
    // queue reports the real number and sends the first page of it. Shared with
    // the paged routes, so no read in the API can return an unbounded set.
    public const int MaxPendingPreview = QueryNormalisation.MaxPageSize;

    private readonly IReservationRepository _reservations;
    private readonly IStationRepository _stations;
    private readonly IReservationQueries _reservationQueries;

    public DashboardService(
        IReservationRepository reservations,
        IStationRepository stations,
        IReservationQueries reservationQueries)
    {
        _reservations = reservations;
        _stations = stations;
        _reservationQueries = reservationQueries;
    }

    // GET /dashboard/prosumer — the caller's own bookings only.
    public async Task<ProsumerDashboardResponse> ProsumerDashboard(CallerIdentity caller)
    {
        CallerGuard.RequireRole(caller, "Only prosumers have a prosumer dashboard.", UserRoles.Prosumer);

        // Normalised once, then used for all four reads below, so no two of
        // them can look for the same prosumer under a different spelling.
        var nic = IdentityFormat.NormaliseNic(CallerGuard.RequireNic(caller));

        // One instant for the whole response. Captured here rather than inside
        // each query, so a second ticking over mid-request cannot leave two
        // counts disagreeing about which bookings are still upcoming.
        var utcNow = DateTime.UtcNow;

        // The shared active predicate, not a second definition of it.
        var activeCount = await _reservationQueries.CountActiveForProsumer(nic);

        // Every Pending booking, whenever it is for. A booking awaiting an
        // operator decision is still pending after its slot passes, so it is
        // counted here and deliberately not in activeCount.
        var pendingCount = await _reservations.CountInStatuses([ReservationStatuses.Pending], nic: nic);

        var approvedFutureCount = await _reservations.CountInStatusesStartingAfter(
            [ReservationStatuses.Approved], utcNow, nic: nic);

        // The database picks the soonest, rather than the service sorting a list.
        var nextBooking = await _reservations.FindNextBookingByNic(nic, utcNow);

        return new ProsumerDashboardResponse
        {
            ActiveCount = activeCount,
            PendingCount = pendingCount,
            ApprovedFutureCount = approvedFutureCount,
            NextBooking = nextBooking is null ? null : ReservationResponse.FromReservation(nextBooking)
        };
    }

    // GET /dashboard/operator — the approval queue and its counts, for one node
    // or for the whole grid.
    public async Task<OperatorDashboardResponse> OperatorDashboard(string? nodeId, CallerIdentity caller)
    {
        CallerGuard.RequireRole(caller, "Only Grid Operators and Backoffice staff have an operator dashboard.",
            UserRoles.GridOperator, UserRoles.Backoffice);

        var stationId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId.Trim();
        var utcNow = DateTime.UtcNow;

        // One call yields both the preview and the total: the count comes back
        // from the database alongside the page, so neither is counted in C#.
        var (pending, pendingCount) = await _reservations.FindPending(stationId, MaxPendingPreview);

        var approvedFutureCount = await _reservations.CountInStatusesStartingAfter(
            [ReservationStatuses.Approved], utcNow, stationId);

        // utcNow.Date is UTC midnight: utcNow is UTC-kind, so this is 00:00
        // UTC today and not a local midnight shifted onto the data.
        var completedToday = await _reservations.CountCompletedSince(utcNow.Date, stationId);

        return new OperatorDashboardResponse
        {
            PendingReservations = pending.Select(ReservationResponse.FromReservation).ToList(),
            PendingCount = pendingCount,
            ApprovedFutureCount = approvedFutureCount,
            CompletedToday = completedToday
        };
    }

    // GET /reservations/history — any role. A prosumer sees their own bookings
    // whatever the nic parameter says.
    public async Task<PagedResult<ReservationResponse>> FindHistory(
        ReservationHistoryQuery query, CallerIdentity caller)
    {
        var scopedNic = ResolveHistoryNic(query.Nic, caller);

        QueryNormalisation.Page(query.Page, query.PageSize, out var page, out var pageSize);

        var status = QueryNormalisation.Status(query.Status);

        // Station names live in SolarStationInfo, so a free-text term that
        // matches a station name is resolved to ids first — through the
        // database, never by filtering a fetched list. Every status is
        // searched: a booking at a node deactivated since is still history.
        var stationIds = await ResolveStationIds(query.Q);

        var (items, total) = await _reservations.FindPaged(
            status,
            string.IsNullOrWhiteSpace(query.NodeId) ? null : query.NodeId.Trim(),
            scopedNic,
            QueryNormalisation.AsUtc(query.From),
            QueryNormalisation.AsUtc(query.To),
            (page - 1) * pageSize,
            pageSize,
            query.Q,
            stationIds);

        return PagedResult<ReservationResponse>.Create(
            items.Select(ReservationResponse.FromReservation).ToList(), page, pageSize, total);
    }

    // Rule (docs/api-contract.md §5): a prosumer is scoped to the token's NIC
    // regardless of the nic parameter. A foreign NIC is ignored rather than
    // refused, so a client cannot probe for other accounts and a stale link
    // still shows the caller their own history instead of failing.
    private static string? ResolveHistoryNic(string? requestedNic, CallerIdentity caller)
    {
        if (caller.Role == UserRoles.Prosumer)
        {
            return IdentityFormat.NormaliseNic(CallerGuard.RequireNic(caller));
        }

        return string.IsNullOrWhiteSpace(requestedNic) ? null : IdentityFormat.NormaliseNic(requestedNic);
    }

    // The ids of the stations whose name or city matches the term, or null when
    // there is no term. A term matching no station gives an empty list, which
    // the repository reads as "no station matched" rather than "any station".
    private async Task<IReadOnlyCollection<string>?> ResolveStationIds(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var stations = await _stations.FindAll(null, search);

        return stations.Select(s => s.Id).ToList();
    }
}
