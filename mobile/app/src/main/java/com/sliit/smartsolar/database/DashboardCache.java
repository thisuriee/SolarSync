/*
 * File:    DashboardCache.java
 * Author:  Imadh
 * Created: 2026-09-30
 * Purpose: Stores the prosumer dashboard's figures as the API last returned
 *          them, and records when that was.
 *
 *          READ-THROUGH and never authoritative, like the node and booking
 *          caches: written only after GET /dashboard/prosumer answers, read only
 *          when it fails, and always shown with the time it was taken. The table
 *          holds one row, so saving is a snapshot in the strictest sense — the
 *          previous answer is replaced, not merged.
 *
 *          The counts are kept verbatim. Nothing here derives a count from the
 *          cached bookings: a number worked out on the phone is this phone's
 *          arithmetic over its own stale copy, not the server's answer, and
 *          storing that would quietly turn an assertion about the server into an
 *          assertion about the cache.
 */
package com.sliit.smartsolar.database;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;

import com.sliit.smartsolar.models.ProsumerDashboard;
import com.sliit.smartsolar.models.Reservation;
import com.sliit.smartsolar.network.DateUtils;
import com.sliit.smartsolar.utils.StaleLabel;

import java.util.Date;
import java.util.List;

public class DashboardCache {

    /** Key this cache is recorded under in the sync bookkeeping. */
    public static final String ENTITY = "dashboard";

    private final DatabaseHelper helper;
    private final SyncMetaDao syncMeta;

    public DashboardCache(Context context) {
        this.helper = DatabaseHelper.getInstance(context);
        this.syncMeta = new SyncMetaDao(context);
    }

    // Replaces the stored figures and stamps the time. Called only after the API
    // answered, which is what makes this a record of what the server said rather
    // than a store of what the app believes.
    public void save(ProsumerDashboard dashboard) {
        Reservation next = dashboard.nextBooking;

        ContentValues row = new ContentValues();
        row.put(DbContract.CachedDashboard.ID, DbContract.CachedDashboard.SINGLE_ROW_ID);
        row.put(DbContract.CachedDashboard.ACTIVE_COUNT, dashboard.activeCount);
        row.put(DbContract.CachedDashboard.PENDING_COUNT, dashboard.pendingCount);
        row.put(DbContract.CachedDashboard.APPROVED_FUTURE_COUNT, dashboard.approvedFutureCount);

        // A missing next booking is written as nulls rather than left behind from
        // the previous save, so "nothing upcoming" cannot be confused with a card
        // that simply was not overwritten.
        row.put(DbContract.CachedDashboard.NEXT_RESERVATION_ID, next == null ? null : next.id);
        row.put(DbContract.CachedDashboard.NEXT_STATION_ID, next == null ? null : next.stationId);
        row.put(DbContract.CachedDashboard.NEXT_SLOT_START, next == null ? null : next.slotStart);
        row.put(DbContract.CachedDashboard.NEXT_SLOT_END, next == null ? null : next.slotEnd);
        row.put(DbContract.CachedDashboard.NEXT_STATUS, next == null ? null : next.status);
        row.put(DbContract.CachedDashboard.NEXT_ENERGY_KWH, next == null ? null : next.energyKWh);

        helper.getWritableDatabase().insertWithOnConflict(
                DbContract.CachedDashboard.TABLE, null, row, SQLiteDatabase.CONFLICT_REPLACE);

        syncMeta.markSynced(ENTITY, DateUtils.toUtcWire(new Date()));
    }

    // The stored figures, or null when the dashboard has never been loaded.
    //
    // Null and "all zeroes" are different answers and the screen shows them
    // differently: null means nothing is known, zeroes mean the server said this
    // prosumer has nothing. Returning a zero-filled object for the first case
    // would invent a reassuring answer out of an absence.
    public ProsumerDashboard load() {
        Cursor cursor = helper.getReadableDatabase().query(
                DbContract.CachedDashboard.TABLE,
                null, null, null, null, null, null, "1");

        List<ContentValues> rows = DatabaseHelper.toRows(cursor);

        if (rows.isEmpty()) {
            return null;
        }

        ContentValues row = rows.get(0);

        return new ProsumerDashboard(
                asLong(row, DbContract.CachedDashboard.ACTIVE_COUNT),
                asLong(row, DbContract.CachedDashboard.PENDING_COUNT),
                asLong(row, DbContract.CachedDashboard.APPROVED_FUTURE_COUNT),
                nextBookingFrom(row));
    }

    // When the figures were last refreshed, or null when they never have been.
    public String lastRefreshedDisplay() {
        return StaleLabel.lastUpdated(syncMeta.getLastSyncedAt(ENTITY));
    }

    public void clear() {
        helper.getWritableDatabase().delete(DbContract.CachedDashboard.TABLE, null, null);
    }

    // Rebuilds the next booking from the columns kept alongside the counts, or
    // null when there was none. Only the fields a card needs were stored, so the
    // rest of the Reservation arrives null — the same display-record shape
    // BookingCache.toReservations produces.
    private static Reservation nextBookingFrom(ContentValues row) {
        String reservationId = row.getAsString(DbContract.CachedDashboard.NEXT_RESERVATION_ID);

        if (reservationId == null) {
            return null;
        }

        return new Reservation(
                reservationId,
                null,
                row.getAsString(DbContract.CachedDashboard.NEXT_STATION_ID),
                null,
                row.getAsString(DbContract.CachedDashboard.NEXT_SLOT_START),
                row.getAsString(DbContract.CachedDashboard.NEXT_SLOT_END),
                asDouble(row, DbContract.CachedDashboard.NEXT_ENERGY_KWH),
                row.getAsString(DbContract.CachedDashboard.NEXT_STATUS),
                null, null, null, null, null, null);
    }

    // SQLite has no strict typing, so each value is read defensively rather than
    // assuming the column came back as the type it was written with.
    private static long asLong(ContentValues row, String key) {
        Long value = row.getAsLong(key);
        return value == null ? 0L : value;
    }

    private static double asDouble(ContentValues row, String key) {
        Double value = row.getAsDouble(key);
        return value == null ? 0d : value;
    }
}
