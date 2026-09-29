/*
 * File:    ProsumerDashboardActivity.java
 * Author:  Imadh
 * Created: 2026-09-30
 * Purpose: The prosumer's own figures from GET /dashboard/prosumer
 *          (docs/api-contract.md §5), read live on every resume.
 *
 *          This screen counts nothing. Each number is shown exactly as the API
 *          returned it, and "upcoming" is the server's definition rather than
 *          this screen's — a count worked out here from a fetched list would be
 *          the phone's arithmetic, and the rubric names that as the failure
 *          mode. There is no arithmetic in this file.
 *
 *          The API is asked first, always. Cached figures appear only when that
 *          call fails, and then with the time they were taken.
 */
package com.sliit.smartsolar.activities;

import android.content.Intent;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.View;
import android.widget.ProgressBar;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.sliit.smartsolar.R;
import com.sliit.smartsolar.database.DashboardCache;
import com.sliit.smartsolar.models.NodeOption;
import com.sliit.smartsolar.models.ProsumerDashboard;
import com.sliit.smartsolar.models.Reservation;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.parsers.DashboardParser;
import com.sliit.smartsolar.parsers.ReservationParser;
import com.sliit.smartsolar.utils.ReservationFormat;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONException;

import java.util.ArrayList;
import java.util.List;

public class ProsumerDashboardActivity extends AppCompatActivity {

    // The API reads the NIC from the token, so this route takes no parameter.
    private static final String DASHBOARD_PATH = "/dashboard/prosumer";

    private TextView textActiveCount, textPendingCount, textApprovedFutureCount;
    private TextView textNextTime, textNextNode, textNextStatus, textNextNone;
    private TextView textError, textStale;
    private ProgressBar progress;

    // Read-through cache of the figures. Written only after a successful read,
    // read only when one fails, cleared on logout.
    private DashboardCache cache;

    /** Node names for display only; loaded once per screen. */
    private List<NodeOption> nodes;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_prosumer_dashboard);

        textActiveCount = findViewById(R.id.textActiveCount);
        textPendingCount = findViewById(R.id.textPendingCount);
        textApprovedFutureCount = findViewById(R.id.textApprovedFutureCount);
        textNextTime = findViewById(R.id.textNextTime);
        textNextNode = findViewById(R.id.textNextNode);
        textNextStatus = findViewById(R.id.textNextStatus);
        textNextNone = findViewById(R.id.textNextNone);
        textError = findViewById(R.id.textError);
        textStale = findViewById(R.id.textStale);
        progress = findViewById(R.id.progress);

        cache = new DashboardCache(this);

        findViewById(R.id.buttonRefresh).setOnClickListener(v -> load());
        findViewById(R.id.buttonMyBookings).setOnClickListener(v ->
                startActivity(new Intent(this, MyBookingsActivity.class)));
        findViewById(R.id.buttonMyQr).setOnClickListener(v ->
                startActivity(new Intent(this, QrDisplayActivity.class)));
        findViewById(R.id.buttonHistory).setOnClickListener(v ->
                startActivity(new Intent(this, BookingHistoryActivity.class)));
    }

    // Reloads on every resume, so returning from a booking action shows the
    // figures that action changed rather than the ones from before it.
    @Override
    protected void onResume() {
        super.onResume();

        if (nodes == null) {
            loadNodesThenFigures();
        } else {
            load();
        }
    }

    // GET /nodes, only so the next booking can name its station. A failure falls
    // back to a short id rather than blocking the figures, which are the point
    // of the screen.
    private void loadNodesThenFigures() {
        setBusy(true);

        ApiClient.get("/nodes", new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                try {
                    nodes = ReservationParser.parseNodes(body);
                } catch (JSONException e) {
                    nodes = new ArrayList<>();
                }
                load();
            }

            @Override
            public void onError(String code, String detail) {
                if (SessionManager.handleAuthError(ProsumerDashboardActivity.this, code, detail)) {
                    return;
                }
                nodes = new ArrayList<>();
                load();
            }
        });
    }

    // GET /dashboard/prosumer — the API is always asked first; the cached
    // figures are only ever a fallback.
    private void load() {
        hideMessages();
        setBusy(true);

        ApiClient.get(DASHBOARD_PATH, new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                setBusy(false);
                try {
                    ProsumerDashboard dashboard = DashboardParser.parseProsumer(body);

                    // Render what the server said, then snapshot it. The cache is
                    // written only after the API answered, so it can never hold a
                    // number this phone worked out for itself.
                    render(dashboard, null);
                    cache.save(dashboard);
                } catch (JSONException e) {
                    showError(getString(R.string.error_unexpected_response));
                }
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false);
                if (!SessionManager.handleAuthError(ProsumerDashboardActivity.this, code, detail)) {
                    fallBackToCache(detail);
                }
            }
        });
    }

    // The call failed. Show the last known figures, labelled with when they were
    // taken; otherwise the server's own message.
    private void fallBackToCache(String detail) {
        ProsumerDashboard cached = cache.load();

        // Nothing has ever been cached, so the server's message is all there is.
        // Zeroes here would read as a real answer that this prosumer has none,
        // which is a different claim and the wrong one to make by accident.
        if (cached == null) {
            showError(detail);
            return;
        }

        String when = cache.lastRefreshedDisplay();
        render(cached, when == null
                ? getString(R.string.cache_offline)
                : getString(R.string.cache_last_updated, when));
    }

    // Draws the figures. `staleNote` is null for live data and a "last updated"
    // line for cached data, so the two can never look the same on screen.
    private void render(ProsumerDashboard dashboard, String staleNote) {
        textActiveCount.setText(String.valueOf(dashboard.activeCount));
        textPendingCount.setText(String.valueOf(dashboard.pendingCount));
        textApprovedFutureCount.setText(String.valueOf(dashboard.approvedFutureCount));

        if (staleNote == null) {
            textStale.setVisibility(View.GONE);
        } else {
            textStale.setText(staleNote);
            textStale.setVisibility(View.VISIBLE);
        }

        renderNextBooking(dashboard.nextBooking);
    }

    // The card shows either the booking or a placeholder, never both, so an
    // absent next booking cannot leave the previous one on screen.
    private void renderNextBooking(Reservation next) {
        if (next == null) {
            textNextNone.setVisibility(View.VISIBLE);
            textNextTime.setVisibility(View.GONE);
            textNextNode.setVisibility(View.GONE);
            textNextStatus.setVisibility(View.GONE);
            return;
        }

        textNextNone.setVisibility(View.GONE);
        textNextTime.setText(ReservationFormat.slotRange(next.slotStart, next.slotEnd));
        textNextNode.setText(ReservationFormat.nodeName(this, nodes, next.stationId));
        textNextStatus.setText(next.status);

        textNextTime.setVisibility(View.VISIBLE);
        textNextNode.setVisibility(View.VISIBLE);
        textNextStatus.setVisibility(View.VISIBLE);
    }

    // Shows or hides the spinner.
    private void setBusy(boolean busy) {
        progress.setVisibility(busy ? View.VISIBLE : View.GONE);
    }

    // Shows the API's detail (or transport wording).
    private void showError(String message) {
        textError.setText(message);
        textError.setVisibility(TextUtils.isEmpty(message) ? View.GONE : View.VISIBLE);
        textStale.setVisibility(View.GONE);
    }

    // Clears the error and stale lines before a reload.
    private void hideMessages() {
        textError.setVisibility(View.GONE);
        textStale.setVisibility(View.GONE);
    }
}
