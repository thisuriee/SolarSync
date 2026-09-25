/*
 * File:    SessionDao.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Reads and writes the single session row — who is signed in and the
 *          bearer token. The password is never stored, in any form. Plumbing
 *          only. Owner: M1.
 */
package com.sliit.smartsolar.database;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;

public class SessionDao {

    private final DatabaseHelper helper;

    public SessionDao(Context context) {
        this.helper = DatabaseHelper.getInstance(context);
    }

    // Stores the signed-in user as the one session row, replacing any previous
    // one. Callers pass the token only — never a password.
    public void save(String userId, String username, String nic, String fullName,
                     String role, String token, String issuedAt, String expiresAt) {

        ContentValues values = new ContentValues();
        values.put(DbContract.Session.ID, DbContract.Session.SINGLE_ROW_ID);
        values.put(DbContract.Session.USER_ID, userId);
        values.put(DbContract.Session.USERNAME, username);
        values.put(DbContract.Session.NIC, nic);
        values.put(DbContract.Session.FULL_NAME, fullName);
        values.put(DbContract.Session.ROLE, role);
        values.put(DbContract.Session.TOKEN, token);
        values.put(DbContract.Session.ISSUED_AT, issuedAt);
        values.put(DbContract.Session.EXPIRES_AT, expiresAt);

        helper.getWritableDatabase().insertWithOnConflict(
                DbContract.Session.TABLE, null, values, SQLiteDatabase.CONFLICT_REPLACE);
    }

    // Returns the session row, or null when nobody is signed in.
    public ContentValues get() {
        Cursor cursor = helper.getReadableDatabase().query(
                DbContract.Session.TABLE, null, null, null, null, null, null, "1");

        java.util.List<ContentValues> rows = DatabaseHelper.toRows(cursor);
        return rows.isEmpty() ? null : rows.get(0);
    }

    // The bearer token attached to every authenticated request. Null when out.
    public String getToken() {
        return getString(DbContract.Session.TOKEN);
    }

    // The signed-in user's role, used to choose which home screen to open.
    public String getRole() {
        return getString(DbContract.Session.ROLE);
    }

    // The signed-in prosumer's NIC. Null for web users, who have no NIC.
    public String getNic() {
        return getString(DbContract.Session.NIC);
    }

    // The signed-in user's display name, for the header.
    public String getFullName() {
        return getString(DbContract.Session.FULL_NAME);
    }

    // The Mongo _id of the signed-in user, used as the operator id.
    public String getUserId() {
        return getString(DbContract.Session.USER_ID);
    }

    // True when a token is present. Makes no judgement about whether it has
    // expired — only the API decides that, and answers 401.
    public boolean hasToken() {
        String token = getToken();
        return token != null && !token.isEmpty();
    }

    // Clears the session row. Called on logout and whenever the API answers 401.
    public void clear() {
        helper.getWritableDatabase().delete(DbContract.Session.TABLE, null, null);
    }

    // Reads one column from the session row, or null when signed out.
    private String getString(String column) {
        ContentValues row = get();
        return row == null ? null : row.getAsString(column);
    }
}
