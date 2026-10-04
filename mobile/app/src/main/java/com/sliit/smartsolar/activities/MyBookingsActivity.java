/*
 * File:    MyBookingsActivity.java
 * Author:  Thisuri
 * Created: 2026-09-28
 * Purpose: A prosumer's own bookings from GET /reservations/mine, grouped as
 *          Pending approval, Approved (current), and Past & closed. The route
 *          has no NIC parameter — the API reads it from the token — so this
 *          screen cannot be pointed at anyone else's bookings. Tapping a
 *          booking opens EditReservationActivity (details, change, cancel).
 */
package com.sliit.smartsolar.activities;

import android.content.ContentValues;
import android.content.Intent;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.sliit.smartsolar.R;
import com.sliit.smartsolar.database.BookingCache;
import com.sliit.smartsolar.models.NodeOption;
import com.sliit.smartsolar.models.Reservation;
import com.sliit.smartsolar.models.ReservationStatuses;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.network.DateUtils;
import com.sliit.smartsolar.parsers.ReservationParser;
import com.sliit.smartsolar.utils.BottomNav;
import com.sliit.smartsolar.utils.ReservationFormat;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONException;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.Date;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

public class MyBookingsActivity extends AppCompatActivity {

    private LinearLayout listBookings;
    private TextView textError, textEmpty, textStale;
    private ProgressBar progress;

    // Read-through cache of the caller's own bookings. Written only after a
    // successful read, read only when one fails, cleared on logout.
    private BookingCache cache;

    /** Node names for display only; loaded once per screen. */
    private List<NodeOption> nodes;

    // Binds the screen. Loading happens in onResume.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_my_bookings);
        BottomNav.attach(this, R.id.nav_bookings);

        listBookings = findViewById(R.id.listBookings);
        textError = findViewById(R.id.textError);
        textEmpty = findViewById(R.id.textEmpty);
        textStale = findViewById(R.id.textStale);
        progress = findViewById(R.id.progress);

        cache = new BookingCache(this);

