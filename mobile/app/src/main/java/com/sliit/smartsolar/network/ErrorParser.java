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

    /** Code used when the API answered but sent no problem object. Not a server code. */
    public static final String CODE_NO_PROBLEM_BODY = "NO_PROBLEM_BODY";

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

    // Parses an error body together with the status that came with it.
    //
    // Three cases that must not be conflated:
    //   - a problem object       -> the API's own code and detail
    //   - a status but no body   -> the server WAS reached: a routing 404 or an
    //                               empty response. Reporting "could not reach
    //                               the server" here sends debugging the wrong way
    //   - no status at all       -> the request never completed
    public static ErrorInfo parse(String body, int statusCode) {
        if (body != null && !body.trim().isEmpty()) {
            try {
                JSONObject json = new JSONObject(body);
                String code = json.optString("code", "");
                String detail = json.optString("detail", "");

                if (!code.isEmpty() || !detail.isEmpty()) {
                    return new ErrorInfo(
                            code.isEmpty() ? CODE_NO_PROBLEM_BODY : code,
                            detail.isEmpty() ? detailFor(statusCode) : detail,
                            json.optInt("status", statusCode),
                            json.optString("title", ""));
                }
            } catch (Exception notJson) {
                // Fall through: a proxy page or transport artefact, not a
                // problem object.
            }
        }

        if (statusCode > 0) {
            return new ErrorInfo(CODE_NO_PROBLEM_BODY, detailFor(statusCode), statusCode, "");
        }

        return new ErrorInfo(CODE_NO_RESPONSE, NO_RESPONSE_DETAIL, 0, "No response");
    }

    // Wording for a response carrying no problem object. This is not a business
    // rule message: no rule failed, the API simply sent no detail.
    private static String detailFor(int statusCode) {
        return "The server returned HTTP " + statusCode + " without an error detail.";
    }
}
