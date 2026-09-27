/*
 * File:    UserRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Mongo implementation of IUserRepository. Pure data access — no
 *          business rules, no validation, no policy. Identity lookups and
 *          insert added by Dahami (2026-09-26); listing, paging, counting and
 *          replace for user management added by Dahami (2026-09-27).
 */
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;

    public UserRepository(IMongoCollection<User> users)
    {
        _users = users;
    }

    // Filters on the Nic field — the business primary key for prosumers.
    public async Task<User?> FindByNic(string nic)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Nic, nic);
        return await _users.Find(filter).FirstOrDefaultAsync();
    }

    // Exact match on username; served by the unique {username: 1} index.
    public async Task<User?> FindByUsername(string username)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Username, username);
        return await _users.Find(filter).FirstOrDefaultAsync();
    }

    // Looks up by _id. An id that is not a valid ObjectId cannot exist, so it
    // returns null instead of letting the driver throw a format exception.
    public async Task<User?> FindById(string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
        {
            return null;
        }

        var filter = Builders<User>.Filter.Eq(u => u.Id, id);
        return await _users.Find(filter).FirstOrDefaultAsync();
    }

    // True when any user already holds this NIC (unique sparse index).
    public async Task<bool> NicExists(string nic)
    {
        return await _users.Find(u => u.Nic == nic).AnyAsync();
    }

    // True when any user already holds this username (unique index).
    public async Task<bool> UsernameExists(string username)
    {
        return await _users.Find(u => u.Username == username).AnyAsync();
    }

    // True when any user already holds this email (unique index).
    public async Task<bool> EmailExists(string email)
    {
        return await _users.Find(u => u.Email == email).AnyAsync();
    }

    // Plain insert. Duplicate-key races are surfaced as MongoWriteException
    // and translated into business errors by the service, not here.
    public async Task Insert(User user)
    {
        await _users.InsertOneAsync(user);
    }

    // Email check that ignores the user being edited.
    public async Task<bool> EmailExistsForOther(string email, string excludeId)
    {
        return await _users.Find(u => u.Email == email && u.Id != excludeId).AnyAsync();
    }

    // Role is always restricted to the two web roles, so this can never
    // return a prosumer even when no role filter is given.
    public async Task<List<User>> FindWebUsers(string? role, string? status)
    {
        var f = Builders<User>.Filter;
        var filter = f.In(u => u.Role, new[] { UserRoles.Backoffice, UserRoles.GridOperator });

        if (role is not null) filter &= f.Eq(u => u.Role, role);
        if (status is not null) filter &= f.Eq(u => u.Status, status);

        return await _users.Find(filter).SortBy(u => u.FullName).ToListAsync();
    }

    // Count and page run on the same filter so totalCount matches the items.
    // The search text is Regex.Escape'd: it is matched literally, never
    // executed as a pattern (no regex injection from the q parameter).
    public async Task<(List<User> Items, long TotalCount)> FindProsumersPaged(string? status, string? q, int skip, int limit)
    {
        var f = Builders<User>.Filter;
        var filter = f.Eq(u => u.Role, UserRoles.Prosumer);

        if (status is not null) filter &= f.Eq(u => u.Status, status);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(q.Trim()), "i");
            filter &= f.Or(
                f.Regex(u => u.Nic, pattern),
                f.Regex(u => u.FullName, pattern),
                f.Regex(u => u.Username, pattern),
                f.Regex(u => u.Email, pattern));
        }

        var total = await _users.CountDocumentsAsync(filter);
        var items = await _users.Find(filter)
            .SortByDescending(u => u.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();

        return (items, total);
    }

    // Served by the compound {role: 1, status: 1} index.
    public async Task<List<User>> FindProsumersByStatus(string status)
    {
        return await _users.Find(u => u.Role == UserRoles.Prosumer && u.Status == status)
            .SortBy(u => u.CreatedAt)
            .ToListAsync();
    }

    // Also served by the {role: 1, status: 1} index.
    public async Task<long> CountByRoleAndStatus(string role, string status)
    {
        return await _users.CountDocumentsAsync(u => u.Role == role && u.Status == status);
    }

    // Whole-document replace by _id. Unique-index violations surface as
    // MongoWriteException for the service to translate.
    public async Task Replace(User user)
    {
        await _users.ReplaceOneAsync(u => u.Id == user.Id, user);
    }
}
