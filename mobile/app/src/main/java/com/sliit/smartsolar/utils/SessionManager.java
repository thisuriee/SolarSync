/*
 * File:    SessionManager.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Single place that writes and clears the SQLite session row, decides
 *          whether a stored session is still usable, and picks the home screen
 *          for a role. Screens of every vertical call handleAuthError() so a 401
 *          always clears the session and returns to login the same way.
 *          Navigation only — the API enforces every business rule.
 */
package com.sliit.smartsolar.utils;

import android.app.Activity;
import android.content.ContentValues;
import android.content.Context;
import android.content.Intent;

import com.sliit.smartsolar.activities.LoginActivity;
import com.sliit.smartsolar.activities.OperatorHomeActivity;
import com.sliit.smartsolar.activities.ProsumerHomeActivity;
import com.sliit.smartsolar.database.DatabaseHelper;
import com.sliit.smartsolar.database.DbContract;
import com.sliit.smartsolar.database.SessionDao;
import com.sliit.smartsolar.models.LoginResult;
import com.sliit.smartsolar.models.UserRoles;
import com.sliit.smartsolar.network.DateUtils;

import java.util.Date;

public final class SessionManager {

    /** Intent extra carrying the API's detail to show on the login screen. */
    public static final String EXTRA_LOGIN_MESSAGE = "login_message";

    // docs/response-format.md: missing, expired or invalid token, or the user
    // behind it no longer exists. Clients clear the session and re-login.
    private static final String CODE_INVALID_TOKEN = "AUTH_INVALID_TOKEN";

    private SessionManager() {
    }

    // Stores the token and who signed in as the single session row. The password
    // is deliberately not a parameter: it is never stored, in any form. The
    // login response has no username, so the one the user typed is kept.
    public static void save(Context context, LoginResult login, String username) {
        new SessionDao(context).save(
                login.userId,
                username,
                login.nic,
                login.fullName,
                login.role,
                login.token,
                DateUtils.toUtcWire(new Date()),
                login.expiresAt);
    }

    // Keeps the cached display name in step after the API accepted a profile
    // edit. The value passed is the API's response, not what was typed. Every
    // other session column is rewritten unchanged.
    public static void updateFullName(Context context, String fullName) {
        SessionDao dao = new SessionDao(context);
        ContentValues row = dao.get();
        if (row == null) {
            return;
        }

        dao.save(
                row.getAsString(DbContract.Session.USER_ID),
                row.getAsString(DbContract.Session.USERNAME),
                row.getAsString(DbContract.Session.NIC),
                fullName,
                row.getAsString(DbContract.Session.ROLE),
                row.getAsString(DbContract.Session.TOKEN),
                row.getAsString(DbContract.Session.ISSUED_AT),
                row.getAsString(DbContract.Session.EXPIRES_AT));
    }

    // Wipes the session row and every cached table without leaving the
    // current screen. Used when the account itself is gone (self-deactivation),
    // so the outcome can still be shown before returning to login.
    public static void clear(Context context) {
        DatabaseHelper.getInstance(context).clearAll();
    }

    // Opens the login screen as a fresh task, closing everything else.
    public static void goToLogin(Activity from) {
        openLogin(from, null);
    }

    // True when a stored session can still be used: a token is present and the
    // expiry the API gave us has not passed. The API still has the final say —
    // it answers 401 and handleAuthError clears the row — but checking here
    // skips a round trip that is certain to fail.
    //
    // The expiry belongs to the token the server issued, so reading it locally
    // is not the client deciding anything.
    public static boolean isLoggedIn(Context context) {
        ContentValues row = new SessionDao(context).get();

        if (row == null) {
            return false;
        }

        String token = row.getAsString(DbContract.Session.TOKEN);
        if (token == null || token.trim().isEmpty()) {
            return false;
        }

        return !hasExpired(row.getAsString(DbContract.Session.EXPIRES_AT));
    }

    // A missing or unreadable expiry counts as not yet expired. The API is the
    // authority, and refusing a session the server would still accept would send
    // a signed-in user back to the login form for no reason.
    private static boolean hasExpired(String expiresAtIso) {
        Date expiresAt = DateUtils.parseUtc(expiresAtIso);
        return expiresAt != null && expiresAt.before(new Date());
    }

    // The home screen for a role, or null when the role has no mobile home.
    // Backoffice is web-only by design, so it returns null.
    public static Class<? extends Activity> homeFor(String role) {
        if (UserRoles.PROSUMER.equals(role)) {
            return ProsumerHomeActivity.class;
        }
        if (UserRoles.GRID_OPERATOR.equals(role)) {
            return OperatorHomeActivity.class;
        }
        return null;
    }

    // Opens the stored role's home and closes the current screen, clearing the
    // back stack so Back cannot return to the login form. Returns false when the
    // stored role has no mobile home.
    public static boolean openHome(Activity from) {
        Class<? extends Activity> home = homeFor(new SessionDao(from).getRole());
        if (home == null) {
            return false;
        }

        Intent intent = new Intent(from, home);
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
        from.startActivity(intent);
        from.finish();
        return true;
    }

    // Logout. Wipes the session row and every cached table so the next user of
    // the device never sees the previous user's data, then returns to login.
    public static void logout(Activity from) {
        DatabaseHelper.getInstance(from).clearAll();
        openLogin(from, null);
    }

    // Call from any onError(). When the API says the token is no longer valid,
    // clears everything and returns to login showing the API's own detail.
    // Returns true if it handled the error, so the caller can stop there.
    public static boolean handleAuthError(Activity from, String code, String detail) {
        if (!CODE_INVALID_TOKEN.equals(code)) {
            return false;
        }

        DatabaseHelper.getInstance(from).clearAll();
        openLogin(from, detail);
        return true;
    }

    // Opens the login screen as a fresh task, optionally with a message to show.
    private static void openLogin(Activity from, String message) {
        Intent intent = new Intent(from, LoginActivity.class);
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
        if (message != null) {
            intent.putExtra(EXTRA_LOGIN_MESSAGE, message);
        }
        from.startActivity(intent);
        from.finish();
    }
}
