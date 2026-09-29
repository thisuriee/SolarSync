/*
 * File:    HistoryQueryTest.java
 * Author:  Imadh
 * Created: 2026-09-30
 * Purpose: Proves the history date range without a device. HistoryQuery is pure
 *          Java (Calendar, SimpleDateFormat), so a JVM test can pin the device
 *          zone and check the instants it produces.
 *
 *          It is the only part of the history screen that can be verified
 *          off-device, and it is the part most worth verifying: Sri Lanka is
 *          UTC+5:30, so local midnight is the previous evening in UTC, and a
 *          range built from a UTC midnight would select the wrong day's
 *          bookings while looking entirely plausible on screen.
 */
package com.sliit.smartsolar.utils;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import com.sliit.smartsolar.network.DateUtils;

import org.junit.After;
import org.junit.Before;
import org.junit.Test;

import java.util.Calendar;
import java.util.Date;
import java.util.TimeZone;

public class HistoryQueryTest {

    private TimeZone originalZone;

    @Before
    public void pinDeviceZone() {
        originalZone = TimeZone.getDefault();
        TimeZone.setDefault(TimeZone.getTimeZone("Asia/Colombo"));
    }

    @After
    public void restoreDeviceZone() {
        TimeZone.setDefault(originalZone);
    }

    // 30 Sep 2026, 00:00 local (UTC+5:30) is 29 Sep 2026, 18:30 UTC. The day
    // boundary is the previous day in UTC, which is exactly what makes this
    // worth a test.
    @Test
    public void dayStartUtc_IsThePreviousEveningInUtc() {
        assertEquals("2026-09-29T18:30:00.000Z", HistoryQuery.dayStartUtc(localDayOf(2026, Calendar.SEPTEMBER, 30)));
    }

    // The API's `to` bound is inclusive, so the range has to reach the last
    // millisecond of the chosen local day.
    @Test
    public void dayEndUtc_IsTheLastInstantOfTheLocalDay() {
        assertEquals("2026-09-30T18:29:59.999Z", HistoryQuery.dayEndUtc(localDayOf(2026, Calendar.SEPTEMBER, 30)));
    }

    // Together the two bounds span the whole local day and not a millisecond
    // more, so a booking either side of midnight falls on the right side.
    @Test
    public void dayStartAndEnd_SpanExactlyOneLocalDay() {
        Date day = localDayOf(2026, Calendar.SEPTEMBER, 30);

        long spanMillis = DateUtils.parseUtc(HistoryQuery.dayEndUtc(day)).getTime()
                - DateUtils.parseUtc(HistoryQuery.dayStartUtc(day)).getTime();

        assertEquals(86_399_999L, spanMillis);
    }

    // The offset is read from the device zone rather than assumed, so the helper
    // is not quietly hardcoded to +5:30.
    @Test
    public void dayStartUtc_UnderUtc_IsMidnightUtc() {
        TimeZone.setDefault(TimeZone.getTimeZone("UTC"));

        assertEquals("2026-09-30T00:00:00.000Z", HistoryQuery.dayStartUtc(localDayOf(2026, Calendar.SEPTEMBER, 30)));
    }

    @Test
    public void isUnfiltered_IsTrueWhenNothingIsNarrowed() {
        assertTrue(HistoryQuery.isUnfiltered(null, null, null, null));
        assertTrue(HistoryQuery.isUnfiltered(null, null, null, "   "));
    }

    @Test
    public void isUnfiltered_IsFalseForEachFilterOnItsOwn() {
        Date day = localDayOf(2026, Calendar.SEPTEMBER, 30);

        assertFalse(HistoryQuery.isUnfiltered("Approved", null, null, null));
        assertFalse(HistoryQuery.isUnfiltered(null, day, null, null));
        assertFalse(HistoryQuery.isUnfiltered(null, null, day, null));
        assertFalse(HistoryQuery.isUnfiltered(null, null, null, "Colombo"));
    }

    // A Date carrying a time of day: dayStartUtc has to ignore it, because the
    // picker supplies a calendar day and nothing finer.
    private static Date localDayOf(int year, int month, int dayOfMonth) {
        Calendar calendar = Calendar.getInstance();
        calendar.set(year, month, dayOfMonth, 13, 45, 30);
        calendar.set(Calendar.MILLISECOND, 123);
        return calendar.getTime();
    }
}
