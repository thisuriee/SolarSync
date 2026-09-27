/*
 * File:    LoginResult.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: The body of a successful POST /auth/login: the bearer token, when it
 *          expires, and who signed in. Holds data only — the API has already
 *          decided the credentials and account status were acceptable.
 */
package com.sliit.smartsolar.models;

public class LoginResult {

    public final String token;

    /** ISO-8601 UTC from the API, stored as-is. Never used to judge expiry. */
    public final String expiresAt;

    public final String userId;

    /** Prosumers only; null for Grid Operators and Backoffice. */
    public final String nic;

    public final String fullName;
    public final String role;

    // Plain value holder; the parser fills it straight from the response body.
    public LoginResult(String token, String expiresAt, String userId,
                       String nic, String fullName, String role) {
        this.token = token;
        this.expiresAt = expiresAt;
        this.userId = userId;
        this.nic = nic;
        this.fullName = fullName;
        this.role = role;
    }
}
