/*
 * File:    AuthController.cs
 * Author:  Dahami
 * Created: 2026-09-26
 * Purpose: HTTP entry points for authentication (docs/api-contract.md §2).
 *          Thin by design — every rule lives in IAuthService; failures
 *          propagate as BusinessRuleException for ExceptionHandlingMiddleware.
 *          login and register-prosumer are two of the three anonymous routes
 *          in the system (docs/auth.md).
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // POST /api/auth/login — one login for all three roles. Rules (credential
    // check, Pending/Deactivated block) are enforced in AuthService.Login.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        return Ok(await _authService.Login(request));
    }

    // POST /api/auth/register-prosumer — prosumer self-registration.
    // Rules (NIC format/uniqueness, role and status forced) in AuthService.
    // Location points at the prosumer profile route.
    [HttpPost("register-prosumer")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterProsumer([FromBody] RegisterProsumerRequest request)
    {
        var result = await _authService.RegisterProsumer(request);
        return Created($"/api/prosumers/{result.Nic}", result);
    }

    // GET /api/auth/me — restores a session from the token alone. Any role.
    // The user id is read from the token's sub claim, never from the request.
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        return Ok(await _authService.GetCurrentUser(User.GetUserId()));
    }
}
