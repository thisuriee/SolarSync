/*
 * File:    DateUtils.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Converts between the API's UTC wire format and local display time.
 *
 *          Two traps this exists to prevent:
 *          1. Every SimpleDateFormat here is pinned to UTC or to the device
 *             zone explicitly. Sri Lanka is UTC+5:30, so an unpinned parser
 *             shifts every timestamp and makes the 12-hour rule look broken in
 *             the UI while the server is correct.
 *          2. The API writes fractional seconds with variable precision —
 *             "2026-09-26T16:35:09Z", "...09.8Z" and "...09.1234567Z" are all
 *             produced by System.Text.Json. A fixed "...ss'Z'" pattern throws
 *             ParseException on two of those three, so input is normalised to
 *             exactly three digits first.
 */
package com.sliit.smartsolar.network;

import java.text.ParseException;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public final class DateUtils {

    private static final TimeZone UTC = TimeZone.getTimeZone("UTC");

    // Wire format, always three fraction digits. Locale.US keeps the digits
    // ASCII regardless of device locale.
    private static final String WIRE_PATTERN = "yyyy-MM-dd'T'HH:mm:ss.SSS'Z'";

    private static final String DISPLAY_PATTERN = "dd MMM yyyy, HH:mm";

    // Seconds, optional fractional part of any length, trailing Z.
    private static final Pattern WIRE_INPUT =
            Pattern.compile("^(\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2})(?:\\.(\\d+))?Z$");

    private DateUtils() {
    }

    // Parses an ISO-8601 UTC timestamp into a Date. Returns null for null, blank
    // or unrecognised input so a caller shows nothing rather than a wrong time.
    public static Date parseUtc(String isoUtc) {
        String normalised = normalise(isoUtc);
        if (normalised == null) {
            return null;
        }

        // SimpleDateFormat is not thread-safe, so a fresh instance per call.
        SimpleDateFormat format = new SimpleDateFormat(WIRE_PATTERN, Locale.US);
        format.setTimeZone(UTC);

        try {
            return format.parse(normalised);
        } catch (ParseException e) {
            return null;
        }
    }

    // Formats a UTC timestamp for display in the device's local time.
    public static String toLocalDisplay(String isoUtc) {
        Date date = parseUtc(isoUtc);
        return date == null ? "" : toLocalDisplay(date);
    }

    // Formats an already-parsed instant for display in the device's local time.
    public static String toLocalDisplay(Date date) {
        if (date == null) {
            return "";
        }

        SimpleDateFormat format = new SimpleDateFormat(DISPLAY_PATTERN, Locale.getDefault());
        format.setTimeZone(TimeZone.getDefault());
        return format.format(date);
    }

    // Formats an instant for sending to the API. Always UTC, always trailing Z.
    public static String toUtcWire(Date date) {
        if (date == null) {
            return null;
        }

        SimpleDateFormat format = new SimpleDateFormat(WIRE_PATTERN, Locale.US);
        format.setTimeZone(UTC);
        return format.format(date);
    }

    // Normalises "...T16:35:09Z", "...T16:35:09.8Z" and "...T16:35:09.1234567Z"
    // to a common three-digit form. Returns null when the shape is not the
    // contract's (docs/api-contract.md: ISO-8601 UTC with a trailing Z).
    private static String normalise(String isoUtc) {
        if (isoUtc == null) {
            return null;
        }

        Matcher matcher = WIRE_INPUT.matcher(isoUtc.trim());
        if (!matcher.matches()) {
            return null;
        }

        String seconds = matcher.group(1);
        String fraction = matcher.group(2);

        if (fraction == null) {
            return seconds + ".000Z";
        }

        // Pad to three digits; truncate anything longer (7 digits from .NET).
        String millis = (fraction + "000").substring(0, 3);
        return seconds + "." + millis + "Z";
    }
}
