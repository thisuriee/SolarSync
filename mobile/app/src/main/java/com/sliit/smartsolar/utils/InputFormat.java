/*
 * File:    InputFormat.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Field-shape checks for the prosumer forms, copied from the data
 *          annotations on the API's RegisterProsumerRequest.cs. They exist
 *          because the API's automatic 400 for a malformed field carries no
 *          code/detail, which ErrorParser would show as "Could not reach the
 *          server". Shape only: NIC format and uniqueness, and every other
 *          business rule, are decided by the API alone.
 */
package com.sliit.smartsolar.utils;

import android.util.Patterns;

import com.sliit.smartsolar.R;

import java.util.regex.Pattern;

public final class InputFormat {

    /** Returned by every check when the value is acceptable. */
    public static final int OK = 0;

    // Same patterns and limits as RegisterProsumerRequest.cs. If the API's
    // annotations change, these must change with them.
    private static final Pattern PHONE = Pattern.compile("^\\+?[0-9]{9,15}$");
    private static final Pattern USERNAME = Pattern.compile("^[A-Za-z0-9._]{3,30}$");

    private InputFormat() {
    }

    // Any required field: non-blank.
    public static int required(String value) {
        return value.isEmpty() ? R.string.error_required : OK;
    }

    // [Required, StringLength(100, MinimumLength = 2)]
    public static int fullName(String value) {
        return lengthBetween(value, 2, 100, R.string.error_full_name);
    }

    // [Required, EmailAddress]
    public static int email(String value) {
        if (value.isEmpty()) {
            return R.string.error_required;
        }
        return Patterns.EMAIL_ADDRESS.matcher(value).matches() ? OK : R.string.error_email;
    }

    // [Required, RegularExpression(@"^\+?[0-9]{9,15}$")]
    public static int phone(String value) {
        if (value.isEmpty()) {
            return R.string.error_required;
        }
        return PHONE.matcher(value).matches() ? OK : R.string.error_phone;
    }

    // [Required, StringLength(200, MinimumLength = 3)]
    public static int address(String value) {
        return lengthBetween(value, 3, 200, R.string.error_address);
    }

    // [Required, RegularExpression(@"^[A-Za-z0-9._]{3,30}$")]
    public static int username(String value) {
        if (value.isEmpty()) {
            return R.string.error_required;
        }
        return USERNAME.matcher(value).matches() ? OK : R.string.error_username;
    }

    // [Required, StringLength(100, MinimumLength = 8)]
    public static int password(String value) {
        return lengthBetween(value, 8, 100, R.string.error_password);
    }

    // Required plus a length range, as StringLength does on the API.
    private static int lengthBetween(String value, int min, int max, int errorRes) {
        if (value.isEmpty()) {
            return R.string.error_required;
        }
        return value.length() >= min && value.length() <= max ? OK : errorRes;
    }
}
