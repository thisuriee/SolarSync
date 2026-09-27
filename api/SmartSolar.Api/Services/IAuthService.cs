/*
 * File:    IAuthService.cs
 * Author:  Dahami
 * Created: 2026-09-26
 * Purpose: Contract for authentication: login, prosumer self-registration and
 *          session restore (/auth/me). Every identity rule lives behind this.
 */
using SmartSolar.Api.Dtos;

namespace SmartSolar.Api.Services;

public interface IAuthService
{
    // Verifies credentials and account status, then issues a JWT.
    Task<LoginResponse> Login(LoginRequest request);

    // Creates a Prosumer account in Pending status after NIC/uniqueness checks.
    Task<RegisterProsumerResponse> RegisterProsumer(RegisterProsumerRequest request);

    // Returns the profile of the user identified by the token's sub claim.
    Task<UserProfileResponse> GetCurrentUser(string userId);
}
