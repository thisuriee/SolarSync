/*
 * File:    IUserRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Data access contract for the UsersDetail collection. FindByNic was
 *          added for the QR fulfilment vertical (Imadh); the identity lookups
 *          and insert were added for login and registration (Dahami, 2026-09-26);
 *          listing, counting and replace for user management (Dahami, 2026-09-27).
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public interface IUserRepository
{
    // Returns the user holding the given NIC, or null when there is none.
    Task<User?> FindByNic(string nic);

    // Returns the user with the given login username, or null. Used by login.
    Task<User?> FindByUsername(string username);

    // Returns the user with the given Mongo _id, or null. Used by /auth/me,
    // where the id comes from the token's "sub" claim.
    Task<User?> FindById(string id);

    // Existence checks behind the registration uniqueness rules
    // (USER_NIC_EXISTS, USER_USERNAME_EXISTS, USER_EMAIL_EXISTS).
    Task<bool> NicExists(string nic);
    Task<bool> UsernameExists(string username);
    Task<bool> EmailExists(string email);

    // Inserts a new user document. The driver fills user.Id on success.
    Task Insert(User user);

    // True when an account OTHER than excludeId holds this email. Used when
    // an existing user changes their email, so they don't collide with themselves.
    Task<bool> EmailExistsForOther(string email, string excludeId);

    // Web users (Backoffice + GridOperator only — never prosumers), optionally
    // filtered by exact role and/or status, ordered by full name.
    Task<List<User>> FindWebUsers(string? role, string? status);

    // One page of prosumers. status filters exactly; q is a case-insensitive
    // "contains" over NIC, full name, username and email. Newest first.
    Task<(List<User> Items, long TotalCount)> FindProsumersPaged(string? status, string? q, int skip, int limit);

    // All prosumers in one status, oldest first (a first-come queue).
    Task<List<User>> FindProsumersByStatus(string status);

    // Number of users with this exact role and status. Drives the
    // last-active-Backoffice guard.
    Task<long> CountByRoleAndStatus(string role, string status);

    // Replaces the whole stored document with the given one (matched by Id).
    Task Replace(User user);
}
