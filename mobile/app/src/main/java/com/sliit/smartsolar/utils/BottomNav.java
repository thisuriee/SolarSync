/*
 * File:    BottomNav.java
 * Author:  Thisuri
 * Created: 2026-10-04
 * Purpose: The bottom tab bar shared by the top-level screens. Each of those
 *          Activities calls attach() once after setContentView; this class
 *          places the bar under the screen, picks the tabs for the signed-in
 *          role and moves between the tab screens. Navigation only: showing a
 *          tab grants nothing, and every screen's calls are still judged by
 *          the API from the token.
 */
package com.sliit.smartsolar.utils;

import android.content.Intent;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;

import androidx.appcompat.app.AppCompatActivity;
import androidx.core.view.ViewCompat;

import com.google.android.material.bottomnavigation.BottomNavigationView;
import com.sliit.smartsolar.R;
import com.sliit.smartsolar.activities.MyBookingsActivity;
import com.sliit.smartsolar.activities.MyProfileActivity;
import com.sliit.smartsolar.activities.NearbyNodesMapActivity;
import com.sliit.smartsolar.activities.NodeListActivity;
import com.sliit.smartsolar.activities.OperatorHomeActivity;
import com.sliit.smartsolar.activities.ProsumerHomeActivity;
import com.sliit.smartsolar.database.SessionDao;
import com.sliit.smartsolar.models.UserRoles;

public final class BottomNav {

    private BottomNav() {
    }

    // Puts the bar under the Activity's screen with `selectedItemId` shown as
    // the current tab. The role comes from the saved session and only chooses
    // which menu to show. If the role's menu has no such tab (a Prosumer who
    // opened the map from Home, where the map is not one of their tabs) the
    // screen is left as an ordinary pushed screen with no bar.
    public static void attach(AppCompatActivity activity, int selectedItemId) {
        boolean operator = UserRoles.GRID_OPERATOR.equals(new SessionDao(activity).getRole());

        ViewGroup content = activity.findViewById(android.R.id.content);
        View screen = content.getChildAt(0);

        View bar = activity.getLayoutInflater().inflate(R.layout.view_bottom_nav, content, false);
        BottomNavigationView nav = bar.findViewById(R.id.bottomNav);
        nav.inflateMenu(operator ? R.menu.bottom_nav_operator : R.menu.bottom_nav_prosumer);

        if (nav.getMenu().findItem(selectedItemId) == null) {
            return;
        }

        // Screen on top taking the spare height, bar underneath. The shell takes
        // over the system-bar insets from the screen so the bar sits above the
        // phone's own navigation buttons instead of behind them.
        LinearLayout shell = new LinearLayout(activity);
        shell.setOrientation(LinearLayout.VERTICAL);
        shell.setFitsSystemWindows(true);
        screen.setFitsSystemWindows(false);

        content.removeView(screen);
        shell.addView(screen, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));
        shell.addView(bar);
        content.addView(shell, new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        ViewCompat.requestApplyInsets(shell);

        // Selected before the listener is set, so marking the current tab does
        // not count as a tap.
        nav.setSelectedItemId(selectedItemId);
        nav.setOnItemSelectedListener(item -> {
            if (item.getItemId() != selectedItemId) {
                open(activity, item.getItemId(), selectedItemId == R.id.nav_home, operator);
            }
            // False keeps this screen's own tab highlighted: the tapped tab is
            // highlighted by the screen that opens, and this one must still be
            // right if the user comes back to it.
            return item.getItemId() == selectedItemId;
        });
    }

    // Opens the screen behind a tab. Home stays at the bottom of the back stack
    // and at most one other tab sits on it, so Back from any tab returns Home
    // and tapping between tabs never piles screens up.
    private static void open(AppCompatActivity from, int itemId, boolean fromHome, boolean operator) {
        if (itemId == R.id.nav_home) {
            // CLEAR_TOP + SINGLE_TOP returns to the Home already running and
            // closes whatever was opened on top of it.
            Class<?> home = operator ? OperatorHomeActivity.class : ProsumerHomeActivity.class;
            from.startActivity(new Intent(from, home)
                    .addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP));
        } else {
            from.startActivity(new Intent(from, screenFor(itemId)));
            if (!fromHome) {
                from.finish();
            }
        }

        // A tab switch should not slide like opening a detail screen does.
        from.overridePendingTransition(0, 0);
    }

    // The Activity behind each non-home tab.
    private static Class<?> screenFor(int itemId) {
        if (itemId == R.id.nav_bookings) {
            return MyBookingsActivity.class;
        }
        if (itemId == R.id.nav_map) {
            return NearbyNodesMapActivity.class;
        }
        if (itemId == R.id.nav_profile) {
            return MyProfileActivity.class;
        }
        return NodeListActivity.class;
    }
}
