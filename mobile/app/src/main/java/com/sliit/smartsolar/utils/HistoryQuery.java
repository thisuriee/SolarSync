/*
 * File:    HistoryQuery.java
 * Author:  Imadh
 * Created: 2026-09-30
 * Purpose: The two parts of GET /reservations/history that are easy to get
 *          wrong: turning a calendar day the user picked into the instant range
 *          the API expects, and deciding whether a request is still unfiltered.
 *
 *          Deliberately free of Android types so it can be unit-tested on its
 *          own, because the trap here is invisible. Sri Lanka is UTC+5:30, so
 *          local midnight on the 30th is 18:30 on the 29th in UTC; a range built
 *          from a UTC midnight would quietly select the wrong day's bookings and
 *          look entirely plausible on screen.
 *
 *          SlotSearchActivity sends no date filter at all, because the API reads
 *          its `date` parameter as a UTC day rather than a local one. This screen
 *          is different: `from` and `to` are full instants the API treats as
 *          UTC, so sending the UTC bounds of the chosen local day is exact and
 *          that problem does not arise.
 */
package com.sliit.smartsolar.utils;

import com.sliit.smartsolar.network.DateUtils;

import java.util.Calendar;
import java.util.Date;

public final class HistoryQuery {

    /** Rows per page. Matches the ceiling the API clamps a page size to. */
    public static final int PAGE_SIZE = 20;

    private HistoryQuery() {
    }

    // The instant the given local day begins, on the wire as the API expects it.
    public static String dayStartUtc(Date localDay) {
        return DateUtils.toUtcWire(startOfLocalDay(localDay));
    }

    // The last millisecond of the given local day. The API's `to` bound is
    // inclusive, so a booking late on the chosen evening has to fall inside it.
    public static String dayEndUtc(Date localDay) {
        Calendar calendar = Calendar.getInstance();
        calendar.setTime(startOfLocalDay(localDay));
        calendar.add(Calendar.DAY_OF_MONTH, 1);
        calendar.add(Calendar.MILLISECOND, -1);
        return DateUtils.toUtcWire(calendar.getTime());
    }

    // True when the request narrows nothing, which is the only case where the
    // cache may stand in for the server.
    //
    // A filtered request must never be answered from the cache. The cached rows
    // were fetched without these filters, so applying them here would answer a
    // question the server was never asked and present the result as though it
    // had been.
    public static boolean isUnfiltered(String status, Date from, Date to, String search) {
        return status == null
                && from == null
                && to == null
                && (search == null || search.trim().isEmpty());
    }

    // Local midnight. Calendar reads the device's own zone, so this is the
    // instant the user means by "the 30th" rather than midnight somewhere else.
    private static Date startOfLocalDay(Date localDay) {
        Calendar calendar = Calendar.getInstance();
        calendar.setTime(localDay);
        calendar.set(Calendar.HOUR_OF_DAY, 0);
        calendar.set(Calendar.MINUTE, 0);
        calendar.set(Calendar.SECOND, 0);
        calendar.set(Calendar.MILLISECOND, 0);
        return calendar.getTime();
    }
}
