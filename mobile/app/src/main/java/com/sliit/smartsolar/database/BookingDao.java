/*
 * File:    BookingDao.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Reads and writes the cached booking list. Read-through only, and
 *          cleared on logout. Never a source of truth for capacity or status —
 *          live counts come from the API, and stale ones are labelled as such.
 *          Plumbing only. Owner: M4.
 */
package com.sliit.smartsolar.database;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;

import java.util.List;

public class BookingDao {

    private final DatabaseHelper helper;

    public BookingDao(Context context) {
        this.helper = DatabaseHelper.getInstance(context);
    }

    // Inserts or replaces one booking row, keyed on reservation_id.
    public void upsert(ContentValues values) {
        helper.getWritableDatabase().insertWithOnConflict(
                DbContract.CachedBookings.TABLE, null, values, SQLiteDatabase.CONFLICT_REPLACE);
    }

    // Replaces the whole booking cache in one transaction.
    public void replaceAll(List<ContentValues> rows) {
        SQLiteDatabase db = helper.getWritableDatabase();
        db.beginTransaction();

        try {
            db.delete(DbContract.CachedBookings.TABLE, null, null);
            for (ContentValues row : rows) {
                db.insertWithOnConflict(
                        DbContract.CachedBookings.TABLE, null, row, SQLiteDatabase.CONFLICT_REPLACE);
            }
            db.setTransactionSuccessful();
        } finally {
            db.endTransaction();
        }
    }

    // Returns every cached booking, most recent slot first.
    public List<ContentValues> findAll() {
        Cursor cursor = helper.getReadableDatabase().query(
                DbContract.CachedBookings.TABLE, null, null, null, null, null,
                DbContract.CachedBookings.SLOT_START + " DESC");

        return DatabaseHelper.toRows(cursor);
    }

    // Returns cached bookings in one status, e.g. "Approved". Status comparison
    // is a filter on cached data, not a rule — the API still decides.
    public List<ContentValues> findByStatus(String status) {
        Cursor cursor = helper.getReadableDatabase().query(
                DbContract.CachedBookings.TABLE, null,
                DbContract.CachedBookings.STATUS + " = ?",
                new String[]{status}, null, null,
                DbContract.CachedBookings.SLOT_START + " ASC");

        return DatabaseHelper.toRows(cursor);
    }

    // Returns one cached booking by reservation id, or null when not cached.
    public ContentValues findById(String reservationId) {
        Cursor cursor = helper.getReadableDatabase().query(
                DbContract.CachedBookings.TABLE, null,
                DbContract.CachedBookings.RESERVATION_ID + " = ?",
                new String[]{reservationId}, null, null, null, "1");

        List<ContentValues> rows = DatabaseHelper.toRows(cursor);
        return rows.isEmpty() ? null : rows.get(0);
    }

    // Number of cached bookings, so a screen can tell "empty cache" from "none".
    public int count() {
        Cursor cursor = helper.getReadableDatabase().rawQuery(
                "SELECT COUNT(*) FROM " + DbContract.CachedBookings.TABLE, null);

        try {
            return cursor.moveToFirst() ? cursor.getInt(0) : 0;
        } finally {
            cursor.close();
        }
    }

    // Empties the booking cache.
    public void clear() {
        helper.getWritableDatabase().delete(DbContract.CachedBookings.TABLE, null, null);
    }
}
