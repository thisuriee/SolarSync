/*
 * File:    CallerIdentity.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Who is making the request, as read from the validated JWT
 *          (sub, role, nic). Controllers build it with User.GetCaller() and
 *          hand it to the service, so ownership and role re-checks in the
 *          service layer never touch HttpContext or the request body.
 */
namespace SmartSolar.Api.Helpers;

// Nic is null for web users — the claim is only issued to prosumers.
public record CallerIdentity(string UserId, string Role, string? Nic);
