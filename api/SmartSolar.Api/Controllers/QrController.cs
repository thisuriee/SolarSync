/*
 * File:    QrController.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: HTTP entry points for the QR fulfilment vertical. Thin by design —
 *          every rule lives in IQrService, and failures propagate as
 *          BusinessRuleException for ExceptionHandlingMiddleware to format.
 *          No action returns Conflict(...) or BadRequest(...) by hand.
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Services;

namespace SmartSolar.Api.Controllers;

[ApiController]
[Route("api")]
public class QrController : ControllerBase
{
    private readonly IQrService _qrService;

    public QrController(IQrService qrService)
    {
        _qrService = qrService;
    }

    // GET /api/reservations/{id}/qr — issues a QR token for an approved
    // reservation. Rule: only the owner can issue; reservation must be Approved.
    [HttpGet("reservations/{id}/qr")]
    [Authorize(Roles = "Prosumer")]
    public async Task<IActionResult> IssueQr(string id)
    {
        return Ok(await _qrService.IssueQrForReservation(id, CallerNic()));
    }

    // POST /api/fulfilment/verify-qr — verifies a scanned QR token.
    // Rule: only GridOperator; server decides validity.
    [HttpPost("fulfilment/verify-qr")]
    [Authorize(Roles = "GridOperator")]
    public async Task<IActionResult> VerifyQr([FromBody] VerifyQrRequest request)
    {
        return Ok(await _qrService.VerifyQrToken(request.Token, OperatorId()));
    }

    // PATCH /api/reservations/{id}/complete — finalises the transfer.
    // Rule: requires a valid verificationId from a prior scan.
    [HttpPatch("reservations/{id}/complete")]
    [Authorize(Roles = "GridOperator")]
    public async Task<IActionResult> Complete(string id, [FromBody] CompleteReservationRequest request)
    {
        await _qrService.CompleteReservation(id, request.VerificationId, OperatorId());

        // The service returns nothing, so there is no updated resource to send.
        return NoContent();
    }

    // Reads the caller's NIC from the token claims, never from the body. Exact
    // "nic" first; the case-insensitive scan is a fallback for a token shaped
    // differently, so a missing claim still fails closed.
    private string CallerNic()
    {
        var claim = User.FindFirst("nic")
            ?? User.Claims.FirstOrDefault(c =>
                c.Type.Contains("nic", StringComparison.OrdinalIgnoreCase));

        if (claim is null || string.IsNullOrWhiteSpace(claim.Value))
        {
            throw new BusinessRuleException(
                "AUTH_NIC_MISSING",
                "Authentication error",
                "Your session is missing the NIC claim.",
                401);
        }

        return claim.Value;
    }

    // Reads the operator's Mongo _id from the token claims. Program.cs sets
    // MapInboundClaims = false, so the claim stays "sub" instead of being
    // remapped to ClaimTypes.NameIdentifier.
    private string OperatorId()
    {
        var claim = User.FindFirst("sub")
            ?? User.FindFirst("nameid")
            ?? User.Claims.FirstOrDefault(c =>
                c.Type.Contains("nameid", StringComparison.OrdinalIgnoreCase)
                || c.Type.Contains("sub", StringComparison.OrdinalIgnoreCase));

        if (claim is null || string.IsNullOrWhiteSpace(claim.Value))
        {
            throw new BusinessRuleException(
                "AUTH_OPERATOR_ID_MISSING",
                "Authentication error",
                "Your session is missing the operator ID claim.",
                401);
        }

        return claim.Value;
    }
}
