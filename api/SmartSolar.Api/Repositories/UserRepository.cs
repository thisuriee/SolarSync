/*
 * File:    UserRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Mongo implementation of IUserRepository. Pure data access — no
 *          business rules, no validation, no policy. Identity lookups and
 *          insert added by Dahami (2026-09-26).
 */
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
}
