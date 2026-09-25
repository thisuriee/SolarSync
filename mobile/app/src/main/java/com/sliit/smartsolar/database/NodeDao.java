/*
 * File:    NodeDao.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Reads and writes the cached node list. Read-through only: every
 *          screen calls the API first and overwrites this cache on success.
 *          Rendered only when the API call fails. Plumbing only. Owner: M2.
 */
package com.sliit.smartsolar.database;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;

import java.util.List;

public class NodeDao {

    private final DatabaseHelper helper;

    public NodeDao(Context context) {
        this.helper = DatabaseHelper.getInstance(context);
    }

    // Inserts or replaces one node row, keyed on station_id.
    public void upsert(ContentValues values) {
        helper.getWritableDatabase().insertWithOnConflict(
                DbContract.CachedNodes.TABLE, null, values, SQLiteDatabase.CONFLICT_REPLACE);
    }

    // Replaces the whole node cache in one transaction, so a failed refresh
    // cannot leave a half-updated list behind.
    public void replaceAll(List<ContentValues> rows) {
        SQLiteDatabase db = helper.getWritableDatabase();
        db.beginTransaction();

        try {
            db.delete(DbContract.CachedNodes.TABLE, null, null);
            for (ContentValues row : rows) {
                db.insertWithOnConflict(
                        DbContract.CachedNodes.TABLE, null, row, SQLiteDatabase.CONFLICT_REPLACE);
            }
            db.setTransactionSuccessful();
        } finally {
            db.endTransaction();
        }
    }

    // Returns every cached node, or an empty list when nothing is cached.
    public List<ContentValues> findAll() {
        Cursor cursor = helper.getReadableDatabase().query(
                DbContract.CachedNodes.TABLE, null, null, null, null, null,
                DbContract.CachedNodes.STATION_NAME + " ASC");

        return DatabaseHelper.toRows(cursor);
    }

    // Returns one cached node by station id, or null when it is not cached.
    public ContentValues findById(String stationId) {
        Cursor cursor = helper.getReadableDatabase().query(
                DbContract.CachedNodes.TABLE, null,
                DbContract.CachedNodes.STATION_ID + " = ?",
                new String[]{stationId}, null, null, null, "1");

        List<ContentValues> rows = DatabaseHelper.toRows(cursor);
        return rows.isEmpty() ? null : rows.get(0);
    }

    // Number of cached nodes, so a screen can tell "empty cache" from "no data".
    public int count() {
        Cursor cursor = helper.getReadableDatabase().rawQuery(
                "SELECT COUNT(*) FROM " + DbContract.CachedNodes.TABLE, null);

        try {
            return cursor.moveToFirst() ? cursor.getInt(0) : 0;
        } finally {
            cursor.close();
        }
    }

    // Empties the node cache.
    public void clear() {
        helper.getWritableDatabase().delete(DbContract.CachedNodes.TABLE, null, null);
    }
}
