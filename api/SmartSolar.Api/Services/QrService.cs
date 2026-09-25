// -----------------------------------------------------------------------------
// File Name:        QrService.cs
// Author:           Imadh
// Date Created:     25 September 2026
// Purpose:          Implementation of the QR fulfilment vertical — issues a
//                   single-use QR token for an approved reservation.
// -----------------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Exceptions;
using SmartSolar.Api.Repositories;
using SmartSolar.Api.Settings;

namespace SmartSolar.Api.Services;

public class QrService : IQrService
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IOptions<QrSettings> _qrSettings;

    // verificationId -> (reservationId, expiry). Populated by VerifyQrToken and
    // read by CompleteReservation in a later commit; unused for now.
    private readonly ConcurrentDictionary<string, (string ReservationId, DateTime ExpiresAt)> _verificationStore = new();

    public QrService(IReservationRepository reservationRepository, IOptions<QrSettings> qrSettings)
    {
        _reservationRepository = reservationRepository;
        _qrSettings = qrSettings;
    }

    // Issues a single-use QR token for an approved reservation owned by the
    // caller. Only SHA-256(token) is persisted; the plaintext is never stored.
    public async Task<QrTokenResult> IssueQrForReservation(string reservationId, string prosumerNic)
    {
        var reservation = await _reservationRepository.FindReservationById(reservationId);

        if (reservation is null)
        {
            throw new BusinessRuleException(
                "RESERVATION_NOT_FOUND",
                "Reservation not found",
                "The reservation you are trying to access does not exist.",
                404);
        }

        if (reservation.Nic != prosumerNic)
        {
            throw new BusinessRuleException(
                "RESERVATION_ACCESS_DENIED",
                "Access denied",
                "You can only issue QR codes for your own reservations.",
                403);
        }

        if (reservation.Status != "Approved")
        {
            throw new BusinessRuleException(
                "QR_NOT_APPROVED",
                "QR not available",
                "A QR code can only be issued for an approved reservation.",
                409);
        }

        string plaintextToken = GenerateSecureToken();
        string tokenHash = HashToken(plaintextToken);

        DateTime utcNow = DateTime.UtcNow;
        DateTime qrExpiresAt = reservation.SlotEnd.AddHours(_qrSettings.Value.TokenExpiryHours);

        await _reservationRepository.UpdateQrFields(reservationId, tokenHash, utcNow, qrExpiresAt);

        return new QrTokenResult { Token = plaintextToken, ExpiresAt = qrExpiresAt };
    }

    // Verifies a scanned token and returns the booking details. Next commit.
    public Task<QrVerificationResult> VerifyQrToken(string token, string operatorId)
        => throw new NotImplementedException();

    // Finalises the energy transfer. Next commit.
    public Task CompleteReservation(string reservationId, string verificationId, string operatorId)
        => throw new NotImplementedException();

    // Generates a 32-byte cryptographically random token and encodes it as a
    // URL-safe string, so it can be embedded in a QR payload without escaping.
    private string GenerateSecureToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    // Returns the Base64Url-encoded SHA-256 hash of a token. Only this hash is
    // persisted as qrTokenHash; the plaintext token is never stored.
    private string HashToken(string plaintextToken)
    {
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(plaintextToken));
            return WebEncoders.Base64UrlEncode(hash);
        }
    }
}
