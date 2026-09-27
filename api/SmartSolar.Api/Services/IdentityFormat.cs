/*
 * File:    IdentityFormat.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: The canonical stored form of identity fields, shared by AuthService
 *          and UserService so a value is compared the same way everywhere:
 *          usernames and emails lower-case, NICs upper-case (old-format V/X).
 */
using System.Text.RegularExpressions;

namespace SmartSolar.Api.Services;

public static class IdentityFormat
{
    // Sri Lankan NIC: old format is 9 digits + V or X (e.g. 881234567V),
    // new format is 12 digits (e.g. 200012345671). Both are accepted.
    private static readonly Regex NicPattern = new(@"^([0-9]{9}[VX]|[0-9]{12})$", RegexOptions.Compiled);

    // Usernames are stored lower-case so "Nimal.Perera" and "nimal.perera"
    // cannot become two accounts.
    public static string NormaliseUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }

    // Emails are stored lower-case so uniqueness is case-insensitive.
    public static string NormaliseEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    // NICs are stored upper-case, so a route {nic} ending in "v" still
    // matches the stored "V" and the token's nic claim.
    public static string NormaliseNic(string nic)
    {
        return nic.Trim().ToUpperInvariant();
    }

    // True when an already-normalised NIC is in the old or new format.
    public static bool IsValidNic(string nic)
    {
        return NicPattern.IsMatch(nic);
    }
}
