/*
 * File:    DashboardController.cs
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: HTTP entry points for the two dashboards and the booking history
 *          (docs/api-contract.md §5). Thin by design — it authorises by role,
 *          binds the query string, builds the caller from the JWT and hands
 *          over to IDashboardService. Every rule lives in the service and
 *          fails as BusinessRuleException for ExceptionHandlingMiddleware to
 *          format. No action returns Forbid(...) or BadRequest(...) by hand.
 *
 *          Route note: "reservations/history" is a literal segment, so ASP.NET
 *          Core routing ranks it above ReservationsController's "{id}"
 *          regardless of controller or declaration order, and the two do not
 *          overlap. "reservations/{id}/qr" and "reservations/{id}/complete"
 *          live the same way in QrController.
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    // GET /api/dashboard/prosumer — the caller's own counts. There is no nic
    // parameter: the NIC is read from the token, in the service.
    [HttpGet("dashboard/prosumer")]
    [Authorize(Roles = UserRoles.Prosumer)]
    public async Task<IActionResult> ProsumerDashboard()
    {
        return Ok(await _dashboardService.ProsumerDashboard(User.GetCaller()));
    }

    // GET /api/dashboard/operator?nodeId= — pendingReservations, pendingCount,
    // approvedFutureCount and completedToday, optionally narrowed to one node.
    [HttpGet("dashboard/operator")]
    [Authorize(Roles = $"{UserRoles.GridOperator},{UserRoles.Backoffice}")]
    public async Task<IActionResult> OperatorDashboard([FromQuery] string? nodeId)
    {
        return Ok(await _dashboardService.OperatorDashboard(nodeId, User.GetCaller()));
    }

    // GET /api/reservations/history — any authenticated role. A prosumer is
    // scoped to the token's NIC in the service; staff may filter by any NIC.
    [HttpGet("reservations/history")]
    public async Task<IActionResult> History([FromQuery] ReservationHistoryQuery query)
    {
        return Ok(await _dashboardService.FindHistory(query, User.GetCaller()));
    }
}
