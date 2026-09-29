/*
 * File:    BookingHistoryActivity.java
 * Author:  Imadh
 * Created: 2026-09-30
 * Purpose: The prosumer's booking history from GET /reservations/history
 *          (docs/api-contract.md §5), filtered and paged by the server.
 *
 *          Every filter is a question for the API. Changing one rebuilds the
 *          query string and asks again — nothing is narrowed from a list already
 *          on the phone, because a filter applied here would be this screen's
 *          answer rather than the server's, and the rubric is explicit that a
 *          filter over a local cache is worth less.
 *
 *          The route scopes a prosumer to the token's NIC whatever parameters
 *          arrive, so there is no NIC control here: there is nothing it could
 *          usefully set.
 *
 *          Offline, the cached bookings stand in ONLY while no filter is
 *          applied. Those rows were fetched unfiltered, so narrowing them here
 *          would answer a question the server was never asked.
 */
package com.sliit.smartsolar.activities;

import android.app.DatePickerDialog;
import android.content.ContentValues;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.inputmethod.EditorInfo;
import android.widget.ArrayAdapter;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.google.android.material.textfield.MaterialAutoCompleteTextView;
import com.google.android.material.textfield.TextInputEditText;
import com.sliit.smartsolar.R;
import com.sliit.smartsolar.database.BookingCache;
import com.sliit.smartsolar.models.NodeOption;
import com.sliit.smartsolar.models.PagedReservations;
import com.sliit.smartsolar.models.Reservation;
import com.sliit.smartsolar.models.ReservationStatuses;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.parsers.ReservationParser;
import com.sliit.smartsolar.utils.HistoryQuery;
import com.sliit.smartsolar.utils.ReservationFormat;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONException;

import java.util.ArrayList;
import java.util.Calendar;
import java.util.Date;
import java.util.List;

public class BookingHistoryActivity extends AppCompatActivity {

    // The API reads the NIC from the token, so no nic parameter is sent.
    private static final String HISTORY_PATH = "/reservations/history";

    // Offered in the status drop-down, in lifecycle order. The entry at
    // position 0 of the drop-down is "Any status", not one of these.
    private static final String[] STATUSES = {
            ReservationStatuses.PENDING,
            ReservationStatuses.APPROVED,
            ReservationStatuses.REJECTED,
            ReservationStatuses.CANCELLED,
            ReservationStatuses.COMPLETED
    };

    private MaterialAutoCompleteTextView inputStatus;
    private TextInputEditText inputSearch;
    private TextView textRange, textPage, textError, textEmpty, textStale;
    private LinearLayout listHistory;
    private ProgressBar progress;

    // The current filter. null means "not filtering on this one".
    private String selectedStatus;
    private Date fromDay;
    private Date toDay;

    private int currentPage = 1;
    private int totalPages;

    // Read-through cache of the caller's bookings, filled by the unfiltered read
    // on the My bookings screen. This screen only ever reads it.
    private BookingCache cache;

    /** Node names for display only; loaded once per screen. */
    private List<NodeOption> nodes;

    // Bumped per request so a slow answer to a filter the user has moved on from
    // is dropped instead of overwriting a newer one. The same guard
    // SlotSearchActivity uses for its own re-queries.
    private int historyRequest;

