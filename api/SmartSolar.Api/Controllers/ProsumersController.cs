/*
 * File:    ProsumersController.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: HTTP entry points for prosumer management and self-service
 *          (docs/api-contract.md §2). Thin by design: the role gate is the
 *          [Authorize] attribute; ownership, the Backoffice-only re-check and
 *          the active-reservation block are in IUserService. The caller's
 *          identity always comes from the token (User.GetCaller()), never the body.
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Controllers;

[ApiController]
[Route("api/prosumers")]
[Authorize]
public class ProsumersController : ControllerBase
{
    private readonly IUserService _userService;

    public ProsumersController(IUserService userService)
    {
        _userService = userService;
    }

    // GET /api/prosumers?status=&q=&page=&pageSize= — paged list for staff.
    [HttpGet]
    [Authorize(Roles = UserRoles.Backoffice + "," + UserRoles.GridOperator)]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? q,
        [FromQuery] int? page, [FromQuery] int? pageSize)
    {
        return Ok(await _userService.GetProsumers(status, q, page, pageSize));
    }

    // GET /api/prosumers/pending — the pending-activations queue. Backoffice only.
    // The literal "pending" segment takes precedence over the {nic} route.
    [HttpGet("pending")]
    [Authorize(Roles = UserRoles.Backoffice)]
    public async Task<IActionResult> GetPending()
    {
        return Ok(await _userService.GetPendingProsumers());
    }

    // GET /api/prosumers/{nic} — any role; a prosumer only their own (403 otherwise).
    [HttpGet("{nic}")]
    public async Task<IActionResult> Get(string nic)
    {
        return Ok(await _userService.GetProsumer(User.GetCaller(), nic));
    }

    // PUT /api/prosumers/{nic} — edits contact fields; a prosumer only their own.
    [HttpPut("{nic}")]
    public async Task<IActionResult> Update(string nic, [FromBody] UpdateProsumerRequest request)
    {
        return Ok(await _userService.UpdateProsumer(User.GetCaller(), nic, request));
    }

    // PATCH /api/prosumers/{nic}/request-deactivation — prosumer self-service,
    // own account only; blocked while active reservations exist.
    [HttpPatch("{nic}/request-deactivation")]
    [Authorize(Roles = UserRoles.Prosumer)]
    public async Task<IActionResult> RequestDeactivation(string nic)
    {
        return Ok(await _userService.RequestDeactivation(User.GetCaller(), nic));
    }

    // PATCH /api/prosumers/{nic}/activate — Backoffice ONLY. A Grid Operator is
    // stopped here by the attribute (403 AUTH_FORBIDDEN_ROLE) and again inside
    // UserService.Activate.
    [HttpPatch("{nic}/activate")]
    [Authorize(Roles = UserRoles.Backoffice)]
    public async Task<IActionResult> Activate(string nic)
    {
        return Ok(await _userService.Activate(User.GetCaller(), nic));
    }

    // PATCH /api/prosumers/{nic}/deactivate — Backoffice only, optional {reason};
    // blocked while active reservations exist. The body may be omitted entirely.
    [HttpPatch("{nic}/deactivate")]
    [Authorize(Roles = UserRoles.Backoffice)]
    public async Task<IActionResult> Deactivate(string nic,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] DeactivateProsumerRequest? request)
    {
        return Ok(await _userService.Deactivate(User.GetCaller(), nic, request?.Reason));
    }
}
