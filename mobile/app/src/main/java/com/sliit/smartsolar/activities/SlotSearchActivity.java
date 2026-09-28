/*
 * File:    SlotSearchActivity.java
 * Author:  Thisuri
 * Created: 2026-09-28
 * Purpose: A prosumer finds a bookable window: choose a node (GET /nodes), then
 *          pick from GET /slots/available, grouped by local day. Tapping a slot
 *          opens CreateReservationActivity. In pick mode (started by
 *          EditReservationActivity) the chosen slot is returned instead.
 *
 *          /slots/available is NEVER cached: it is fetched again every time
 *          this screen comes to the front, because capacity changes with every
 *          other prosumer's booking. Which windows are bookable (open, inside
 *          the booking window, spaces left) is decided by the API's query —
 *          this screen shows the list exactly as returned.
 */
package com.sliit.smartsolar.activities;

import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.google.android.material.textfield.MaterialAutoCompleteTextView;
import com.sliit.smartsolar.R;
import com.sliit.smartsolar.models.AvailableSlot;
import com.sliit.smartsolar.models.NodeOption;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.network.DateUtils;
import com.sliit.smartsolar.parsers.ReservationParser;
import com.sliit.smartsolar.utils.ReservationFormat;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONException;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Date;
import java.util.List;

public class SlotSearchActivity extends AppCompatActivity {

    /** true = return the chosen slot to the caller instead of booking it. */
    public static final String EXTRA_PICK_MODE = "pick_mode";

    /** Optional node to pre-select (the edit screen passes the booking's node). */
    public static final String EXTRA_NODE_ID = "node_id";

    /** The chosen slot, as the API's JSON, for CreateReservation or the picker's caller. */
    public static final String EXTRA_SLOT_JSON = "slot_json";

    /** Display name of the chosen slot's node. */
    public static final String EXTRA_STATION_NAME = "station_name";

    private MaterialAutoCompleteTextView inputNode;
    private LinearLayout listSlots;
    private TextView textError, textEmpty;
    private ProgressBar progress;

    private final List<NodeOption> nodes = new ArrayList<>();
    private boolean pickMode;
    private boolean nodesLoaded;

    /** null means "all nodes": /slots/available is called without nodeId. */
    private String selectedNodeId;

    // Incremented per slot request, so a slow response for a node the user has
    // already switched away from cannot overwrite the newer list.
    private int slotRequest;

    // Binds the screen and loads the node list, which then triggers the first
    // slot load.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_slot_search);

        inputNode = findViewById(R.id.inputNode);
        listSlots = findViewById(R.id.listSlots);
        textError = findViewById(R.id.textError);
        textEmpty = findViewById(R.id.textEmpty);
        progress = findViewById(R.id.progress);

        pickMode = getIntent().getBooleanExtra(EXTRA_PICK_MODE, false);
        selectedNodeId = getIntent().getStringExtra(EXTRA_NODE_ID);

        if (pickMode) {
            ((TextView) findViewById(R.id.textTitle)).setText(R.string.slot_pick_title);
        }

        inputNode.setOnItemClickListener((parent, view, position, id) -> {
            // Position 0 is "All nodes"; the rest follow the nodes list.
            selectedNodeId = position == 0 ? null : nodes.get(position - 1).id;
            loadSlots();
        });
        findViewById(R.id.buttonRefresh).setOnClickListener(v -> loadSlots());

