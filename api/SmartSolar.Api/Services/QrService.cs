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
using SmartSolar.Api.Configuration;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Repositories;

namespace SmartSolar.Api.Services;

public class QrService : IQrService
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStationRepository _stationRepository;
    private readonly IOptions<QrSettings> _qrSettings;

    // verificationId -> (reservationId, expiry). Populated by VerifyQrToken and
    // read by CompleteReservation in a later commit.
    private readonly ConcurrentDictionary<string, (string ReservationId, DateTime ExpiresAt)> _verificationStore = new();

    public QrService(
        IReservationRepository reservationRepository,
        IUserRepository userRepository,
        IStationRepository stationRepository,
        IOptions<QrSettings> qrSettings)
    {
        _reservationRepository = reservationRepository;
        _userRepository = userRepository;
        _stationRepository = stationRepository;
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

        if (reservation.ProsumerNIC != prosumerNic)
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

    // Verifies a scanned token and issues the short-lived verificationId that is
    // the only thing authorising CompleteReservation.
    public async Task<QrVerificationResult> VerifyQrToken(string token, string operatorId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new BusinessRuleException(
                "QR_INVALID",
                "Invalid QR code",
                "The scanned QR code is not valid.",
                400);
        }

        string tokenHash = HashToken(token);

        var reservation = await _reservationRepository.FindByQrTokenHash(tokenHash);

        if (reservation is null)
        {
            throw new BusinessRuleException(
                "QR_INVALID",
                "Invalid QR code",
                "The scanned QR code is not valid.",
                400);
        }

        if (reservation.QrExpiresAt == null || reservation.QrExpiresAt < DateTime.UtcNow)
        {
            throw new BusinessRuleException(
                "QR_EXPIRED",
                "QR code expired",
                "This QR code has expired and can no longer be used.",
                410);
        }

        if (reservation.Status != "Approved")
        {
            throw new BusinessRuleException(
                "QR_ALREADY_USED",
                "QR code already used",
                "This QR code has already been used to complete a transfer.",
                409);
        }

        string verificationId = GenerateSecureToken();
        DateTime verificationExpiresAt = DateTime.UtcNow
            .AddMinutes(_qrSettings.Value.VerificationIdExpiryMinutes);
        _verificationStore[verificationId] = (reservation.Id, verificationExpiresAt);

        var prosumer = await _userRepository.FindByNic(reservation.ProsumerNIC);
        var station = await _stationRepository.FindById(reservation.StationId);

        return new QrVerificationResult
        {
            Reservation = reservation,
            Prosumer = prosumer is null ? null : new ProsumerSummary
            {
                Nic = prosumer.Nic ?? string.Empty,
                FullName = prosumer.FullName,
                Email = prosumer.Email,
                Phone = prosumer.Phone
            },
            Station = station,
            VerificationId = verificationId
        };
    }

    // Finalises the energy transfer: the verificationId must be live and tied to
    // this reservation, and the reservation must still be Approved. The entry is
    // consumed on success, so a repeat call fails the verification check.
    public async Task CompleteReservation(string reservationId, string verificationId, string operatorId)
    {
        if (string.IsNullOrWhiteSpace(verificationId))
        {
            throw new BusinessRuleException(
                "VERIFICATION_INVALID",
                "Verification invalid",
                "The verification session is invalid. Please scan the QR code again.",
                400);
        }

        if (!_verificationStore.TryGetValue(verificationId, out var verification)
            || verification.ReservationId != reservationId)
        {
            throw new BusinessRuleException(
                "VERIFICATION_INVALID",
                "Verification invalid",
                "The verification session is invalid. Please scan the QR code again.",
                400);
        }

        if (verification.ExpiresAt < DateTime.UtcNow)
        {
            _verificationStore.TryRemove(verificationId, out _);

            throw new BusinessRuleException(
                "VERIFICATION_EXPIRED",
                "Verification expired",
                "The verification session has expired. Please scan the QR code again.",
                410);
        }

        var reservation = await _reservationRepository.FindReservationById(reservationId);

        if (reservation == null || reservation.Status != "Approved")
        {
            throw new BusinessRuleException(
                "RESERVATION_INVALID_STATE",
                "Invalid reservation state",
                "This reservation is no longer in an approved state.",
                409);
        }

        await _reservationRepository.MarkCompleted(reservationId, operatorId, DateTime.UtcNow);

        // Single-use: the verificationId cannot be presented again.
        _verificationStore.TryRemove(verificationId, out _);
    }

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
