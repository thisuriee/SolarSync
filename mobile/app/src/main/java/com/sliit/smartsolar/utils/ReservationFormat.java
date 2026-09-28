/*
 * File:    ReservationFormat.java
 * Author:  Thisuri
 * Created: 2026-09-28
 * Purpose: Display formatting shared by every reservation screen, so a slot
 *          time, an energy amount or a node name looks the same on all of
 *          them. Formatting only: nothing here decides whether a booking is
 *          allowed — the 7-day and 12-hour rules live in the API alone.
 */
package com.sliit.smartsolar.utils;

import android.content.Context;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;

import com.sliit.smartsolar.R;
import com.sliit.smartsolar.models.NodeOption;
import com.sliit.smartsolar.network.DateUtils;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.List;
import java.util.Locale;
import java.util.TimeZone;

public final class ReservationFormat {

    private static final String DAY_PATTERN = "EEE, dd MMM yyyy";
    private static final String TIME_PATTERN = "HH:mm";

    private ReservationFormat() {
    }

    // "28 Sep 2026, 14:00 – 16:00" in the device's local time. Both ends are
    // UTC on the wire; the end shows only its time as windows sit in one day.
    public static String slotRange(String startUtc, String endUtc) {
        Date end = DateUtils.parseUtc(endUtc);
        return DateUtils.toLocalDisplay(startUtc) + " – " + (end == null ? "" : localTime(end));
    }

    // "Mon, 28 Sep 2026" — the local calendar day a slot starts on, used as the
    // section header in the slot list.
    public static String localDay(String isoUtc) {
        Date date = DateUtils.parseUtc(isoUtc);
        if (date == null) {
            return "";
        }
        SimpleDateFormat format = new SimpleDateFormat(DAY_PATTERN, Locale.getDefault());
        format.setTimeZone(TimeZone.getDefault());
        return format.format(date);
    }

    // "4.5 kWh". Trailing zeros dropped so 5.0 reads as "5".
    public static String energy(Context context, double kWh) {
        return context.getString(R.string.reservation_energy_value, plainNumber(kWh));
    }

    // "5" rather than "5.0"; used for the label and to pre-fill the edit field.
    public static String plainNumber(double value) {
        return value == Math.rint(value) ? String.valueOf((long) value) : String.valueOf(value);
    }

    // Reads the energy field: a finite number, or null when the text is not
    // one. Shape only — a comma is accepted as the decimal mark for locales
    // that type one. Whether the amount is allowed is the API's decision.
    public static Double parseEnergy(String text) {
        try {
            double value = Double.parseDouble(text.trim().replace(',', '.'));
            return Double.isNaN(value) || Double.isInfinite(value) ? null : value;
        } catch (NumberFormatException e) {
            return null;
        }
    }

    // How long until the slot starts ("3 h 20 min"), or null once it has
    // started. A fact for the user to read, not a rule: whether that is still
    // enough notice to change or cancel is answered by the API.
    public static String timeUntil(Context context, String startUtc) {
        Date start = DateUtils.parseUtc(startUtc);
        if (start == null) {
            return null;
        }

        long minutes = (start.getTime() - System.currentTimeMillis()) / 60_000L;
        if (minutes <= 0) {
            return null;
        }

        long days = minutes / (24 * 60);
        long hours = (minutes / 60) % 24;
        long mins = minutes % 60;

        if (days > 0) {
            return context.getString(R.string.duration_days_hours, days, hours);
        }
        return context.getString(R.string.duration_hours_minutes, hours, mins);
    }

    // The node's name for a stationId, or a short form of the id when the node
    // is not in the list (e.g. it has since been taken out of service and the
    // API no longer lists it to prosumers).
    public static String nodeName(Context context, List<NodeOption> nodes, String stationId) {
        if (nodes != null) {
            for (NodeOption node : nodes) {
                if (node.id.equals(stationId)) {
                    return node.stationName;
                }
            }
        }
        return context.getString(R.string.reservation_node_fallback, shortId(stationId));
    }

    // Last six characters of an id, upper-case — enough to tell bookings apart
    // on screen and to quote to support, without printing a 24-char ObjectId.
    public static String shortId(String id) {
        if (id == null) {
            return "";
        }
        return (id.length() <= 6 ? id : id.substring(id.length() - 6)).toUpperCase(Locale.US);
    }

    // Appends one label / value line (item_detail_row) to a details card. A
    // null or empty value adds nothing, so an audit step that has not happened
    // (never approved, never cancelled) simply does not appear.
    public static void addDetailRow(LinearLayout container, int labelRes, String value) {
        if (value == null || value.isEmpty()) {
            return;
        }

        View row = LayoutInflater.from(container.getContext())
                .inflate(R.layout.item_detail_row, container, false);
        ((TextView) row.findViewById(R.id.textLabel)).setText(labelRes);
        ((TextView) row.findViewById(R.id.textValue)).setText(value);
        container.addView(row);
    }

    // Local "HH:mm" for an instant.
    private static String localTime(Date date) {
        SimpleDateFormat format = new SimpleDateFormat(TIME_PATTERN, Locale.getDefault());
        format.setTimeZone(TimeZone.getDefault());
        return format.format(date);
    }
}
