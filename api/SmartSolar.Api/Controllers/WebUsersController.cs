/*
 * File:    WebUsersController.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: HTTP entry points for web-user management (docs/api-contract.md §2).
 *          Backoffice only, enforced once at class level. Thin by design —
 *          every rule (web roles only, last active Backoffice) is in IUserService.
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Models;
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Controllers;

[ApiController]
[Route("api/webusers")]
[Authorize(Roles = UserRoles.Backoffice)]
public class WebUsersController : ControllerBase
{
    private readonly IUserService _userService;

    public WebUsersController(IUserService userService)
    {
        _userService = userService;
    }

    // GET /api/webusers?role=&status= — Backoffice/GridOperator accounts only.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? role, [FromQuery] string? status)
    {
        return Ok(await _userService.GetWebUsers(role, status));
    }

    // POST /api/webusers — creates an Active web user; Prosumer role rejected
    // in UserService.CreateWebUser. 201 + Location of the new resource.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWebUserRequest request)
    {
        var created = await _userService.CreateWebUser(request);
        return Created($"/api/webusers/{created.Id}", created);
    }

    // PUT /api/webusers/{id} — full update; last-active-Backoffice guard in service.
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateWebUserRequest request)
    {
        return Ok(await _userService.UpdateWebUser(id, request));
    }

    // PATCH /api/webusers/{id}/status — {status}; same last-Backoffice guard.
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateStatusRequest request)
    {
        return Ok(await _userService.UpdateWebUserStatus(id, request));
    }
}