        loadNodes();
    }

    // Rule: never cache /slots/available. Every return to this screen (e.g.
    // back from a failed booking because the slot filled up) fetches it again.
    // The first resume is skipped only because loadNodes() is about to do it.
    @Override
    protected void onResume() {
        super.onResume();
        if (nodesLoaded) {
            loadSlots();
        }
    }

    // GET /nodes — the API returns only active nodes to a prosumer. A failure
    // here is not fatal: slots still load for "All nodes", shown by id.
    private void loadNodes() {
        setBusy(true);

        ApiClient.get("/nodes", new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                try {
                    nodes.clear();
                    nodes.addAll(ReservationParser.parseNodes(body));
                } catch (JSONException e) {
                    showError(getString(R.string.error_unexpected_response));
                }
                onNodesReady();
            }

            @Override
            public void onError(String code, String detail) {
                if (SessionManager.handleAuthError(SlotSearchActivity.this, code, detail)) {
                    return;
                }
                showError(detail);
                onNodesReady();
            }
        });
    }

    // Fills the node drop-down ("All nodes" first), pre-selects the requested
    // node if it is in the list, then loads that node's slots.
    private void onNodesReady() {
        List<String> labels = new ArrayList<>();
        labels.add(getString(R.string.slot_search_all_nodes));
        int selected = 0;

        for (int i = 0; i < nodes.size(); i++) {
            labels.add(nodes.get(i).toString());
            if (nodes.get(i).id.equals(selectedNodeId)) {
                selected = i + 1;
            }
        }

        if (selected == 0) {
            selectedNodeId = null; // Requested node no longer listed: show all.
        }

        inputNode.setAdapter(new ArrayAdapter<>(this, android.R.layout.simple_list_item_1, labels));
        inputNode.setText(labels.get(selected), false);

        nodesLoaded = true;
        loadSlots();
    }

    // GET /slots/available[?nodeId=]. No date parameter: the API reads "date"
    // as a UTC day, which is not the prosumer's local day in UTC+5:30, so the
    // whole bookable range is fetched and grouped by LOCAL day on screen.
    private void loadSlots() {
        final int request = ++slotRequest;
        String path = "/slots/available";
        if (selectedNodeId != null) {
            path += "?nodeId=" + Uri.encode(selectedNodeId);
        }

        hideMessages();
        setBusy(true);

        ApiClient.get(path, new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                if (request != slotRequest) {
                    return; // A newer request is in flight; drop this answer.
                }
                setBusy(false);
                try {
                    showSlots(ReservationParser.parseSlots(body));
                } catch (JSONException e) {
                    listSlots.removeAllViews();
                    showError(getString(R.string.error_unexpected_response));
                }
            }

            @Override
            public void onError(String code, String detail) {
                if (request != slotRequest) {
                    return;
                }
                setBusy(false);
                if (!SessionManager.handleAuthError(SlotSearchActivity.this, code, detail)) {
                    listSlots.removeAllViews();
                    showError(detail);
                }
            }
        });
    }

    // Renders the slots in start order with a header for each local day.
    private void showSlots(List<AvailableSlot> slots) {
        listSlots.removeAllViews();
        textEmpty.setVisibility(slots.isEmpty() ? View.VISIBLE : View.GONE);

        Collections.sort(slots, (a, b) -> Long.compare(startMillis(a), startMillis(b)));

        LayoutInflater inflater = getLayoutInflater();
        String currentDay = null;

        for (AvailableSlot slot : slots) {
            String day = ReservationFormat.localDay(slot.slotStart);
            if (!day.equals(currentDay)) {
                TextView header = (TextView) inflater.inflate(R.layout.item_section_header, listSlots, false);
                header.setText(day);
                listSlots.addView(header);
                currentDay = day;
            }
            listSlots.addView(buildSlotRow(inflater, slot));
        }
    }

    // One slot card. The capacity line is the API's live figure at the moment
    // of this fetch; the booking itself re-checks it atomically on the server.
    private View buildSlotRow(LayoutInflater inflater, AvailableSlot slot) {
        View row = inflater.inflate(R.layout.item_slot, listSlots, false);
        String stationName = ReservationFormat.nodeName(this, nodes, slot.stationId);

        ((TextView) row.findViewById(R.id.textSlotTime))
                .setText(ReservationFormat.slotRange(slot.slotStart, slot.slotEnd));
        ((TextView) row.findViewById(R.id.textSlotNode)).setText(stationName);
        ((TextView) row.findViewById(R.id.textSlotCapacity)).setText(getString(
                R.string.slot_capacity_line,
                slot.availableCapacity,
                slot.totalCapacity,
                ReservationFormat.energy(this, slot.energyPerSlotKWh)));

        row.setOnClickListener(v -> onSlotChosen(slot, stationName));
        return row;
    }

    // Pick mode hands the slot back to the edit screen; otherwise it opens the
    // booking form. The slot travels as the API's own JSON.
    private void onSlotChosen(AvailableSlot slot, String stationName) {
        Intent data = new Intent()
                .putExtra(EXTRA_SLOT_JSON, slot.rawJson)
                .putExtra(EXTRA_STATION_NAME, stationName);

        if (pickMode) {
            setResult(RESULT_OK, data);
            finish();
            return;
        }

        data.setClass(this, CreateReservationActivity.class);
        startActivity(data);
    }

    // Sort key: the slot's start instant (ISO strings with differing fraction
    // lengths do not sort correctly as text).
    private static long startMillis(AvailableSlot slot) {
        Date start = DateUtils.parseUtc(slot.slotStart);
        return start == null ? Long.MAX_VALUE : start.getTime();
    }

    // Shows the spinner and blocks re-entry while a request is in flight.
    private void setBusy(boolean busy) {
        progress.setVisibility(busy ? View.VISIBLE : View.GONE);
        findViewById(R.id.buttonRefresh).setEnabled(!busy);
    }

    // Shows the API's detail (or transport wording).
    private void showError(String message) {
        textError.setText(message);
        textError.setVisibility(TextUtils.isEmpty(message) ? View.GONE : View.VISIBLE);
    }

    // Clears the error and empty-list lines before a new load.
    private void hideMessages() {
        textError.setVisibility(View.GONE);
        textEmpty.setVisibility(View.GONE);
    }
}
