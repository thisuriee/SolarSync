/*
 * File:    RegisterProsumerResponse.cs
 * Author:  Dahami
 * Created: 2026-09-26
 * Purpose: 201 body of POST /api/auth/register-prosumer: {id, nic, status}.
 *          status is always "Pending" — the client shows "awaiting activation".
 */
namespace SmartSolar.Api.Dtos;

public class RegisterProsumerResponse
{
    public string Id { get; set; } = string.Empty;
    public string Nic { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
