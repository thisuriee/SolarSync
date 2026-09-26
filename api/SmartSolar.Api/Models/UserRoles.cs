/*
 * File:    UserRoles.cs
 * Author:  Dahami
 * Created: 2026-09-26
 * Purpose: The exact role and account-status strings from docs/api-contract.md
 *          and docs/db-schema.md, written once. They are const so they can
 *          also be used inside [Authorize(Roles = ...)] attributes.
 */
namespace SmartSolar.Api.Models;

public static class UserRoles
{
    public const string Backoffice = "Backoffice";
    public const string GridOperator = "GridOperator";
    public const string Prosumer = "Prosumer";
}

public static class UserStatuses
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Deactivated = "Deactivated";
}
