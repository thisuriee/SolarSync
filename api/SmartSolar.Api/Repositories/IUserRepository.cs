/*
 * File:    IUserRepository.cs
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Data access contract for the QR fulfilment vertical — resolves the
 *          prosumer who owns a reservation.
 */
using SmartSolar.Api.Models;

namespace SmartSolar.Api.Repositories;

public interface IUserRepository
{
    // Returns the user holding the given NIC, or null when there is none.
    Task<User?> FindByNic(string nic);
}
