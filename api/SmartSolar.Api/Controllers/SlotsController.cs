/*
 * File:    SlotsController.cs
 * Author:  Aman
 * Created: 2026-09-27
 * Purpose: HTTP entry points for booking-window management
 *          (docs/api-contract.md §3). Thin by design — it authorises, binds
 *          the request and hands over to ISlotService. Every rule lives in
 *          the service, and failures propagate as BusinessRuleException for
 *          ExceptionHandlingMiddleware to format.
 *
 *          The routes sit on two different prefixes — windows are created
 *          under their node, then addressed directly once they exist — so the
 *          controller takes the bare "api" route and spells each path out,
 *          the same pattern the fulfilment controller uses.
 *
 *          Who may do what follows the project scenario: opening and adjusting
 *          availability is an operator's day-to-day job, so Grid Operators
 *          share it with the Backoffice. Destroying a window that bookings may
 *          point at is administrative, so deletion is Backoffice alone.
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Models;
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class SlotsController : ControllerBase
{
    private readonly ISlotService _slotService;

    public SlotsController(ISlotService slotService)
    {
        _slotService = slotService;
    }

    // GET /api/nodes/{nodeId}/slots?from=&to= — every booking window on a
    // node, whatever its status. Open to any authenticated role: staff manage
    // them here and a prosumer may want to see a node's whole schedule, not
    // only what is free.
    [HttpGet("nodes/{nodeId}/slots")]
    public async Task<IActionResult> GetByNode(string nodeId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        return Ok(await _slotService.FindByNode(nodeId, from, to));
    }

    // POST /api/nodes/{nodeId}/slots — opens a booking window on a node.
    // Overlap, capacity and window-length rules are enforced in SlotService.
    [HttpPost("nodes/{nodeId}/slots")]
    [Authorize(Roles = $"{UserRoles.Backoffice},{UserRoles.GridOperator}")]
    public async Task<IActionResult> Create(string nodeId, [FromBody] SlotCreateRequest request)
    {
        var created = await _slotService.Create(nodeId, request);

        return CreatedAtAction(nameof(GetByNode), new { nodeId }, created);
    }

    // PUT /api/slots/{id} — full update of a window's editable fields.
    // Its reserved count and status are not among them.
    [HttpPut("slots/{id}")]
    [Authorize(Roles = $"{UserRoles.Backoffice},{UserRoles.GridOperator}")]
    public async Task<IActionResult> Update(string id, [FromBody] SlotUpdateRequest request)
    {
        return Ok(await _slotService.Update(id, request));
    }

    // DELETE /api/slots/{id} — removes a window. Refused while reservations
    // reference it. Returns 204 with no body: there is nothing left to send.
    [HttpDelete("slots/{id}")]
    [Authorize(Roles = UserRoles.Backoffice)]
    public async Task<IActionResult> Delete(string id)
    {
        await _slotService.Delete(id);

        return NoContent();
    }

    // GET /api/slots/available?nodeId=&date= — windows that can actually be
    // booked right now. This is the prosumer's booking list, so it is open to
    // any authenticated role; the filtering that makes it truthful happens in
    // the service, not in the app asking.
    [HttpGet("slots/available")]
    public async Task<IActionResult> GetAvailable([FromQuery] string? nodeId, [FromQuery] DateTime? date)
    {
        return Ok(await _slotService.FindBookable(nodeId, date));
    }
}
