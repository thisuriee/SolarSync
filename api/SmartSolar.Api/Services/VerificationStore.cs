/*
 * File:    VerificationStore.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Holds verificationId -> (reservationId, expiry) between the QR scan
 *          and the completion confirmation.
 *
 *          Registered as a SINGLETON on purpose. The handshake spans two HTTP
 *          requests, and QrService is scoped, so a per-instance dictionary would
 *          be empty by the time CompleteReservation ran — which is exactly the
 *          bug this replaces.
 *
 *          In-process only: entries do not survive an app-pool recycle and are
 *          not shared across instances. Fine for a single-instance deployment;
 *          persisting the verificationId would be the production answer.
 */
using System.Collections.Concurrent;

namespace SmartSolar.Api.Services;

public class VerificationStore
{
    private readonly ConcurrentDictionary<string, (string ReservationId, DateTime ExpiresAt)> _entries = new();

    // Records a freshly issued verificationId against its reservation.
    public void Add(string verificationId, string reservationId, DateTime expiresAt)
    {
        _entries[verificationId] = (reservationId, expiresAt);
    }

    // Returns the expiry only when the verificationId exists AND belongs to this
    // reservation, so a scan for one booking cannot complete another.
    public bool TryGet(string verificationId, string reservationId, out DateTime expiresAt)
    {
        if (_entries.TryGetValue(verificationId, out var entry)
            && entry.ReservationId == reservationId)
        {
            expiresAt = entry.ExpiresAt;
            return true;
        }

        expiresAt = default;
        return false;
    }

    // Consumes a verificationId so it cannot be presented twice.
    public void Remove(string verificationId)
    {
        _entries.TryRemove(verificationId, out _);
    }
}
