/*
 * File:    IUserRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Data access contract for the UsersDetail collection. FindByNic was
 *          added for the QR fulfilment vertical (Imadh); the identity lookups
 *          and insert were added for login and registration (Dahami, 2026-09-26).
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
}
