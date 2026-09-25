/*
 * File:    QrParser.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Converts the QR screen's JSON into models, and models into cache
 *          rows. The only place the QR vertical reads a field name, so a
 *          contract change touches this file alone. No business logic — it
 *          never decides whether anything is valid.
 */
package com.sliit.smartsolar.parsers;

import android.content.ContentValues;

import com.sliit.smartsolar.database.DbContract;
import com.sliit.smartsolar.models.ApprovedBooking;
import com.sliit.smartsolar.network.DateUtils;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Date;
import java.util.List;

public final class QrParser {

    private QrParser() {
    }

    /** What GET /api/reservations/{id}/qr returns: { token, expiresAt }. */
    public static class Token {
        public final String token;
        public final String expiresAtIso;

        public Token(String token, String expiresAtIso) {
            this.token = token;
            this.expiresAtIso = expiresAtIso;
        }
    }

    // Parses the QR token response. Returns null when the body carries no token,
    // which the caller treats as a failure rather than encoding an empty QR.
    public static Token parseToken(String json) {
        if (json == null || json.trim().isEmpty()) {
            return null;
        }

        try {
            JSONObject object = new JSONObject(json);
            String token = object.optString("token", "");
            if (token.isEmpty()) {
                return null;
            }

            // optString everywhere: an absent field is not the same as a null one.
            return new Token(token, object.isNull("expiresAt") ? null : object.optString("expiresAt"));
        } catch (Exception malformed) {
            return null;
        }
    }

    // Parses the approved reservation array. A bare array is the collection shape
    // in docs/response-format.md, so there is no envelope to unwrap.
    public static List<ApprovedBooking> parseBookings(String json) {
        List<ApprovedBooking> bookings = new ArrayList<>();

        if (json == null || json.trim().isEmpty()) {
            return bookings;
        }

        try {
            JSONArray array = new JSONArray(json);

            for (int i = 0; i < array.length(); i++) {
                JSONObject item = array.optJSONObject(i);
                if (item == null) {
                    continue;
                }

                bookings.add(new ApprovedBooking(
                        item.optString("id", ""),
                        item.optString("stationId", ""),
                        null,
                        item.isNull("slotStart") ? null : item.optString("slotStart"),
                        item.isNull("slotEnd") ? null : item.optString("slotEnd"),
                        item.optString("status", ""),
                        item.optDouble("energyKWh", 0d)));
            }
        } catch (Exception malformed) {
            return new ArrayList<>();
        }

        return bookings;
    }

    // Maps bookings to cached_bookings rows, stamping the refresh time.
    public static List<ContentValues> toCacheRows(List<ApprovedBooking> bookings) {
        List<ContentValues> rows = new ArrayList<>();
        String cachedAt = DateUtils.toUtcWire(new Date());

        for (ApprovedBooking booking : bookings) {
            ContentValues row = new ContentValues();
            row.put(DbContract.CachedBookings.RESERVATION_ID, booking.reservationId);
            row.put(DbContract.CachedBookings.STATION_ID, booking.stationId);
            row.put(DbContract.CachedBookings.STATION_NAME, booking.stationName);
            row.put(DbContract.CachedBookings.SLOT_START, booking.slotStartIso);
            row.put(DbContract.CachedBookings.SLOT_END, booking.slotEndIso);
            row.put(DbContract.CachedBookings.ENERGY_KWH, booking.energyKWh);
            row.put(DbContract.CachedBookings.STATUS, booking.status);
            row.put(DbContract.CachedBookings.CACHED_AT, cachedAt);
            rows.add(row);
        }

        return rows;
    }

    // Maps cached rows back to bookings, for rendering when the API is unreachable.
    public static List<ApprovedBooking> fromCacheRows(List<ContentValues> rows) {
        List<ApprovedBooking> bookings = new ArrayList<>();

        for (ContentValues row : rows) {
            bookings.add(new ApprovedBooking(
                    row.getAsString(DbContract.CachedBookings.RESERVATION_ID),
                    row.getAsString(DbContract.CachedBookings.STATION_ID),
                    row.getAsString(DbContract.CachedBookings.STATION_NAME),
                    row.getAsString(DbContract.CachedBookings.SLOT_START),
                    row.getAsString(DbContract.CachedBookings.SLOT_END),
                    row.getAsString(DbContract.CachedBookings.STATUS),
                    row.getAsDouble(DbContract.CachedBookings.ENERGY_KWH) == null
                            ? 0d
                            : row.getAsDouble(DbContract.CachedBookings.ENERGY_KWH)));
        }

        return bookings;
    }
}
