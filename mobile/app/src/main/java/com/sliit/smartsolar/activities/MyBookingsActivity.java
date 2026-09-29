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
import com.sliit.smartsolar.models.NodeOption;
import com.sliit.smartsolar.models.Reservation;
import com.sliit.smartsolar.models.ReservationStatuses;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.network.DateUtils;
import com.sliit.smartsolar.parsers.ReservationParser;
import com.sliit.smartsolar.utils.ReservationFormat;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONException;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.Date;
import java.util.List;

public class MyBookingsActivity extends AppCompatActivity {

    private LinearLayout listBookings;
    private TextView textError, textEmpty;
    private ProgressBar progress;

    /** Node names for display only; loaded once per screen. */
    private List<NodeOption> nodes;

    // Binds the screen. Loading happens in onResume.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_my_bookings);

        listBookings = findViewById(R.id.listBookings);
        textError = findViewById(R.id.textError);
        textEmpty = findViewById(R.id.textEmpty);
        progress = findViewById(R.id.progress);

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

    // GET /reservations/mine — no status filter, so one call serves all
    // three groups. Never read from a local cache: the API is the only source.
    private void loadBookings() {
        hideMessages();
        setBusy(true);

        ApiClient.get("/reservations/mine", new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                setBusy(false);
                try {
                    showBookings(ReservationParser.parseReservationList(body));
                } catch (JSONException e) {
                    showError(getString(R.string.error_unexpected_response));
                }
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false);
                if (!SessionManager.handleAuthError(MyBookingsActivity.this, code, detail)) {
                    showError(detail);
                }
            }
        });
    }

    // Groups by the API's status string. Upcoming groups run soonest first;
    // the history group runs most recent first.
    private void showBookings(List<Reservation> all) {
        listBookings.removeAllViews();
        textEmpty.setVisibility(all.isEmpty() ? View.VISIBLE : View.GONE);

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
    }

    // Clears the error and empty-list lines before a reload.
    private void hideMessages() {
        textError.setVisibility(View.GONE);
        textEmpty.setVisibility(View.GONE);
    }
}
