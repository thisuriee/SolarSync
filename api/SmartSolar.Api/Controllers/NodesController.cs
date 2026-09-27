/*
 * File:    NodesController.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: HTTP entry points for microgrid node management
 *          (docs/api-contract.md §3). Thin by design — it authorises, binds
 *          the request and hands over to INodeService. Every rule lives in
 *          the service, and failures propagate as BusinessRuleException for
 *          ExceptionHandlingMiddleware to format. No action in this file
 *          returns Conflict(...) or BadRequest(...) by hand.
 *
 *          Reads are open to any authenticated role because a prosumer has to
 *          see nodes in order to book one; the service narrows what they get
 *          to active nodes. Writes are Backoffice only: registering nodes and
 *          maintaining their schedules is the back office's responsibility,
 *          while operators work at the booking-window level.
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Controllers;

[ApiController]
[Route("api/nodes")]
[Authorize]
public class NodesController : ControllerBase
{
    private readonly INodeService _nodeService;

    public NodesController(INodeService nodeService)
    {
        _nodeService = nodeService;
    }

    // GET /api/nodes?status=&q= — lists microgrid nodes.
    // The caller's role is passed to the service, which applies the rule that
    // a prosumer only ever sees active nodes.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? q)
    {
        return Ok(await _nodeService.FindAll(status, q, User.GetRole()));
    }

    // GET /api/nodes/{id} — one node with its booking-window summary and the
    // count of active reservations that currently block deactivation.
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        return Ok(await _nodeService.FindById(id, User.GetRole()));
    }

    // POST /api/nodes — registers a node. Coordinate and capacity rules, and
    // the GeoJSON mapping, are enforced in NodeService. The owning user id is
    // read from the token, never from the body.
    [HttpPost]
    [Authorize(Roles = UserRoles.Backoffice)]
    public async Task<IActionResult> Create([FromBody] NodeCreateRequest request)
    {
        var created = await _nodeService.Create(request, User.GetUserId());

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /api/nodes/{id} — full update of a node's editable fields. The
    // battery-count guard (NODE_CAPACITY_CONFLICT) is enforced in the service.
    [HttpPut("{id}")]
    [Authorize(Roles = UserRoles.Backoffice)]
    public async Task<IActionResult> Update(string id, [FromBody] NodeUpdateRequest request)
    {
        return Ok(await _nodeService.Update(id, request));
    }

    // PATCH /api/nodes/{id}/deactivate — takes a node out of service.
    // Refused with 409 while active reservations reference it; that check and
    // its message are produced by NodeService, not here.
    //
    // No request body: the route names the transition, so there is nothing for
    // a caller to supply and nothing to validate. The Android client cannot
    // send PATCH over HttpURLConnection and reaches this action by posting
    // with the override header, which the middleware rewrites before routing.
    [HttpPatch("{id}/deactivate")]
    [Authorize(Roles = UserRoles.Backoffice)]
    public async Task<IActionResult> Deactivate(string id)
    {
        return Ok(await _nodeService.Deactivate(id));
    }

    // PATCH /api/nodes/{id}/activate — returns a node to service.
    [HttpPatch("{id}/activate")]
    [Authorize(Roles = UserRoles.Backoffice)]
    public async Task<IActionResult> Activate(string id)
    {
        return Ok(await _nodeService.Activate(id));
    }
}
