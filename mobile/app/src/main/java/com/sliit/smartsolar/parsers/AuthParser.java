/*
 * File:    AuthParser.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Turns the identity vertical's success bodies (docs/api-contract.md §2)
 *          into model objects with org.json. Parsing only — no decisions.
 */
package com.sliit.smartsolar.parsers;

import com.sliit.smartsolar.models.LoginResult;

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

    // Reads a key that may be missing or JSON null. optString alone would turn
    // a JSON null into the text "null", which would then be stored as a NIC.
    static String optionalString(JSONObject json, String key) {
        return json.isNull(key) ? null : json.optString(key, null);
    }
}
