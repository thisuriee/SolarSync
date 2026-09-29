/*
 * File:    RegisterResult.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: The body of a successful POST /auth/register-prosumer (201):
 *          {id, nic, status}. The status is whatever the API set — always
 *          "Pending", because the server forces it; the app never chooses it.
 */
package com.sliit.smartsolar.models;

public class RegisterResult {

    public final String userId;

    /** The NIC as the API stored it: trimmed and upper-cased. */
    public final String nic;

    public final String status;

    // Plain value holder; the parser fills it straight from the response body.
    public RegisterResult(String userId, String nic, String status) {
        this.userId = userId;
        this.nic = nic;
        this.status = status;
    }
}
