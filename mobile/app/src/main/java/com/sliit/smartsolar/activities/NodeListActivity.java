/*
 * File:    NodeListActivity.java
 * Author:  Aman
 * Created: 2026-09-29
 * Purpose: Every microgrid node in service, as a list (GET /nodes). The map's
 *          companion: it needs no location fix and no map tiles, so it is the
 *          way in when the device cannot place itself or has no signal to
 *          spare. Tapping a row opens the same detail screen a marker does.
 *
 *          READ-THROUGH CACHING. The API is asked every single time this screen
 *          opens, and its answer is what gets rendered; the cache is written
 *          only after a call succeeds. The cache is read only when a call
 *          fails, and when it is, the screen says plainly when the data was
 *          taken. Nothing here decides anything from cached rows — which
 *          nodes exist and which are in service is the server's answer, and a
 *          saved copy of that answer is never treated as current.
 */
package com.sliit.smartsolar.activities;

import android.content.Intent;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.sliit.smartsolar.R;
import com.sliit.smartsolar.database.NodeCache;
import com.sliit.smartsolar.models.Node;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.parsers.NodeParser;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONException;

import java.util.List;

public class NodeListActivity extends AppCompatActivity {

    private NodeCache cache;

    private LinearLayout listNodes;
    private ProgressBar progress;
    private TextView textMessage;
    private TextView textStale;

    // Binds the screen and takes hold of the cache. The first load happens in
    // onResume, so returning from a node's details refreshes the list.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_node_list);

        cache = new NodeCache(this);

        listNodes = findViewById(R.id.listNodes);
        progress = findViewById(R.id.progress);
        textMessage = findViewById(R.id.textMessage);
        textStale = findViewById(R.id.textStale);

        findViewById(R.id.buttonRefresh).setOnClickListener(v -> load());
    }

    // Rule: ask the API every time. Coming back to this screen re-fetches
    // rather than trusting whatever was drawn before.
    @Override
    protected void onResume() {
        super.onResume();
        load();
    }

    // GET /nodes. On success the response is rendered and then written to the
    // cache — in that order, so what the user sees is always the server's
    // answer and never a round trip through local storage.
    private void load() {
        setBusy(true);

        ApiClient.get("/nodes", new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                setBusy(false);
                try {
                    List<Node> fresh = NodeParser.parseList(body);

                    render(fresh, null);
                    cache.save(fresh);
                } catch (JSONException e) {
                    showMessage(getString(R.string.error_unexpected_response));
                }
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false);
                if (SessionManager.handleAuthError(NodeListActivity.this, code, detail)) {
                    return;
                }
                fallBackToCache(detail);
            }
        });
    }

    // The call failed. Show the last known list if there is one, labelled with
    // when it was taken; otherwise show the server's own message. A cached list
    // is never presented as current — the label is what keeps it honest.
    private void fallBackToCache(String detail) {
        List<Node> cached = cache.load();

        if (cached.isEmpty()) {
            showMessage(detail);
            return;
        }

        String when = cache.lastRefreshedDisplay();
        render(cached, when == null
                ? getString(R.string.nodes_offline)
                : getString(R.string.nodes_last_updated, when));
    }

    // Draws the rows. `staleNote` is null for live data and a "last updated"
    // line for cached data, so the two can never look the same on screen.
    private void render(List<Node> nodes, String staleNote) {
        listNodes.removeAllViews();
        textMessage.setVisibility(View.GONE);

        if (staleNote == null) {
            textStale.setVisibility(View.GONE);
        } else {
            textStale.setText(staleNote);
            textStale.setVisibility(View.VISIBLE);
        }

        if (nodes.isEmpty()) {
            showMessage(getString(R.string.nodes_none));
            return;
        }

        LayoutInflater inflater = LayoutInflater.from(this);

        for (Node node : nodes) {
            View row = inflater.inflate(R.layout.item_node, listNodes, false);

            ((TextView) row.findViewById(R.id.textStationName)).setText(node.stationName);
            ((TextView) row.findViewById(R.id.textCity)).setText(node.subtitle());
            ((TextView) row.findViewById(R.id.textCapacity)).setText(
                    getString(R.string.node_row_capacity, node.capacityKWh, node.totalBatterySlots));

            row.setOnClickListener(v -> openDetail(node));
            listNodes.addView(row);
        }
    }

    // Opens the node's details — the same screen a map marker opens, given the
    // same id, so there is one place a node is described.
    private void openDetail(Node node) {
        Intent intent = new Intent(this, NodeDetailActivity.class);
        intent.putExtra(NodeDetailActivity.EXTRA_NODE_ID, node.id);
        startActivity(intent);
    }

    // Shows a message in place of the list.
    private void showMessage(String message) {
        listNodes.removeAllViews();
        textStale.setVisibility(View.GONE);
        textMessage.setText(message);
        textMessage.setVisibility(View.VISIBLE);
    }

    // Shows or hides the spinner.
    private void setBusy(boolean busy) {
        progress.setVisibility(busy ? View.VISIBLE : View.GONE);
    }
}
