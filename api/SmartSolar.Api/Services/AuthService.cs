/*
 * File:    AuthService.cs
 * Author:  Dahami
 * Created: 2026-09-26
 * Purpose: Authentication business rules (docs/api-contract.md §2, docs/auth.md):
 *          password verification with PasswordHasher<User>, the Pending /
 *          Deactivated login block, prosumer registration with NIC format and
 *          uniqueness checks, and role/status forced server-side. Throws
 *          BusinessRuleException; ExceptionHandlingMiddleware formats it.
 */
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Repositories;

namespace SmartSolar.Api.Services;

public class AuthService : IAuthService
{
    // Sri Lankan NIC: old format is 9 digits + V or X (e.g. 881234567V),
    // new format is 12 digits (e.g. 200012345671). Both are accepted.
    private static readonly Regex NicPattern = new(@"^([0-9]{9}[VX]|[0-9]{12})$", RegexOptions.Compiled);

    private readonly IUserRepository _users;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtTokenGenerator _tokenGenerator;

    public AuthService(IUserRepository users, IPasswordHasher<User> passwordHasher, JwtTokenGenerator tokenGenerator)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    // Rule: bad username or password -> 401 AUTH_INVALID_CREDENTIALS (same
    // message for both, so the API never reveals which usernames exist).
    // Rule: only Active accounts log in — Pending -> 403 AUTH_ACCOUNT_PENDING,
    // Deactivated -> 403 AUTH_ACCOUNT_DEACTIVATED. Status is checked AFTER the
    // password, so an attacker cannot probe an account's status without it.
    public async Task<LoginResponse> Login(LoginRequest request)
    {
        var user = await _users.FindByUsername(NormaliseUsername(request.Username));

        if (user is null || !PasswordMatches(user, request.Password))
        {
            throw new BusinessRuleException(
                "AUTH_INVALID_CREDENTIALS",
                "Login failed",
                "The username or password is incorrect.",
                StatusCodes.Status401Unauthorized);
        }

        EnsureAccountActive(user);

        var (token, expiresAt) = _tokenGenerator.Generate(user.Id, user.Nic, user.FullName, user.Role);

        return new LoginResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = new LoginUser
            {
                Id = user.Id,
                Nic = user.Nic,
                FullName = user.FullName,
                Role = user.Role
            }
        };
    }

    // Rule: NIC must be a valid Sri Lankan NIC and unique (USER_NIC_EXISTS);
    // username and email must be unique. Rule: role is forced to Prosumer and
    // status to Pending here — the request DTO has no field for either, so the
    // client cannot influence them. Only a Backoffice officer can activate.
    public async Task<RegisterProsumerResponse> RegisterProsumer(RegisterProsumerRequest request)
    {
        var nic = request.Nic.Trim().ToUpperInvariant();
        var username = NormaliseUsername(request.Username);
        var email = request.Email.Trim().ToLowerInvariant();

        if (!NicPattern.IsMatch(nic))
        {
            throw new BusinessRuleException(
                "USER_NIC_INVALID",
                "Invalid NIC",
                "NIC must be 9 digits followed by V or X (old format), or 12 digits (new format).",
                StatusCodes.Status400BadRequest);
        }

        // Friendly pre-checks. The unique indexes are the real guarantee —
        // see the duplicate-key catch below for the concurrent case.
        if (await _users.NicExists(nic)) throw NicExists(nic);
        if (await _users.UsernameExists(username)) throw UsernameExists();
        if (await _users.EmailExists(email)) throw EmailExists();

        var now = DateTime.UtcNow;
        var user = new User
        {
            Nic = nic,
            Username = username,
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = request.Phone.Trim(),
            Address = request.Address.Trim(),
            Role = UserRoles.Prosumer,        // forced server-side
            Status = UserStatuses.Pending,    // forced server-side
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        try
        {
            await _users.Insert(user);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Two registrations raced past the pre-checks; the unique index
            // rejected the second. Map the violated index (default names from
            // docs/seed/seed-database.js) to the right code.
            var message = ex.WriteError.Message;
            if (message.Contains("index: nic_1")) throw NicExists(nic);
            if (message.Contains("index: username_1")) throw UsernameExists();
            throw EmailExists();
        }

        return new RegisterProsumerResponse
        {
            Id = user.Id,
            Nic = user.Nic,
            Status = user.Status
        };
    }

    // Session restore for both clients. The user id comes from the token's
    // sub claim, never from the request. Rule: an account deactivated after
    // login cannot restore its session — the status is re-checked here.
    public async Task<UserProfileResponse> GetCurrentUser(string userId)
    {
        var user = await _users.FindById(userId);

        if (user is null)
        {
            // Token is validly signed but the account no longer exists.
            throw new BusinessRuleException(
                "AUTH_INVALID_TOKEN",
                "Invalid session",
                "Your account could not be found. Please log in again.",
                StatusCodes.Status401Unauthorized);
        }

        EnsureAccountActive(user);

        return UserProfileResponse.FromUser(user);
    }

    // Rule: only Active accounts may use the system. Shared by login and /me
    // so both paths give the identical codes and messages.
    private static void EnsureAccountActive(User user)
    {
        if (user.Status == UserStatuses.Pending)
        {
            throw new BusinessRuleException(
                "AUTH_ACCOUNT_PENDING",
                "Account pending activation",
                "Your account is awaiting activation by a Backoffice officer.",
                StatusCodes.Status403Forbidden);
        }

        if (user.Status != UserStatuses.Active)
        {
            throw new BusinessRuleException(
                "AUTH_ACCOUNT_DEACTIVATED",
                "Account deactivated",
                "Your account has been deactivated. Contact a Backoffice officer to reactivate it.",
                StatusCodes.Status403Forbidden);
        }
    }

    // Verifies the password against the stored PBKDF2 hash. SuccessRehashNeeded
    // still means the password is correct (the hash just uses older settings).
    private bool PasswordMatches(User user, string password)
    {
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result != PasswordVerificationResult.Failed;
    }

    // Usernames are stored lower-case so "Nimal.Perera" and "nimal.perera"
    // cannot become two accounts.
    private static string NormaliseUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }

    // Builds the 409 for a NIC that is already registered.
    private static BusinessRuleException NicExists(string nic)
    {
        return new BusinessRuleException(
            "USER_NIC_EXISTS",
            "NIC already registered",
            $"An account with NIC {nic} already exists.",
            StatusCodes.Status409Conflict);
    }

    // Builds the 409 for a username that is already taken.
    private static BusinessRuleException UsernameExists()
    {
        return new BusinessRuleException(
            "USER_USERNAME_EXISTS",
            "Username taken",
            "That username is already taken. Please choose another.",
            StatusCodes.Status409Conflict);
    }

    // Builds the 409 for an email that is already registered.
    private static BusinessRuleException EmailExists()
    {
        return new BusinessRuleException(
            "USER_EMAIL_EXISTS",
            "Email already registered",
            "An account with that email address already exists.",
            StatusCodes.Status409Conflict);
    }
}
