/*
 * File:    NearbyNodesMapActivity.java
 * Author:  Aman
 * Created: 2026-09-29
 * Purpose: Shows microgrid nodes near the device on a Google Map
 *          (GET /nodes/nearby). One marker per node, placed at the latitude and
 *          longitude the API supplied, with the distance the server measured
 *          printed beside the name.
 *
 *          This screen performs no geometry. It asks Android where the phone
 *          is, sends that to the API, and draws what comes back. No coordinate
 *          is written into the app, and no distance is calculated here — the
 *          database measures that against its geospatial index, so the two
 *          clients can never disagree about how far away a node is.
 *
 *          Which nodes are returned is the API's decision too: only nodes in
 *          service are listed, so a hub that has been taken out of service
 *          simply stops appearing without this screen filtering anything.
 */
package com.sliit.smartsolar.activities;

import android.Manifest;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.location.Location;
import android.net.Uri;
import android.os.Bundle;
import android.provider.Settings;
import android.util.Log;
import android.view.View;
import android.widget.ProgressBar;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;

import com.google.android.gms.location.FusedLocationProviderClient;
import com.google.android.gms.location.LocationServices;
import com.google.android.gms.location.Priority;
import com.google.android.gms.maps.CameraUpdateFactory;
import com.google.android.gms.maps.GoogleMap;
import com.google.android.gms.maps.OnMapReadyCallback;
import com.google.android.gms.maps.SupportMapFragment;
import com.google.android.gms.maps.model.LatLng;
import com.google.android.gms.maps.model.Marker;
import com.google.android.gms.maps.model.MarkerOptions;
import com.google.android.gms.tasks.CancellationTokenSource;
import com.google.android.material.chip.ChipGroup;
import com.sliit.smartsolar.R;
import com.sliit.smartsolar.utils.BottomNav;
import com.sliit.smartsolar.models.Node;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.parsers.NodeParser;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONException;

import java.util.List;

public class NearbyNodesMapActivity extends AppCompatActivity implements OnMapReadyCallback {

    /** Tag for the request trace. Logs the search, never a token or a name. */
    private static final String TAG = "NearbyNodes";

    /** The search radius the screen opens on. The API defaults to the same. */
    private static final int DEFAULT_RADIUS_KM = 25;

    /** Zoom that shows roughly a city and its surrounding towns. */
    private static final float CITY_ZOOM = 11f;

    private static final int REQUEST_LOCATION = 1001;

    private GoogleMap map;
    private FusedLocationProviderClient locationClient;
    private CancellationTokenSource locationCancellation;

    /** Currently selected search radius, in kilometres. */
    private int radiusKm = DEFAULT_RADIUS_KM;

    /** The last position obtained, so changing the radius re-queries without
        asking the device where it is all over again. */
    private Location lastKnown;

    private ProgressBar progress;
    private TextView textStatus;
    private View panelUnavailable;
    private TextView textUnavailable;

    /** Set once the user has been sent to settings, so returning here retries. */
    private boolean awaitingSettings;

    // Binds the screen and asks for the map. Everything else waits for
    // onMapReady, because there is nothing to draw on until then.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_nearby_nodes_map);
        BottomNav.attach(this, R.id.nav_map);

        progress = findViewById(R.id.progress);
        textStatus = findViewById(R.id.textStatus);
        panelUnavailable = findViewById(R.id.panelUnavailable);
        textUnavailable = findViewById(R.id.textUnavailable);

        bindRadiusChips();
        findViewById(R.id.buttonRetry).setOnClickListener(v -> start());
        findViewById(R.id.buttonSettings).setOnClickListener(v -> openAppSettings());

        locationClient = LocationServices.getFusedLocationProviderClient(this);

