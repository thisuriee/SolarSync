// -----------------------------------------------------------------------------
// File Name:        IQrService.cs
// Author:           Imadh
// Date Created:     25 September 2026
// Purpose:          Defines the business rules for QR token issuance,
//                   verification and completion in the Smart Solar Microgrid
//                   Trading System.
// -----------------------------------------------------------------------------
using SmartSolar.Api.Dtos;

namespace SmartSolar.Api.Services;

public interface IQrService
{
    // Issues a single-use QR token for an approved reservation so the prosumer can
    // present it at the node.
    // Rules: reservation must be Approved; caller's NIC must match the reservation's
    // prosumerNIC; only SHA-256(token) is stored, never the plaintext. Expiry is
    // slotEnd + QrSettings.TokenExpiryHours.
    // Returns the plaintext token to render as the QR, plus its UTC expiry.
    Task<QrTokenResult> IssueQrForReservation(string reservationId, string prosumerNic);

    // Verifies a scanned token and issues the short-lived verificationId that is the
    // only thing authorising CompleteReservation.
    // Rules: hash must match a stored qrTokenHash (QR_INVALID); must be unexpired
    // (QR_EXPIRED); reservation must still be Approved (QR_ALREADY_USED). The
    // verifying operator is recorded. verificationId lasts
    // QrSettings.VerificationIdExpiryMinutes.
    // Returns the reservation, prosumer and station details the operator confirms.
    Task<QrVerificationResult> VerifyQrToken(string token, string operatorId);

    // Finalises the energy transfer: status goes to Completed and the verifying
    // operator is recorded with the completion timestamp.
    // Rules: verificationId must be valid and unexpired — this is what stops a
    // completion without a real scan — and the reservation must still be Approved.
    // Single-use, because any repeat call fails the status check.
    Task CompleteReservation(string reservationId, string verificationId, string operatorId);
}
