/*
 * File:    ErrorParser.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Reads the standard error envelope from docs/response-format.md
 *          (type, title, status, code, detail, traceId) and exposes the code
 *          and detail a screen needs. Plumbing only.
 */
package com.sliit.smartsolar.network;

import org.json.JSONObject;

public final class ErrorParser {

    /** Code used when the request never reached the API. Not a server code. */
    public static final String CODE_NO_RESPONSE = "NO_RESPONSE";

    // Transport-only wording. Business rule failures always use the API's detail
    // so the message on screen is never authored by the client.
    private static final String NO_RESPONSE_DETAIL =
            "Could not reach the server. Check your connection and try again.";

    private ErrorParser() {
    }

    /**
     * The parts of the error envelope the clients act on: switch on code,
     * display detail, and treat 401 as "session gone" to clear and re-login.
     */
    public static class ErrorInfo {
        public final String code;
        public final String detail;
        public final int status;
        public final String title;

        public ErrorInfo(String code, String detail, int status, String title) {
            this.code = code;
            this.detail = detail;
            this.status = status;
            this.title = title;
        }
    }

    // Parses an error body. A null, empty or non-JSON body means the API was
    // never reached, which is a transport failure rather than a rule violation.
    public static ErrorInfo parse(String body) {
        if (body == null || body.trim().isEmpty()) {
            return new ErrorInfo(CODE_NO_RESPONSE, NO_RESPONSE_DETAIL, 0, "No response");
        }

        try {
            JSONObject json = new JSONObject(body);
            String code = json.optString("code", CODE_NO_RESPONSE);
            String detail = json.optString("detail", NO_RESPONSE_DETAIL);

            return new ErrorInfo(
                    code.isEmpty() ? CODE_NO_RESPONSE : code,
                    detail.isEmpty() ? NO_RESPONSE_DETAIL : detail,
                    json.optInt("status", 0),
                    json.optString("title", ""));
        } catch (Exception notJson) {
            // A non-JSON error body is a proxy or transport artefact, not a
            // problem object, so fall back to the transport wording.
            return new ErrorInfo(CODE_NO_RESPONSE, NO_RESPONSE_DETAIL, 0, "No response");
        }
    }
}