        SupportMapFragment fragment =
                (SupportMapFragment) getSupportFragmentManager().findFragmentById(R.id.mapFragment);
        if (fragment != null) {
            fragment.getMapAsync(this);
        }
    }

    // Wires the radius chips. Changing the radius asks the API a different
    // question — it does not filter a list already fetched — so each selection
    // is a fresh search from the same position.
    private void bindRadiusChips() {
        ChipGroup group = findViewById(R.id.groupRadius);

        group.setOnCheckedStateChangeListener((chipGroup, checkedIds) -> {
            if (checkedIds.isEmpty()) {
                return;
            }

            int id = checkedIds.get(0);
            if (id == R.id.radius10) {
                radiusKm = 10;
            } else if (id == R.id.radius50) {
                radiusKm = 50;
            } else if (id == R.id.radius100) {
                radiusKm = 100;
            } else {
                radiusKm = DEFAULT_RADIUS_KM;
            }

            // Re-search from the position already in hand. Nothing to do yet if
            // the first fix has not arrived.
            if (lastKnown != null) {
                loadNearby(lastKnown);
            }
        });
    }

    // Retries automatically when the user comes back from granting the
    // permission in system settings, so they do not have to press anything.
    @Override
    protected void onResume() {
        super.onResume();
        if (awaitingSettings && hasLocationPermission()) {
            awaitingSettings = false;
            start();
        }
    }

    // Drops any location request still in flight, so a late callback cannot
    // touch views on a screen the user has already left.
    @Override
    protected void onDestroy() {
        super.onDestroy();
        if (locationCancellation != null) {
            locationCancellation.cancel();
        }
    }

    // The map is ready. Markers are drawn later, once the device position and
    // the API's answer are both in hand.
    @Override
    public void onMapReady(@NonNull GoogleMap googleMap) {
        map = googleMap;
        map.getUiSettings().setZoomControlsEnabled(true);
        map.getUiSettings().setMyLocationButtonEnabled(false);

        // Tapping a marker opens the node's details. The node travelled with
        // the marker, so nothing is looked up again to answer the tap.
        map.setOnInfoWindowClickListener(marker -> {
            Object tag = marker.getTag();
            if (tag instanceof Node) {
                openDetail((Node) tag);
            }
        });

        start();
    }

    // Entry point for the whole flow, and the retry target. Asks for the
    // location permission if it is missing; otherwise goes straight on.
    private void start() {
        hideUnavailable();

        if (!hasLocationPermission()) {
            ActivityCompat.requestPermissions(
                    this,
                    new String[]{Manifest.permission.ACCESS_FINE_LOCATION,
                            Manifest.permission.ACCESS_COARSE_LOCATION},
                    REQUEST_LOCATION);
            return;
        }

        requestPosition();
    }

    // Rule: a denial is an outcome, not a failure. The screen explains what is
    // missing and offers a way forward; it never crashes and never falls back
    // to a made-up position, which would put markers somewhere nobody is.
    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions,
                                           @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode != REQUEST_LOCATION) {
            return;
        }

        if (hasLocationPermission()) {
            requestPosition();
            return;
        }

        // "Don't ask again" leaves shouldShowRequestPermissionRationale false,
        // and only then is the settings route the one that can help.
        boolean canAskAgain = ActivityCompat.shouldShowRequestPermissionRationale(
                this, Manifest.permission.ACCESS_FINE_LOCATION);

        showUnavailable(getString(R.string.map_permission_denied), !canAskAgain);
    }

    // Asks for the device's position. getCurrentLocation is used rather than
    // getLastLocation because a phone that has not fixed recently returns null
    // from the latter, which on a fresh device is most of the time.
    private void requestPosition() {
        setBusy(true, getString(R.string.map_locating));

        if (locationCancellation != null) {
            locationCancellation.cancel();
        }
        locationCancellation = new CancellationTokenSource();

        try {
            locationClient
                    .getCurrentLocation(Priority.PRIORITY_BALANCED_POWER_ACCURACY,
                            locationCancellation.getToken())
                    .addOnSuccessListener(location -> {
                        if (location == null) {
                            setBusy(false, null);
                            showUnavailable(getString(R.string.map_no_fix), false);
                            return;
                        }
                        lastKnown = location;
                        loadNearby(location);
                    })
                    .addOnFailureListener(e -> {
                        setBusy(false, null);
                        showUnavailable(getString(R.string.map_no_fix), false);
                    });
        } catch (SecurityException e) {
            // The permission was revoked between the check and the call.
            setBusy(false, null);
            showUnavailable(getString(R.string.map_permission_denied), false);
        }
    }

    // GET /nodes/nearby with the device's position. The API measures the
    // distances, orders the results nearest-first and returns nodes that are in
    // service — none of which is decided here.
    private void loadNearby(Location location) {
        setBusy(true, getString(R.string.map_loading_nodes));

        String path = "/nodes/nearby?lat=" + location.getLatitude()
                + "&lng=" + location.getLongitude()
                + "&radiusKm=" + radiusKm;

        Log.d(TAG, "requesting " + path);

        ApiClient.get(path, new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                setBusy(false, null);
                try {
                    List<Node> nodes = NodeParser.parseList(body);
                    Log.d(TAG, "received " + nodes.size() + " node(s)");
                    plot(nodes, location);
                } catch (JSONException e) {
                    showUnavailable(getString(R.string.error_unexpected_response), false);
                }
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false, null);
                Log.w(TAG, "nearby search failed: " + code);
                if (SessionManager.handleAuthError(NearbyNodesMapActivity.this, code, detail)) {
                    return;
                }
                // The server's own message, never one composed here.
                showUnavailable(detail, false);
            }
        });
    }

    // Draws one marker per node, at the coordinates the API supplied, and
    // centres on the device. The node is attached to its marker so a tap can
    // open its details without another request.
    private void plot(List<Node> nodes, Location location) {
        if (map == null) {
            return;
        }

        map.clear();

        for (Node node : nodes) {
            Marker marker = map.addMarker(new MarkerOptions()
                    .position(new LatLng(node.lat, node.lng))
                    .title(node.stationName)
                    .snippet(node.subtitle()));

            if (marker != null) {
                marker.setTag(node);
            }
        }

        LatLng here = new LatLng(location.getLatitude(), location.getLongitude());
        map.animateCamera(CameraUpdateFactory.newLatLngZoom(here, CITY_ZOOM));

        if (nodes.isEmpty()) {
            // Say what was searched and offer the way out, rather than leaving
            // an empty map looking broken.
            textStatus.setText(getString(R.string.map_no_nodes, radiusKm)
                    + " " + getString(R.string.map_widen_hint));
        } else {
            textStatus.setText(getResources().getQuantityString(
                    R.plurals.map_node_count, nodes.size(), nodes.size(), radiusKm));
        }
        textStatus.setVisibility(View.VISIBLE);
    }

    // Opens the node's detail screen. Only the id travels: the detail screen
    // asks the API for the node itself, so what it shows is current rather
    // than whatever this map happened to load.
    private void openDetail(Node node) {
        Intent intent = new Intent(this, NodeDetailActivity.class);
        intent.putExtra(NodeDetailActivity.EXTRA_NODE_ID, node.id);
        startActivity(intent);
    }

    // True when either location permission has been granted. Coarse is enough
    // to find nearby nodes; the search radius is kilometres wide.
    private boolean hasLocationPermission() {
        return ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION)
                == PackageManager.PERMISSION_GRANTED
                || ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_COARSE_LOCATION)
                == PackageManager.PERMISSION_GRANTED;
    }

    // Sends the user to this app's settings page, the only place a permanently
    // denied permission can be granted again.
    private void openAppSettings() {
        awaitingSettings = true;
        startActivity(new Intent(
                Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                Uri.fromParts("package", getPackageName(), null)));
    }

    // Shows or hides the progress strip and its caption.
    private void setBusy(boolean busy, String message) {
        progress.setVisibility(busy ? View.VISIBLE : View.GONE);
        textStatus.setVisibility(message == null ? View.GONE : View.VISIBLE);
        if (message != null) {
            textStatus.setText(message);
        }
    }

    // Explains why there are no markers, and offers the action that can fix it.
    private void showUnavailable(String message, boolean offerSettings) {
        textUnavailable.setText(message);
        findViewById(R.id.buttonSettings).setVisibility(offerSettings ? View.VISIBLE : View.GONE);
        panelUnavailable.setVisibility(View.VISIBLE);
    }

    // Clears the explanation panel before a retry.
    private void hideUnavailable() {
        panelUnavailable.setVisibility(View.GONE);
    }
}
