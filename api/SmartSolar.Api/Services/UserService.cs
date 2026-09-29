/*
 * File:    UserService.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: User-management business rules (docs/api-contract.md §2, docs/auth.md):
 *          web users are Backoffice/GridOperator only, the last active
 *          Backoffice cannot be demoted or disabled, a prosumer sees and edits
 *          only their own profile, only Backoffice activates (re-checked here,
 *          not only by the attribute), and deactivation is blocked while
 *          active reservations exist. Throws BusinessRuleException;
 *          ExceptionHandlingMiddleware formats it.
 */
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;
using SmartSolar.Api.Models;
using SmartSolar.Api.Repositories;

namespace SmartSolar.Api.Services;

public class UserService : IUserService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private static readonly string[] WebRoles = { UserRoles.Backoffice, UserRoles.GridOperator };
    private static readonly string[] AllStatuses = { UserStatuses.Pending, UserStatuses.Active, UserStatuses.Deactivated };

    // Pending only means "prosumer awaiting activation"; a web user is
    // created Active by Backoffice, so it is either Active or Deactivated.
    private static readonly string[] WebUserStatuses = { UserStatuses.Active, UserStatuses.Deactivated };

    private readonly IUserRepository _users;
    private readonly IPasswordHasher<User> _passwordHasher;
    // The group's shared active-reservation predicate (docs/api-contract.md §6),
    // owned outside this vertical. Identity only asks for a count.
    private readonly IReservationQueries _reservations;

    // Dependencies come from DI (Program.cs). IReservationQueries is the same
    // interface NodeService uses, so both verticals agree on "active".
    public UserService(IUserRepository users, IPasswordHasher<User> passwordHasher, IReservationQueries reservations)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _reservations = reservations;
    }

    // =========================================================================
    // Web users
    // =========================================================================

    // Lists Backoffice/GridOperator accounts. Rule: this route never returns
    // prosumers, so role=Prosumer is rejected rather than silently ignored.
    public async Task<List<UserProfileResponse>> GetWebUsers(string? role, string? status)
    {
        var roleFilter = string.IsNullOrWhiteSpace(role) ? null : RequireWebRole(role);
        var statusFilter = string.IsNullOrWhiteSpace(status) ? null : RequireStatus(status, AllStatuses);

        var users = await _users.FindWebUsers(roleFilter, statusFilter);
        return users.Select(UserProfileResponse.FromUser).ToList();
    }

    // Rule: role must be Backoffice or GridOperator — Prosumer is rejected
    // (prosumers only enter through self-registration). Rule: username and
    // email unique. Status is forced to Active: Backoffice is the approver.
    public async Task<UserProfileResponse> CreateWebUser(CreateWebUserRequest request)
    {
        var role = RequireWebRole(request.Role);
        var username = IdentityFormat.NormaliseUsername(request.Username);
        var email = IdentityFormat.NormaliseEmail(request.Email);

        // Friendly pre-checks; the unique indexes are the real guarantee.
        if (await _users.UsernameExists(username)) throw UserErrors.UsernameExists();
        if (await _users.EmailExists(email)) throw UserErrors.EmailExists();

        var now = DateTime.UtcNow;
        var user = new User
        {
            Nic = null,                       // web users have no NIC
            Username = username,
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = request.Phone.Trim(),
            Role = role,
            Status = UserStatuses.Active,     // forced server-side
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
            throw UserErrors.FromDuplicateKey(ex, null);
        }

        return UserProfileResponse.FromUser(user);
    }

    // Full update of a web user. Rule: role stays a web role; status is
    // Active or Deactivated; the last active Backoffice cannot be demoted or
    // disabled (USER_LAST_BACKOFFICE); email stays unique.
    public async Task<UserProfileResponse> UpdateWebUser(string id, UpdateWebUserRequest request)
    {
        var user = await LoadWebUser(id);
        var newRole = RequireWebRole(request.Role);
        var newStatus = RequireStatus(request.Status, WebUserStatuses);
        var email = IdentityFormat.NormaliseEmail(request.Email);

        await EnsureNotRemovingLastBackoffice(user, newRole, newStatus);

        if (email != user.Email && await _users.EmailExistsForOther(email, user.Id))
        {
            throw UserErrors.EmailExists();
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.Phone = request.Phone.Trim();
        user.Role = newRole;
        user.Status = newStatus;

        await Save(user);
        return UserProfileResponse.FromUser(user);
    }

    // Status-only change. Rule: same last-active-Backoffice guard as PUT, so
    // the rule cannot be bypassed by choosing the other route.
    public async Task<UserProfileResponse> UpdateWebUserStatus(string id, UpdateStatusRequest request)
    {
        var user = await LoadWebUser(id);
        var newStatus = RequireStatus(request.Status, WebUserStatuses);

        await EnsureNotRemovingLastBackoffice(user, user.Role, newStatus);

        user.Status = newStatus;
        await Save(user);
        return UserProfileResponse.FromUser(user);
    }

    // =========================================================================
    // Prosumers
    // =========================================================================

    // Paged prosumer list for Backoffice and Grid Operator. page / pageSize
    // are clamped to sane bounds instead of failing the request.
    public async Task<PagedResult<UserProfileResponse>> GetProsumers(string? status, string? q, int? page, int? pageSize)
    {
        var statusFilter = string.IsNullOrWhiteSpace(status) ? null : RequireStatus(status, AllStatuses);
        var currentPage = Math.Max(page ?? 1, 1);
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);

        var (items, total) = await _users.FindProsumersPaged(statusFilter, q, (currentPage - 1) * size, size);

        return PagedResult<UserProfileResponse>.Create(
            items.Select(UserProfileResponse.FromUser).ToList(), currentPage, size, total);
    }

    // The pending-activations queue: every Pending prosumer, oldest first.
    public async Task<List<UserProfileResponse>> GetPendingProsumers()
    {
        var users = await _users.FindProsumersByStatus(UserStatuses.Pending);
        return users.Select(UserProfileResponse.FromUser).ToList();
    }

    // Rule: a prosumer reads only their own profile — the ownership check runs
    // BEFORE the lookup, so another NIC gets 403 whether it exists or not.
    public async Task<UserProfileResponse> GetProsumer(CallerIdentity caller, string nic)
    {
        nic = IdentityFormat.NormaliseNic(nic);
        EnsureProsumerOwnsOrStaff(caller, nic);

        return UserProfileResponse.FromUser(await LoadProsumer(nic));
    }

    // Rule: a prosumer edits only their own profile. Only fullName, email,
    // phone and address are copied from the request — nic, role and status
    // are not even properties of UpdateProsumerRequest, so cannot be applied.
    public async Task<UserProfileResponse> UpdateProsumer(CallerIdentity caller, string nic, UpdateProsumerRequest request)
    {
        nic = IdentityFormat.NormaliseNic(nic);
        EnsureProsumerOwnsOrStaff(caller, nic);

        var user = await LoadProsumer(nic);
        var email = IdentityFormat.NormaliseEmail(request.Email);

        if (email != user.Email && await _users.EmailExistsForOther(email, user.Id))
        {
            throw UserErrors.EmailExists();
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.Phone = request.Phone.Trim();
        user.Address = request.Address.Trim();

        await Save(user);
        return UserProfileResponse.FromUser(user);
    }

    // Rule: own account only (token nic == route nic). Rule: blocked while the
    // prosumer holds active reservations (USER_HAS_ACTIVE_RESERVATIONS).
    // Sets Deactivated + deactivationRequestedAt. Idempotent if already done.
    public async Task<UserProfileResponse> RequestDeactivation(CallerIdentity caller, string nic)
    {
        nic = IdentityFormat.NormaliseNic(nic);
        EnsureIsOwnProsumerAccount(caller, nic);

        var user = await LoadProsumer(nic);
        if (user.Status == UserStatuses.Deactivated)
        {
            return UserProfileResponse.FromUser(user);
        }

        await EnsureNoActiveReservations(nic);

        user.Status = UserStatuses.Deactivated;
        user.DeactivationRequestedAt = DateTime.UtcNow;

        await Save(user);
        return UserProfileResponse.FromUser(user);
    }

    // Rule: ONLY Backoffice can activate or reactivate a prosumer. The
    // controller attribute already blocks other roles; this re-check makes the
    // rule survive the attribute being removed in a refactor. Records the
    // officer (activatedBy = token sub) and time as audit evidence.
    public async Task<UserProfileResponse> Activate(CallerIdentity caller, string nic)
    {
        EnsureBackoffice(caller, "Only a Backoffice officer can activate or reactivate a prosumer account.");
        nic = IdentityFormat.NormaliseNic(nic);

        var user = await LoadProsumer(nic);
        if (user.Status == UserStatuses.Active)
        {
            return UserProfileResponse.FromUser(user);
        }

        user.Status = UserStatuses.Active;
        user.ActivatedBy = caller.UserId;
        user.ActivatedAt = DateTime.UtcNow;
        user.DeactivationRequestedAt = null;   // describes the deactivation just undone
        user.DeactivationReason = null;

        await Save(user);
        return UserProfileResponse.FromUser(user);
    }

    // Rule: Backoffice only (attribute + re-check, as for Activate). Rule:
    // blocked while the prosumer holds active reservations.
    public async Task<UserProfileResponse> Deactivate(CallerIdentity caller, string nic, string? reason)
    {
        EnsureBackoffice(caller, "Only a Backoffice officer can deactivate a prosumer account.");
        nic = IdentityFormat.NormaliseNic(nic);

        var user = await LoadProsumer(nic);
        if (user.Status == UserStatuses.Deactivated)
        {
            return UserProfileResponse.FromUser(user);
        }

        await EnsureNoActiveReservations(nic);

        user.Status = UserStatuses.Deactivated;
        user.DeactivationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await Save(user);
        return UserProfileResponse.FromUser(user);
    }

    // =========================================================================
    // Guards — one rule each
    // =========================================================================

    // Rule: the system must always keep at least one active Backoffice, or
    // nobody could activate prosumers or manage users again. Applies only when
    // this change takes an active Backoffice out of "active Backoffice".
    private async Task EnsureNotRemovingLastBackoffice(User user, string newRole, string newStatus)
    {
        var isActiveBackoffice = user.Role == UserRoles.Backoffice && user.Status == UserStatuses.Active;
        var staysActiveBackoffice = newRole == UserRoles.Backoffice && newStatus == UserStatuses.Active;

        if (isActiveBackoffice && !staysActiveBackoffice
            && await _users.CountByRoleAndStatus(UserRoles.Backoffice, UserStatuses.Active) <= 1)
        {
            throw UserErrors.LastBackoffice();
        }
    }

    // Rule: a Prosumer caller may only touch the profile whose NIC is in their
    // token -> 403 AUTH_FORBIDDEN_ROLE (never 404). Staff roles may view any.
    private static void EnsureProsumerOwnsOrStaff(CallerIdentity caller, string nic)
    {
        if (caller.Role == UserRoles.Prosumer && caller.Nic != nic)
        {
            throw UserErrors.ForbiddenRole("You can only access your own profile.");
        }
    }

    // Rule: self-service deactivation is for the account owner only — the
    // caller must be a Prosumer AND the route NIC must be their token NIC.
    private static void EnsureIsOwnProsumerAccount(CallerIdentity caller, string nic)
    {
        if (caller.Role != UserRoles.Prosumer || caller.Nic != nic)
        {
            throw UserErrors.ForbiddenRole("You can only request deactivation of your own account.");
        }
    }

    // Service-layer half of "attribute AND re-check" for Backoffice-only actions.
    private static void EnsureBackoffice(CallerIdentity caller, string detail)
    {
        if (caller.Role != UserRoles.Backoffice)
        {
            throw UserErrors.ForbiddenRole(detail);
        }
    }

    // Rule: no deactivation while the prosumer has active reservations. The
    // "active" predicate is the shared one (IReservationQueries), not ours.
    private async Task EnsureNoActiveReservations(string nic)
    {
        var count = await _reservations.CountActiveForProsumer(nic);
        if (count > 0)
        {
            throw UserErrors.HasActiveReservations(count);
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    // Loads a web user by id. A prosumer's id is "not found" on /webusers —
    // this route family only manages Backoffice and GridOperator accounts.
    private async Task<User> LoadWebUser(string id)
    {
        var user = await _users.FindById(id);
        if (user is null || !WebRoles.Contains(user.Role))
        {
            throw UserErrors.NotFound("web user with that id");
        }
        return user;
    }

    // Loads a prosumer by NIC, or 404 if no prosumer holds it.
    private async Task<User> LoadProsumer(string nic)
    {
        var user = await _users.FindByNic(nic);
        if (user is null || user.Role != UserRoles.Prosumer)
        {
            throw UserErrors.NotFound($"prosumer with NIC {nic}");
        }
        return user;
    }

    // Stamps updatedAt (UTC) and writes the document, translating a
    // unique-index race on email into USER_EMAIL_EXISTS.
    private async Task Save(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _users.Replace(user);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw UserErrors.FromDuplicateKey(ex, user.Nic);
        }
    }

    // Returns the canonical role string if it is a web role (case-insensitive
    // input), otherwise USER_ROLE_INVALID. Rejects Prosumer on /webusers.
    private static string RequireWebRole(string role)
    {
        var match = WebRoles.FirstOrDefault(r => r.Equals(role.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? throw UserErrors.RoleInvalid(
            "Role must be Backoffice or GridOperator. Prosumers register through the mobile app.");
    }

    // Returns the canonical status string if it is one of the allowed values,
    // otherwise USER_STATUS_INVALID naming the allowed values.
    private static string RequireStatus(string status, string[] allowed)
    {
        var match = allowed.FirstOrDefault(s => s.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? throw UserErrors.StatusInvalid($"Status must be one of: {string.Join(", ", allowed)}.");
    }
}