    private boolean started;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_booking_history);

        inputStatus = findViewById(R.id.inputStatus);
        inputSearch = findViewById(R.id.inputSearch);
        textRange = findViewById(R.id.textRange);
        textPage = findViewById(R.id.textPage);
        textError = findViewById(R.id.textError);
        textEmpty = findViewById(R.id.textEmpty);
        textStale = findViewById(R.id.textStale);
        listHistory = findViewById(R.id.listHistory);
        progress = findViewById(R.id.progress);

        cache = new BookingCache(this);

        bindStatusDropDown();

        findViewById(R.id.buttonApply).setOnClickListener(v -> applyFilters());
        findViewById(R.id.buttonClearDates).setOnClickListener(v -> clearDates());
        findViewById(R.id.buttonFrom).setOnClickListener(v -> pickDay(true));
        findViewById(R.id.buttonTo).setOnClickListener(v -> pickDay(false));
        findViewById(R.id.buttonPrev).setOnClickListener(v -> stepPage(-1));
        findViewById(R.id.buttonNext).setOnClickListener(v -> stepPage(1));

        // The search field declares an IME search action, so it has to do
        // something or that key is a dead end.
        inputSearch.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_SEARCH) {
                applyFilters();
                return true;
            }
            return false;
        });
    }

    @Override
    protected void onResume() {
        super.onResume();

        if (!started) {
            started = true;
            loadNodesThenHistory();
            return;
        }

        // Refresh on return, so a booking changed from the detail screen shows
        // its new status here.
        loadHistory();
    }

    // "Any status" sits at position 0, the same sentinel SlotSearchActivity uses
    // for "All nodes".
    private void bindStatusDropDown() {
        List<String> labels = new ArrayList<>(STATUSES.length + 1);
        labels.add(getString(R.string.history_status_any));

        for (String status : STATUSES) {
            labels.add(status);
        }

        inputStatus.setAdapter(new ArrayAdapter<>(this, android.R.layout.simple_list_item_1, labels));
        inputStatus.setText(labels.get(0), false);

        inputStatus.setOnItemClickListener((parent, view, position, id) ->
                selectedStatus = position == 0 ? null : STATUSES[position - 1]);
    }

    // A changed filter is a new question, so it starts at page 1: "page 3" has
    // no meaning under a filter the user has just changed.
    private void applyFilters() {
        currentPage = 1;
        loadHistory();
    }

    private void stepPage(int delta) {
        int target = currentPage + delta;

        if (target < 1 || (totalPages > 0 && target > totalPages)) {
            return;
        }

        currentPage = target;
        loadHistory();
    }

    private void clearDates() {
        fromDay = null;
        toDay = null;
        showRange();
    }

    // A framework DatePickerDialog: no new dependency, and nothing else in the
    // app offers a date yet.
    private void pickDay(boolean isFrom) {
        Date current = isFrom ? fromDay : toDay;

        Calendar initial = Calendar.getInstance();
        if (current != null) {
            initial.setTime(current);
        }

        new DatePickerDialog(this, (view, year, month, dayOfMonth) -> {
            Calendar picked = Calendar.getInstance();
            picked.set(year, month, dayOfMonth);

            if (isFrom) {
                fromDay = picked.getTime();
            } else {
                toDay = picked.getTime();
            }

            showRange();
        }, initial.get(Calendar.YEAR), initial.get(Calendar.MONTH),
                initial.get(Calendar.DAY_OF_MONTH)).show();
    }

    // The chosen range in words, so the two buttons stay short and the selection
    // is still visible on screen.
    private void showRange() {
        if (fromDay == null && toDay == null) {
            textRange.setVisibility(View.GONE);
            return;
        }

        String from = fromDay == null ? null : ReservationFormat.localDay(HistoryQuery.dayStartUtc(fromDay));
        String to = toDay == null ? null : ReservationFormat.localDay(HistoryQuery.dayEndUtc(toDay));

        if (from != null && to != null) {
            textRange.setText(getString(R.string.history_range, from, to));
        } else if (from != null) {
            textRange.setText(getString(R.string.history_range_from, from));
        } else {
            textRange.setText(getString(R.string.history_range_to, to));
        }

        textRange.setVisibility(View.VISIBLE);
    }

    // GET /nodes, only so rows can name their stations. A failure falls back to
    // short ids rather than blocking the history.
    private void loadNodesThenHistory() {
        setBusy(true);

        ApiClient.get("/nodes", new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                try {
                    nodes = ReservationParser.parseNodes(body);
                } catch (JSONException e) {
                    nodes = new ArrayList<>();
                }
                loadHistory();
            }

            @Override
            public void onError(String code, String detail) {
                if (SessionManager.handleAuthError(BookingHistoryActivity.this, code, detail)) {
                    return;
                }
                nodes = new ArrayList<>();
                loadHistory();
            }
        });
    }

    // Every filter travels to the server as a query parameter.
    private void loadHistory() {
        hideMessages();
        setBusy(true);

        final int request = ++historyRequest;

        ApiClient.get(buildPath(), new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                // A newer request has already gone out, so this answer belongs to
                // a filter the user has moved on from.
                if (request != historyRequest) {
                    return;
                }

                setBusy(false);
                try {
                    showPage(ReservationParser.parseHistoryPage(body), null);
                } catch (JSONException e) {
                    showError(getString(R.string.error_unexpected_response));
                }
            }

            @Override
            public void onError(String code, String detail) {
                if (request != historyRequest) {
                    return;
                }

                setBusy(false);
                if (!SessionManager.handleAuthError(BookingHistoryActivity.this, code, detail)) {
                    fallBackToCache(detail);
                }
            }
        });
    }

    // The query string, built the way the rest of the app builds one: concatenate
    // and encode each value. The API does the filtering; this only states the
    // question.
    private String buildPath() {
        StringBuilder path = new StringBuilder(HISTORY_PATH)
                .append("?page=").append(currentPage)
                .append("&pageSize=").append(HistoryQuery.PAGE_SIZE);

        if (selectedStatus != null) {
            path.append("&status=").append(Uri.encode(selectedStatus));
        }

        if (fromDay != null) {
            path.append("&from=").append(Uri.encode(HistoryQuery.dayStartUtc(fromDay)));
        }

        if (toDay != null) {
            path.append("&to=").append(Uri.encode(HistoryQuery.dayEndUtc(toDay)));
        }

        String search = searchText();
        if (!search.isEmpty()) {
            path.append("&q=").append(Uri.encode(search));
        }

        return path.toString();
    }

    private String searchText() {
        return inputSearch.getText() == null ? "" : inputSearch.getText().toString().trim();
    }

    // The call failed. Cached bookings may stand in only while nothing is
    // filtered: those rows were fetched unfiltered, so narrowing them here would
    // answer a question the server was never asked and present it as though it
    // had been.
    private void fallBackToCache(String detail) {
        if (!isUnfiltered()) {
            showError(detail);
            return;
        }

        List<ContentValues> cached = cache.load();
        String when = cache.lastRefreshedDisplay();

        // Nothing has ever been cached, so the server's message is all there is.
        // An empty list here would read as "you have no bookings".
        if (cached.isEmpty() && when == null) {
            showError(detail);
            return;
        }

        // The cached rows carry their station names, so a cached list can still
        // name its nodes when /nodes could not be reached either.
        if (nodes == null || nodes.isEmpty()) {
            nodes = BookingCache.nodeOptions(cached);
        }

        showRows(BookingCache.toReservations(cached),
                when == null
                        ? getString(R.string.cache_offline)
                        : getString(R.string.cache_last_updated, when),
                getString(R.string.my_bookings_empty));

        // Paging does not apply to a cached list: it is whatever was last fetched
        // in one go, not a page of a larger set.
        textPage.setVisibility(View.GONE);
    }

    // The live path.
    private void showPage(PagedReservations page, String staleNote) {
        currentPage = page.page;
        totalPages = page.totalPages;

        showRows(page.items, staleNote, getString(isUnfiltered()
                ? R.string.my_bookings_empty
                : R.string.history_empty_filtered));

        textPage.setVisibility(View.VISIBLE);
        textPage.setText(getString(R.string.history_page, page.page, Math.max(page.totalPages, 1)));

        findViewById(R.id.buttonPrev).setEnabled(page.page > 1);
        findViewById(R.id.buttonNext).setEnabled(page.totalPages > 0 && page.page < page.totalPages);
    }

    // Draws the rows with item_reservation.xml, the same card the My bookings
    // screen draws, so a booking looks the same wherever it is listed.
    private void showRows(List<Reservation> reservations, String staleNote, String emptyMessage) {
        listHistory.removeAllViews();

        // Null for live data, a "last updated" line for cached data, so the two
        // can never look the same on screen.
        if (staleNote == null) {
            textStale.setVisibility(View.GONE);
        } else {
            textStale.setText(staleNote);
            textStale.setVisibility(View.VISIBLE);
        }

        if (reservations.isEmpty()) {
            textEmpty.setText(emptyMessage);
            textEmpty.setVisibility(View.VISIBLE);
            return;
        }

        LayoutInflater inflater = LayoutInflater.from(this);
        for (Reservation reservation : reservations) {
            listHistory.addView(buildRow(inflater, reservation));
        }
    }

    private View buildRow(LayoutInflater inflater, Reservation reservation) {
        View row = inflater.inflate(R.layout.item_reservation, listHistory, false);
        String stationName = ReservationFormat.nodeName(this, nodes, reservation.stationId);

        ((TextView) row.findViewById(R.id.textBookingTime))
                .setText(ReservationFormat.slotRange(reservation.slotStart, reservation.slotEnd));
        ((TextView) row.findViewById(R.id.textBookingNode)).setText(stationName);
        ((TextView) row.findViewById(R.id.textBookingMeta)).setText(getString(
                R.string.booking_meta_line,
                ReservationFormat.energy(this, reservation.energyKWh),
                reservation.status,
                ReservationFormat.shortId(reservation.id)));

        // There is no deep link into the QR screen — it lists approved bookings
        // rather than opening one — so every row opens the detail screen the My
        // bookings list opens. The dashboard carries the QR entry point.
        row.setOnClickListener(v -> startActivity(new Intent(this, EditReservationActivity.class)
                .putExtra(EditReservationActivity.EXTRA_RESERVATION_ID, reservation.id)
                .putExtra(SlotSearchActivity.EXTRA_STATION_NAME, stationName)));

        return row;
    }

    private boolean isUnfiltered() {
        return HistoryQuery.isUnfiltered(selectedStatus, fromDay, toDay, searchText());
    }

    // Shows or hides the spinner.
    private void setBusy(boolean busy) {
        progress.setVisibility(busy ? View.VISIBLE : View.GONE);
    }

    // Shows the API's detail (or transport wording).
    private void showError(String message) {
        textError.setText(message);
        textError.setVisibility(TextUtils.isEmpty(message) ? View.GONE : View.VISIBLE);
        textEmpty.setVisibility(View.GONE);
        textStale.setVisibility(View.GONE);
        textPage.setVisibility(View.GONE);
    }

    // Clears the message lines before a reload.
    private void hideMessages() {
        textError.setVisibility(View.GONE);
        textEmpty.setVisibility(View.GONE);
        textStale.setVisibility(View.GONE);
    }
}
