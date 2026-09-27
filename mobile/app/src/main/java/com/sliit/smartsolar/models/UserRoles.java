/*
 * File:    UserRoles.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: The role and status strings the API issues, mirrored from the API's
 *          Models/UserRoles.cs so no screen hard-codes "Prosumer" or "Pending".
 *          Used for navigation only — the API enforces every role rule.
 */
package com.sliit.smartsolar.models;

public final class UserRoles {

    public static final String BACKOFFICE = "Backoffice";
    public static final String GRID_OPERATOR = "GridOperator";
    public static final String PROSUMER = "Prosumer";

    public static final String STATUS_PENDING = "Pending";
    public static final String STATUS_ACTIVE = "Active";
    public static final String STATUS_DEACTIVATED = "Deactivated";

    private UserRoles() {
    }
}