        findViewById(R.id.buttonBookSlot).setOnClickListener(v ->
                startActivity(new Intent(this, SlotSearchActivity.class)));
    }

    // Reloads every time the screen is shown, so a booking changed or
    // cancelled a moment ago appears with the API's current status.
    @Override
    protected void onResume() {
        super.onResume();
        if (nodes == null) {
            loadNodesThenBookings();
        } else {
            loadBookings();
        }
    }

    // GET /nodes, only to show node names instead of ids. A failure falls
    // back to short ids rather than blocking the booking list.
    private void loadNodesThenBookings() {
        setBusy(true);

        ApiClient.get("/nodes", new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                try {
                    nodes = ReservationParser.parseNodes(body);
                } catch (JSONException e) {
                    nodes = new ArrayList<>();
                }
                loadBookings();
            }

            @Override
            public void onError(String code, String detail) {
                if (SessionManager.handleAuthError(MyBookingsActivity.this, code, detail)) {
                    return;
                }
                nodes = new ArrayList<>();
                loadBookings();
            }
        });
    }

    // GET /reservations/mine — no status filter, so one call serves all three
    // groups. The API is always asked first; the cache is only ever a fallback.
    private void loadBookings() {
        hideMessages();
        setBusy(true);

        ApiClient.get("/reservations/mine", new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                setBusy(false);
                try {
                    List<Reservation> live = ReservationParser.parseReservationList(body);

                    // Render the response, then snapshot it. The cache is written
                    // only after the API answered, so it can never hold something
                    // the server did not say.
                    showBookings(live, null);
                    cache.save(BookingCache.rowsFor(live, nodeNamesById()));
                } catch (JSONException e) {
                    showError(getString(R.string.error_unexpected_response));
                }
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false);
                if (!SessionManager.handleAuthError(MyBookingsActivity.this, code, detail)) {
                    fallBackToCache(detail);
                }
            }
        });
    }

    // The call failed. Show the last known bookings, labelled with when they
    // were taken; otherwise the server's own message. A cached list is never
    // presented as current — the label is what keeps it honest.
    private void fallBackToCache(String detail) {
        List<ContentValues> cached = cache.load();
        String when = cache.lastRefreshedDisplay();

        // Nothing has ever been cached, so the server's message is all there is.
        // An empty list here would wrongly read as "you have no bookings".
        if (cached.isEmpty() && when == null) {
            showError(detail);
            return;
        }

        // The cached rows carry their station names, so a cached list can still
        // name its nodes when /nodes could not be reached either.
        if (nodes == null || nodes.isEmpty()) {
            nodes = BookingCache.nodeOptions(cached);
        }

        showBookings(BookingCache.toReservations(cached), when == null
                ? getString(R.string.cache_offline)
                : getString(R.string.cache_last_updated, when));
    }

    // Station names for the cache, so a cached row can name its node later
    // without another read.
    private Map<String, String> nodeNamesById() {
        Map<String, String> names = new HashMap<>();

        if (nodes == null) {
            return names;
        }

        for (NodeOption node : nodes) {
            names.put(node.id, node.stationName);
        }

        return names;
    }

    // Groups by the API's status string. Upcoming groups run soonest first;
    // the history group runs most recent first.
    private void showBookings(List<Reservation> all, String staleNote) {
        listBookings.removeAllViews();
        textEmpty.setVisibility(all.isEmpty() ? View.VISIBLE : View.GONE);

        // Null for live data, a "last updated" line for cached data, so the two
        // can never look the same on screen.
        if (staleNote == null) {
            textStale.setVisibility(View.GONE);
        } else {
            textStale.setText(staleNote);
            textStale.setVisibility(View.VISIBLE);
        }

        List<Reservation> pending = new ArrayList<>();
        List<Reservation> approved = new ArrayList<>();
        List<Reservation> closed = new ArrayList<>();

        for (Reservation r : all) {
            if (ReservationStatuses.PENDING.equals(r.status)) {
                pending.add(r);
            } else if (ReservationStatuses.APPROVED.equals(r.status)) {
                approved.add(r);
            } else {
                closed.add(r);
            }
        }

        Comparator<Reservation> soonestFirst = (a, b) -> Long.compare(startMillis(a), startMillis(b));
        Collections.sort(pending, soonestFirst);
        Collections.sort(approved, soonestFirst);
        Collections.sort(closed, Collections.reverseOrder(soonestFirst));

        addGroup(R.string.my_bookings_pending, pending);
        addGroup(R.string.my_bookings_approved, approved);
        addGroup(R.string.my_bookings_closed, closed);
    }

    // One header plus a card per booking; an empty group is left out.
    private void addGroup(int headerRes, List<Reservation> group) {
        if (group.isEmpty()) {
            return;
        }

        LayoutInflater inflater = getLayoutInflater();
        TextView header = (TextView) inflater.inflate(R.layout.item_section_header, listBookings, false);
        header.setText(getString(headerRes, group.size()));
        listBookings.addView(header);

        for (Reservation r : group) {
            listBookings.addView(buildRow(inflater, r));
        }
    }

    // One booking card; tapping it opens the booking with its node name.
    private View buildRow(LayoutInflater inflater, Reservation r) {
        View row = inflater.inflate(R.layout.item_reservation, listBookings, false);
        String stationName = ReservationFormat.nodeName(this, nodes, r.stationId);

        ((TextView) row.findViewById(R.id.textBookingTime))
                .setText(ReservationFormat.slotRange(r.slotStart, r.slotEnd));
        ((TextView) row.findViewById(R.id.textBookingNode)).setText(stationName);
        ((TextView) row.findViewById(R.id.textBookingMeta)).setText(getString(
                R.string.booking_meta_line,
                ReservationFormat.energy(this, r.energyKWh),
                r.status,
                ReservationFormat.shortId(r.id)));

        row.setOnClickListener(v -> startActivity(new Intent(this, EditReservationActivity.class)
                .putExtra(EditReservationActivity.EXTRA_RESERVATION_ID, r.id)
                .putExtra(SlotSearchActivity.EXTRA_STATION_NAME, stationName)));
        return row;
    }

    // Sort key: the booking's slot start instant.
    private static long startMillis(Reservation r) {
        Date start = DateUtils.parseUtc(r.slotStart);
        return start == null ? Long.MAX_VALUE : start.getTime();
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

    // Clears the error, empty-list and stale lines before a reload.
    private void hideMessages() {
        textError.setVisibility(View.GONE);
        textEmpty.setVisibility(View.GONE);
        textStale.setVisibility(View.GONE);
    }
}
