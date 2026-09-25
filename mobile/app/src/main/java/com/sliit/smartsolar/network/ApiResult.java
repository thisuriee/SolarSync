/*
 * File:    ApiResult.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Carries the raw outcome of one HTTP call — status code and body —
 *          with no parsing and no business decisions. Plumbing only.
 */
package com.sliit.smartsolar.network;

public class ApiResult {

    /** HTTP status returned by the API, or 0 when the request never completed. */
    public final int statusCode;

    /** Raw response body, or null when there was none. Never interpreted here. */
    public final String body;

    /** True for 2xx only. A 0 status (transport failure) is never successful. */
    public final boolean isSuccess;

    // Classifies the status mechanically. This is transport plumbing, not a
    // business rule — the API decides whether the request was legitimate.
    public ApiResult(int statusCode, String body) {
        this.statusCode = statusCode;
        this.body = body;
        this.isSuccess = statusCode >= 200 && statusCode < 300;
    }
}
