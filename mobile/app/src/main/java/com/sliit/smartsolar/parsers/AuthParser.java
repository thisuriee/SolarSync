/*
 * File:    AuthParser.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Turns the identity vertical's success bodies (docs/api-contract.md §2)
 *          into model objects with org.json. Parsing only — no decisions.
 */
package com.sliit.smartsolar.parsers;

import com.sliit.smartsolar.models.LoginResult;
import com.sliit.smartsolar.models.RegisterResult;
import com.sliit.smartsolar.models.UserProfile;

import org.json.JSONException;
import org.json.JSONObject;

public final class AuthParser {

    private AuthParser() {
    }

    // Parses {token, expiresAt, user:{id, nic?, fullName, role}}. The nic key is
    // omitted by the API for web users, so it is read as optional (null).
    public static LoginResult parseLogin(String body) throws JSONException {
        JSONObject json = new JSONObject(body);
        JSONObject user = json.getJSONObject("user");

        return new LoginResult(
                json.getString("token"),
                json.getString("expiresAt"),
                user.getString("id"),
                optionalString(user, "nic"),
                user.getString("fullName"),
                user.getString("role"));
    }

    // Parses the 201 body of POST /auth/register-prosumer: {id, nic, status}.
    public static RegisterResult parseRegister(String body) throws JSONException {
        JSONObject json = new JSONObject(body);

        return new RegisterResult(
                json.getString("id"),
                json.getString("nic"),
                json.getString("status"));
    }

    // Parses a UserProfileResponse, returned by GET/PUT /prosumers/{nic} and
    // PATCH /prosumers/{nic}/request-deactivation. Optional fields may be null.
    public static UserProfile parseProfile(String body) throws JSONException {
        JSONObject json = new JSONObject(body);

        return new UserProfile(
                json.getString("id"),
                optionalString(json, "nic"),
                json.getString("username"),
                json.getString("fullName"),
                json.getString("email"),
                json.getString("phone"),
                optionalString(json, "address"),
                json.getString("role"),
                json.getString("status"),
                optionalString(json, "deactivationRequestedAt"));
    }

    // Reads a key that may be missing or JSON null. optString alone would turn
    // a JSON null into the text "null", which would then be stored as a NIC.
    static String optionalString(JSONObject json, String key) {
        return json.isNull(key) ? null : json.optString(key, null);
    }
}
