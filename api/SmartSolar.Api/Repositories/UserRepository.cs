/*
 * File:    UserRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Mongo implementation of IUserRepository. Pure data access — no
 *          business rules, no validation, no policy.
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
}
