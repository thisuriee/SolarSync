/*
 * File:    DatabaseHelper.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Owns the local SQLite cache: creates the four tables, handles
 *          upgrades, and clears everything on logout. Plumbing only — no
 *          business logic, and no rule is ever decided from this data.
 */
package com.sliit.smartsolar.database;

import android.content.Context;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;

public class DatabaseHelper extends SQLiteOpenHelper {

    private static volatile DatabaseHelper instance;

    // One connection per process. Creating a helper per call leaks cursors and
    // serialises poorly once the cache tables are written from several screens.
    public static DatabaseHelper getInstance(Context context) {
        if (instance == null) {
            synchronized (DatabaseHelper.class) {
                if (instance == null) {
                    instance = new DatabaseHelper(context.getApplicationContext());
                }
            }
        }
        return instance;
    }

    private DatabaseHelper(Context context) {
        super(context, DbContract.DB_NAME, null, DbContract.DB_VERSION);
    }

    // Creates the four tables. The session table holds the token only — never a
    // password, plaintext or hashed.
    @Override
    public void onCreate(SQLiteDatabase db) {
        db.execSQL("CREATE TABLE " + DbContract.Session.TABLE + " ("
                + DbContract.Session.ID + " INTEGER PRIMARY KEY, "
                + DbContract.Session.USER_ID + " TEXT, "
                + DbContract.Session.USERNAME + " TEXT, "
                + DbContract.Session.NIC + " TEXT, "
                + DbContract.Session.FULL_NAME + " TEXT, "
                + DbContract.Session.ROLE + " TEXT, "
                + DbContract.Session.TOKEN + " TEXT, "
                + DbContract.Session.ISSUED_AT + " TEXT, "
                + DbContract.Session.EXPIRES_AT + " TEXT)");

        db.execSQL("CREATE TABLE " + DbContract.CachedNodes.TABLE + " ("
                + DbContract.CachedNodes.STATION_ID + " TEXT PRIMARY KEY, "
                + DbContract.CachedNodes.STATION_NAME + " TEXT, "
                + DbContract.CachedNodes.CITY + " TEXT, "
                + DbContract.CachedNodes.LAT + " REAL, "
                + DbContract.CachedNodes.LNG + " REAL, "
                + DbContract.CachedNodes.CAPACITY_KWH + " REAL, "
                + DbContract.CachedNodes.TOTAL_SLOTS + " INTEGER, "
                + DbContract.CachedNodes.STATUS + " TEXT, "
                + DbContract.CachedNodes.CACHED_AT + " TEXT)");

        db.execSQL("CREATE TABLE " + DbContract.CachedBookings.TABLE + " ("
                + DbContract.CachedBookings.RESERVATION_ID + " TEXT PRIMARY KEY, "
                + DbContract.CachedBookings.STATION_ID + " TEXT, "
                + DbContract.CachedBookings.STATION_NAME + " TEXT, "
                + DbContract.CachedBookings.SLOT_START + " TEXT, "
                + DbContract.CachedBookings.SLOT_END + " TEXT, "
                + DbContract.CachedBookings.ENERGY_KWH + " REAL, "
                + DbContract.CachedBookings.STATUS + " TEXT, "
                + DbContract.CachedBookings.CACHED_AT + " TEXT)");

        db.execSQL("CREATE TABLE " + DbContract.SyncMeta.TABLE + " ("
                + DbContract.SyncMeta.ENTITY + " TEXT PRIMARY KEY, "
                + DbContract.SyncMeta.LAST_SYNCED_AT + " TEXT)");
    }

    // Drop and recreate is acceptable for this project: the cache is never
    // authoritative, so a schema change costs one refresh, not data. Stated so
    // nobody mistakes it for an oversight.
    @Override
    public void onUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
        db.execSQL("DROP TABLE IF EXISTS " + DbContract.Session.TABLE);
        db.execSQL("DROP TABLE IF EXISTS " + DbContract.CachedNodes.TABLE);
        db.execSQL("DROP TABLE IF EXISTS " + DbContract.CachedBookings.TABLE);
        db.execSQL("DROP TABLE IF EXISTS " + DbContract.SyncMeta.TABLE);
        onCreate(db);
    }

    // Wipes every cached table and the session row. Called on logout so a
    // different user never sees the previous user's data.
    public void clearAll() {
        SQLiteDatabase db = getWritableDatabase();
        db.delete(DbContract.Session.TABLE, null, null);
        db.delete(DbContract.CachedNodes.TABLE, null, null);
        db.delete(DbContract.CachedBookings.TABLE, null, null);
        db.delete(DbContract.SyncMeta.TABLE, null, null);
    }

    // Shared cursor mapper used by every DAO. Materialises rows into
    // ContentValues so callers cannot leak a cursor by forgetting to close it,
    // and always closes the cursor here.
    public static java.util.List<android.content.ContentValues> toRows(android.database.Cursor cursor) {
        java.util.List<android.content.ContentValues> rows = new java.util.ArrayList<>();

        if (cursor == null) {
            return rows;
        }

        try {
            String[] columns = cursor.getColumnNames();
            while (cursor.moveToNext()) {
                android.content.ContentValues row = new android.content.ContentValues();
                for (String column : columns) {
                    row.put(column, cursor.getString(cursor.getColumnIndexOrThrow(column)));
                }
                rows.add(row);
            }
        } finally {
            cursor.close();
        }

        return rows;
    }
}
