/*
 * File:    IUserService.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Contract for user management (docs/api-contract.md §2): web-user
 *          CRUD for Backoffice, and prosumer listing, profile, activation and
 *          deactivation. Every rule behind these routes lives behind this.
 */
using SmartSolar.Api.Dtos;
using SmartSolar.Api.Helpers;

namespace SmartSolar.Api.Services;

public interface IUserService
{
    // ---- Web users (Backoffice + GridOperator accounts)

    // Lists web users, optionally filtered by role and/or status.
    Task<List<UserProfileResponse>> GetWebUsers(string? role, string? status);

    // Creates an Active web user. Role must be Backoffice or GridOperator.
    Task<UserProfileResponse> CreateWebUser(CreateWebUserRequest request);

    // Full update. Guards against removing the last active Backoffice.
    Task<UserProfileResponse> UpdateWebUser(string id, UpdateWebUserRequest request);

    // Status-only update, with the same last-Backoffice guard.
    Task<UserProfileResponse> UpdateWebUserStatus(string id, UpdateStatusRequest request);

    // ---- Prosumers

    // One page of prosumers, filtered by status and a free-text search.
    Task<PagedResult<UserProfileResponse>> GetProsumers(string? status, string? q, int? page, int? pageSize);

    // Every Pending prosumer, oldest first — the pending-activations queue.
    Task<List<UserProfileResponse>> GetPendingProsumers();

    // A prosumer's profile. A prosumer caller may only read their own.
    Task<UserProfileResponse> GetProsumer(CallerIdentity caller, string nic);

    // Edits contact fields. A prosumer caller may only edit their own.
    Task<UserProfileResponse> UpdateProsumer(CallerIdentity caller, string nic, UpdateProsumerRequest request);

    // Prosumer closes their own account; blocked by active reservations.
    Task<UserProfileResponse> RequestDeactivation(CallerIdentity caller, string nic);

    // Backoffice-only activation / reactivation.
    Task<UserProfileResponse> Activate(CallerIdentity caller, string nic);

    // Backoffice-only deactivation; blocked by active reservations.
    Task<UserProfileResponse> Deactivate(CallerIdentity caller, string nic, string? reason);
}
