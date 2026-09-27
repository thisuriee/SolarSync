/*
 * File:    UserErrors.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Every identity BusinessRuleException built in one place, so login,
 *          registration, web-user and prosumer management return identical
 *          codes and messages. Codes are registered in docs/response-format.md.
 */
using MongoDB.Driver;
using SmartSolar.Api.Helpers;

namespace SmartSolar.Api.Services;

public static class UserErrors
{
    // 409 — NIC already registered (NIC is the prosumer business key).
    public static BusinessRuleException NicExists(string nic)
    {
        return new BusinessRuleException(
            "USER_NIC_EXISTS",
            "NIC already registered",
            $"An account with NIC {nic} already exists.",
            StatusCodes.Status409Conflict);
    }

    // 409 — username taken (usernames are stored lower-case).
    public static BusinessRuleException UsernameExists()
    {
        return new BusinessRuleException(
            "USER_USERNAME_EXISTS",
            "Username taken",
            "That username is already taken. Please choose another.",
            StatusCodes.Status409Conflict);
    }

    // 409 — email already used by another account (stored lower-case).
    public static BusinessRuleException EmailExists()
    {
        return new BusinessRuleException(
            "USER_EMAIL_EXISTS",
            "Email already registered",
            "An account with that email address already exists.",
            StatusCodes.Status409Conflict);
    }

    // 404 — the user genuinely does not exist. Never used to hide a 403.
    public static BusinessRuleException NotFound(string what)
    {
        return new BusinessRuleException(
            "USER_NOT_FOUND",
            "User not found",
            $"No {what} was found.",
            StatusCodes.Status404NotFound);
    }

    // 403 — wrong role, or a prosumer touching someone else's profile.
    public static BusinessRuleException ForbiddenRole(string detail)
    {
        return new BusinessRuleException(
            "AUTH_FORBIDDEN_ROLE",
            "Not permitted",
            detail,
            StatusCodes.Status403Forbidden);
    }

    // 400 — role outside what the route allows (e.g. Prosumer on /webusers).
    public static BusinessRuleException RoleInvalid(string detail)
    {
        return new BusinessRuleException(
            "USER_ROLE_INVALID",
            "Invalid role",
            detail,
            StatusCodes.Status400BadRequest);
    }

    // 400 — status string outside what the route allows.
    public static BusinessRuleException StatusInvalid(string detail)
    {
        return new BusinessRuleException(
            "USER_STATUS_INVALID",
            "Invalid status",
            detail,
            StatusCodes.Status400BadRequest);
    }

    // Maps a unique-index violation (two writes raced past the friendly
    // pre-checks) to the right 409. Index names are the defaults created by
    // docs/seed/seed-database.js.
    public static BusinessRuleException FromDuplicateKey(MongoWriteException ex, string? nic)
    {
        var message = ex.WriteError?.Message ?? string.Empty;
        if (message.Contains("index: nic_1")) return NicExists(nic ?? string.Empty);
        if (message.Contains("index: username_1")) return UsernameExists();
        return EmailExists();
    }

    // 409 — change would leave the system with no active Backoffice account.
    public static BusinessRuleException LastBackoffice()
    {
        return new BusinessRuleException(
            "USER_LAST_BACKOFFICE",
            "Last Backoffice account",
            "This is the only active Backoffice account. Activate or promote another Backoffice account first.",
            StatusCodes.Status409Conflict);
    }

    // 409 — prosumer deactivation blocked while bookings are still live.
    public static BusinessRuleException HasActiveReservations(long count)
    {
        return new BusinessRuleException(
            "USER_HAS_ACTIVE_RESERVATIONS",
            "Active reservations exist",
            $"This account has {count} active reservation(s). They must be completed or cancelled before the account can be deactivated.",
            StatusCodes.Status409Conflict);
    }
}
