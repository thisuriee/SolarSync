/*
 * File:    LoginRequest.cs
 * Author:  Dahami
 * Created: 2026-09-26
 * Purpose: Body of POST /api/auth/login. One login for all three roles.
 */
using System.ComponentModel.DataAnnotations;

namespace SmartSolar.Api.Dtos;

public class LoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
