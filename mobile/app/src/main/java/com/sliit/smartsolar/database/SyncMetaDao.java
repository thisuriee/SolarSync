/*
 * File:    SyncMetaDao.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Tracks when each cache entity was last refreshed, so screens can
 *          label cached counts as stale ("Last updated 14:32"). Plumbing only.
 */
package com.sliit.smartsolar.database;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;

import java.util.List;

public class SyncMetaDao {

    private final DatabaseHelper helper;

    public SyncMetaDao(Context context) {
        this.helper = DatabaseHelper.getInstance(context);
    }

    // Records the UTC timestamp of the last successful refresh for one entity.
    public void markSynced(String entity, String syncedAtUtc) {
        ContentValues values = new ContentValues();
        values.put(DbContract.SyncMeta.ENTITY, entity);
        values.put(DbContract.SyncMeta.LAST_SYNCED_AT, syncedAtUtc);

        helper.getWritableDatabase().insertWithOnConflict(
                DbContract.SyncMeta.TABLE, null, values, SQLiteDatabase.CONFLICT_REPLACE);
    }

    // Returns the last refresh timestamp for one entity, or null if never synced.
    public String getLastSyncedAt(String entity) {
        Cursor cursor = helper.getReadableDatabase().query(
                DbContract.SyncMeta.TABLE,
                new String[]{DbContract.SyncMeta.LAST_SYNCED_AT},
                DbContract.SyncMeta.ENTITY + " = ?",
                new String[]{entity},
                null, null, null, "1");

        List<ContentValues> rows = DatabaseHelper.toRows(cursor);
        return rows.isEmpty() ? null : rows.get(0).getAsString(DbContract.SyncMeta.LAST_SYNCED_AT);
    }

    // Clears the sync bookkeeping. Called together with clearAll() on logout.
    public void clear() {
        helper.getWritableDatabase().delete(DbContract.SyncMeta.TABLE, null, null);
    }
}
