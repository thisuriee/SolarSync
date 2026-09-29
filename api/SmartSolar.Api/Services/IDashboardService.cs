/*
 * File:    IDashboardService.cs
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: The read models behind docs/api-contract.md §5 — the two dashboards
 *          and the booking history.
 *
 *          Every method takes the CallerIdentity built from the JWT, because
 *          both the role and the prosumer scoping rule are re-checked in the
 *          service, not only by the controller's [Authorize] attribute.
 */
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;

namespace SmartSolar.Api.Services;

public interface IDashboardService
{
    // GET /dashboard/prosumer — Prosumer only; every count is the caller's own,
    // computed by the database from the token's NIC.
    Task<ProsumerDashboardResponse> ProsumerDashboard(CallerIdentity caller);

    // GET /dashboard/operator — GridOperator and Backoffice. nodeId narrows
    // every figure to one station; absent means the whole grid.
    Task<OperatorDashboardResponse> OperatorDashboard(string? nodeId, CallerIdentity caller);

    // GET /reservations/history — any role. A prosumer is scoped to the token
    // NIC regardless of the nic parameter, and filtering and paging both happen
    // in the Mongo query.
    Task<PagedResult<ReservationResponse>> FindHistory(ReservationHistoryQuery query, CallerIdentity caller);
}
