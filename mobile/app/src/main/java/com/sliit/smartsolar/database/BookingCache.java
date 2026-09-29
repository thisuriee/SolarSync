/*
 * File:    BookingCache.java
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: Maps between the reservation models and the cached_bookings table,
 *          and records when that cache was last refreshed.
 *
 *          The cache is READ-THROUGH and never authoritative. A screen calls
 *          the API first and renders the response; this is written only after
 *          that call succeeds, and read only when one fails. Nothing decides
 *          anything from it — a stale row is shown with the time it was taken,
 *          never treated as current, and nothing is ever written that the
 *          server did not send. There is no queue of pending writes.
 *
 *          The table is a snapshot, not an append log: save() replaces every
 *          row, so a booking cancelled elsewhere disappears from the cache on
 *          the next successful read instead of lingering beside the live data.
 *
 *          It deliberately does not store everything a Reservation holds — only
 *          the columns a list needs. A reservation rebuilt from cache is
 *          therefore a display record: the audit trail and the identity fields
 *          are not kept, and a screen that needs them asks the API.
 */
package com.sliit.smartsolar.database;

import android.content.ContentValues;
import android.content.Context;

import com.sliit.smartsolar.models.NodeOption;
import com.sliit.smartsolar.models.Reservation;
import com.sliit.smartsolar.network.DateUtils;
import com.sliit.smartsolar.utils.StaleLabel;

import java.util.ArrayList;
import java.util.Date;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public class BookingCache {

    /** Key this cache is recorded under in the sync bookkeeping. */
    public static final String ENTITY = "bookings";

    private final BookingDao bookings;
    private final SyncMetaDao syncMeta;

    public BookingCache(Context context) {
        this.bookings = new BookingDao(context);
        this.syncMeta = new SyncMetaDao(context);
    }

    // Replaces the cache with a freshly fetched list and stamps the time.
    // Called only after the API answered, which is what makes this a cache of
    // what the server said rather than a store of what the app believes.
    public void save(List<ContentValues> rows) {
        bookings.replaceAll(rows);
        syncMeta.markSynced(ENTITY, DateUtils.toUtcWire(new Date()));
    }

    // Everything cached, newest slot first — the order the table already returns.
    public List<ContentValues> load() {
        return bookings.findAll();
    }

    // The cached rows in one status, soonest slot first.
    public List<ContentValues> loadByStatus(String status) {
        return bookings.findByStatus(status);
    }

    // When the cache was last refreshed, or null when it never has been. Null
    // is how a screen tells "nothing has ever been cached" apart from "cached,
    // and the server's answer was that there is nothing to show".
    public String lastRefreshedDisplay() {
        return StaleLabel.lastUpdated(syncMeta.getLastSyncedAt(ENTITY));
    }

    public int count() {
        return bookings.count();
    }

    public void clear() {
        bookings.clear();
    }

    // Maps a freshly fetched booking list onto cached_bookings rows. This is now
    // the only writer of that table, so the row contract is defined here — and
    // it is the contract QrParser.fromCacheRows reads back.
    public static List<ContentValues> rowsFor(
            List<Reservation> reservations, Map<String, String> stationNamesById) {
        List<ContentValues> rows = new ArrayList<>(reservations.size());
        String cachedAt = DateUtils.toUtcWire(new Date());

        for (Reservation reservation : reservations) {
            ContentValues row = new ContentValues();
            row.put(DbContract.CachedBookings.RESERVATION_ID, reservation.id);
            row.put(DbContract.CachedBookings.STATION_ID, reservation.stationId);
            row.put(DbContract.CachedBookings.STATION_NAME, nameFor(stationNamesById, reservation.stationId));
            row.put(DbContract.CachedBookings.SLOT_START, reservation.slotStart);
            row.put(DbContract.CachedBookings.SLOT_END, reservation.slotEnd);
            row.put(DbContract.CachedBookings.ENERGY_KWH, reservation.energyKWh);
            row.put(DbContract.CachedBookings.STATUS, reservation.status);
            row.put(DbContract.CachedBookings.CACHED_AT, cachedAt);
            rows.add(row);
        }

        return rows;
    }

    // Rebuilds reservations from cached rows, for rendering when the API is
    // unreachable. Only the columns above come back, so the audit trail and the
    // identity fields arrive null: they were never stored, and nothing is
    // inferred to fill them in.
    public static List<Reservation> toReservations(List<ContentValues> rows) {
        List<Reservation> reservations = new ArrayList<>(rows.size());

        for (ContentValues row : rows) {
            reservations.add(new Reservation(
                    asString(row, DbContract.CachedBookings.RESERVATION_ID),
                    null,
                    asString(row, DbContract.CachedBookings.STATION_ID),
                    null,
                    asString(row, DbContract.CachedBookings.SLOT_START),
                    asString(row, DbContract.CachedBookings.SLOT_END),
                    asDouble(row, DbContract.CachedBookings.ENERGY_KWH),
                    asString(row, DbContract.CachedBookings.STATUS),
                    null, null, null, null, null, null));
        }

        return reservations;
    }

    // The stations the cached rows mention, so a cached list can still name its
    // nodes without a second read. This is what makes station_name worth
    // storing: without it a cached row would fall back to showing a short id.
    public static List<NodeOption> nodeOptions(List<ContentValues> rows) {
        Map<String, NodeOption> byId = new LinkedHashMap<>();

        for (ContentValues row : rows) {
            String stationId = row.getAsString(DbContract.CachedBookings.STATION_ID);
            if (stationId == null || byId.containsKey(stationId)) {
                continue;
            }

            // The address line is not part of this table, so it stays empty
            // rather than being invented.
            byId.put(stationId, new NodeOption(
                    stationId,
                    asString(row, DbContract.CachedBookings.STATION_NAME),
                    ""));
        }

        return new ArrayList<>(byId.values());
    }

    private static String nameFor(Map<String, String> stationNamesById, String stationId) {
        if (stationNamesById == null || stationId == null) {
            return "";
        }

        String name = stationNamesById.get(stationId);
        return name == null ? "" : name;
    }

    // SQLite has no strict typing, so each value is read defensively rather
    // than assuming the column came back as the type it was written with.
    private static String asString(ContentValues row, String key) {
        String value = row.getAsString(key);
        return value == null ? "" : value;
    }

    private static double asDouble(ContentValues row, String key) {
        Double value = row.getAsDouble(key);
        return value == null ? 0d : value;
    }
}
