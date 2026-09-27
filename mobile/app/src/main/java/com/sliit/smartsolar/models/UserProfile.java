/*
 * File:    UserProfile.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: A user profile as the API returns it (UserProfileResponse): the
 *          fields the My Profile screen shows. There is no password field —
 *          the API never sends one. Holds data only.
 */
package com.sliit.smartsolar.models;

public class UserProfile {

    public final String userId;
    public final String nic;
    public final String username;
    public final String fullName;
    public final String email;
    public final String phone;
    public final String address;
    public final String role;
    public final String status;

    /** ISO-8601 UTC, or null. Converted to local time only for display. */
    public final String deactivationRequestedAt;

    // Plain value holder; the parser fills it straight from the response body.
    public UserProfile(String userId, String nic, String username, String fullName,
                       String email, String phone, String address, String role,
                       String status, String deactivationRequestedAt) {
        this.userId = userId;
        this.nic = nic;
        this.username = username;
        this.fullName = fullName;
        this.email = email;
        this.phone = phone;
        this.address = address;
        this.role = role;
        this.status = status;
        this.deactivationRequestedAt = deactivationRequestedAt;
    }
}
