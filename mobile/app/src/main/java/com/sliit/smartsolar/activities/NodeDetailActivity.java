/*
 * File:    NodeDetailActivity.java
 * Author:  Aman
 * Created: 2026-09-29
 * Purpose: One microgrid node in full (GET /nodes/{id}) — where it is, how much
 *          it holds, and how many of its booking windows are still open.
 *          Opened by tapping a marker on the nearby map.
 *
 *          Only the node's id is passed in. The screen asks the API for the
 *          node itself rather than carrying a copy from the map, so what it
 *          shows is the current state and not a snapshot taken minutes ago.
 */
package com.sliit.smartsolar.activities;

import android.os.Bundle;
import android.view.View;
import android.widget.ProgressBar;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.sliit.smartsolar.R;
import com.sliit.smartsolar.models.Node;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.parsers.NodeParser;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONException;
import org.json.JSONObject;

public class NodeDetailActivity extends AppCompatActivity {

    /** The node to show. The only thing the map passes across. */
    public static final String EXTRA_NODE_ID = "node_id";

    private ProgressBar progress;
    private TextView textError;
    private View content;

    // Binds the screen and loads the node named by the intent.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_node_detail);

        progress = findViewById(R.id.progress);
        textError = findViewById(R.id.textError);
        content = findViewById(R.id.content);

        String nodeId = getIntent().getStringExtra(EXTRA_NODE_ID);
        if (nodeId == null || nodeId.isEmpty()) {
            showError(getString(R.string.error_unexpected_response));
            return;
        }

        load(nodeId);
    }

    // GET /nodes/{id}. The response carries the node plus its window summary,
    // which is why this screen can show open windows without a second call.
    private void load(String nodeId) {
        progress.setVisibility(View.VISIBLE);

        ApiClient.get("/nodes/" + nodeId, new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                progress.setVisibility(View.GONE);
                try {
                    bind(NodeParser.parseOne(body), new JSONObject(body));
                } catch (JSONException e) {
                    showError(getString(R.string.error_unexpected_response));
                }
            }

            @Override
            public void onError(String code, String detail) {
                progress.setVisibility(View.GONE);
                if (SessionManager.handleAuthError(NodeDetailActivity.this, code, detail)) {
                    return;
                }
                // The server's own message. A node a prosumer may not see comes
                // back as not found, and that is what gets shown.
                showError(detail);
            }
        });
    }

    // Fills the screen. The window counts are read from the raw object because
    // they belong to this one response rather than to the node itself, so they
    // are not part of the shared Node model.
    private void bind(Node node, JSONObject raw) {
        ((TextView) findViewById(R.id.textStationName)).setText(node.stationName);
        ((TextView) findViewById(R.id.textAddress))
                .setText(getString(R.string.node_address, node.addressLine, node.city));
        ((TextView) findViewById(R.id.textCoordinates))
                .setText(getString(R.string.node_coordinates, node.lat, node.lng));
        ((TextView) findViewById(R.id.textCapacity))
                .setText(getString(R.string.node_capacity, node.capacityKWh));
        ((TextView) findViewById(R.id.textBatterySlots))
                .setText(String.valueOf(node.totalBatterySlots));
        ((TextView) findViewById(R.id.textOpenWindows)).setText(getString(
                R.string.node_open_windows,
                raw.optInt("openSlotCount", 0),
                raw.optInt("slotCount", 0)));

        TextView textDistance = findViewById(R.id.textDistance);
        if (node.hasDistance()) {
            textDistance.setText(getString(R.string.node_distance, node.distanceKm));
            textDistance.setVisibility(View.VISIBLE);
        } else {
            textDistance.setVisibility(View.GONE);
        }

        content.setVisibility(View.VISIBLE);
    }

    // Shows a failure in place of the content.
    private void showError(String message) {
        textError.setText(message);
        textError.setVisibility(View.VISIBLE);
        content.setVisibility(View.GONE);
    }
}
