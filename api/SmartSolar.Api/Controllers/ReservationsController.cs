/*
 * File:    ReservationsController.cs
 * Author:  Thisuri
 * Created: 2026-09-27
 * Purpose: HTTP entry points for the reservation workflow
 *          (docs/api-contract.md §4). Thin by design — it authorises by role,
 *          binds the request, builds the caller from the JWT and hands over
 *          to IReservationService. Every rule (R1, R2, ownership, transitions,
 *          capacity) lives in the service and fails as BusinessRuleException
 *          for ExceptionHandlingMiddleware to format. No action returns
 *          Conflict(...) or BadRequest(...) by hand.
 *
 *          Routes: "mine" is a literal segment, so ASP.NET Core routing ranks
 *          it above "{id}" regardless of declaration order. M4's
 *          /reservations/{id}/qr, /{id}/complete and /history live in their
 *          own controller and do not overlap with anything here.
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationsController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    // POST /api/reservations — Prosumer books for themselves (NIC from token);
    // Backoffice books on a prosumer's behalf (prosumerNIC in body). R1,
    // capacity and duplicate rules are enforced in the service.
    [HttpPost]
    [Authorize(Roles = $"{UserRoles.Prosumer},{UserRoles.Backoffice}")]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest request)
    {
        var created = await _reservationService.Create(request, User.GetCaller());

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // GET /api/reservations/mine?status= — the caller's own bookings. There is
    // no nic parameter: the NIC is read from the token in the service.
    [HttpGet("mine")]
    [Authorize(Roles = UserRoles.Prosumer)]
    public async Task<IActionResult> GetMine([FromQuery] string? status)
    {
        return Ok(await _reservationService.FindMine(status, User.GetCaller()));
    }

    // GET /api/reservations?status=&nodeId=&nic=&from=&to=&page=&pageSize= —
    // staff search, returned in the paging envelope.
    [HttpGet]
    [Authorize(Roles = $"{UserRoles.Backoffice},{UserRoles.GridOperator}")]
    public async Task<IActionResult> Search(
        [FromQuery] string? status, [FromQuery] string? nodeId, [FromQuery] string? nic,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int? page, [FromQuery] int? pageSize)
    {
        return Ok(await _reservationService.Search(status, nodeId, nic, from, to, page, pageSize, User.GetCaller()));
    }

    // GET /api/reservations/{id} — any role; a prosumer only their own (403 otherwise).
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        return Ok(await _reservationService.FindById(id, User.GetCaller()));
    }

    // PUT /api/reservations/{id} — change slot and/or energy. R2 (12 h) on the
    // current booking, R1 (7 days) + capacity on the new slot.
    [HttpPut("{id}")]
    [Authorize(Roles = $"{UserRoles.Prosumer},{UserRoles.Backoffice}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateReservationRequest request)
    {
        return Ok(await _reservationService.Update(id, request, User.GetCaller()));
    }

    // PATCH /api/reservations/{id}/cancel — R2 (12 h), then capacity released.
    // Prosumer (own), GridOperator (operator-assisted), Backoffice.
    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id)
    {
        return Ok(await _reservationService.Cancel(id, User.GetCaller()));
    }

    // PATCH /api/reservations/{id}/approve — GridOperator only, from Pending.
    [HttpPatch("{id}/approve")]
    [Authorize(Roles = UserRoles.GridOperator)]
    public async Task<IActionResult> Approve(string id)
    {
        return Ok(await _reservationService.Approve(id, User.GetCaller()));
    }

    // PATCH /api/reservations/{id}/reject {reason} — GridOperator only, from
    // Pending; releases the slot space.
    [HttpPatch("{id}/reject")]
    [Authorize(Roles = UserRoles.GridOperator)]
    public async Task<IActionResult> Reject(string id, [FromBody] RejectReservationRequest request)
    {
        return Ok(await _reservationService.Reject(id, request.Reason, User.GetCaller()));
    }
}
