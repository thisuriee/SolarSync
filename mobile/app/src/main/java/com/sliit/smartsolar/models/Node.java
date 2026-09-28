/*
 * File:    Node.java
 * Author:  Aman
 * Created: 2026-09-29
 * Purpose: One microgrid node as the API describes it (GET /nodes,
 *          GET /nodes/nearby). Holds data only.
 *
 *          latitude and longitude are whatever the server stored; the app never
 *          invents a coordinate. distanceKm is only present on the nearby
 *          search, because how far away a node is has no meaning except
 *          relative to where the phone is standing — and it is measured by the
 *          database, never worked out here.
 */
package com.sliit.smartsolar.models;

public class Node {

    /**
     * Stands in for "the server sent no distance for this node". Not 0, because
     * 0 km is a real answer — it is what a node sitting exactly where the phone
     * is would return.
     */
    public static final double NO_DISTANCE = -1;

    public final String id;
    public final String stationName;
    public final String addressLine;
    public final String city;

    /** Straight from the node document. Nothing in this app computes these. */
    public final double lat;
    public final double lng;

    public final double capacityKWh;
    public final int totalBatterySlots;
    public final String status;

    /**
     * Kilometres from the searched point, as measured by the server.
     * Negative means the API did not supply one (this node came from a plain
     * listing rather than a proximity search), so nothing should be shown.
     */
    public final double distanceKm;

    // Plain value holder; NodeParser fills it from the API's JSON.
    public Node(String id, String stationName, String addressLine, String city,
                double lat, double lng, double capacityKWh, int totalBatterySlots,
                String status, double distanceKm) {
        this.id = id;
        this.stationName = stationName;
        this.addressLine = addressLine;
        this.city = city;
        this.lat = lat;
        this.lng = lng;
        this.capacityKWh = capacityKWh;
        this.totalBatterySlots = totalBatterySlots;
        this.status = status;
        this.distanceKm = distanceKm;
    }

    /** True when the server supplied a distance for this node. */
    public boolean hasDistance() {
        return distanceKm >= 0;
    }

    /** "Colombo · 2.19 km", or just the city when there is no distance. */
    public String subtitle() {
        String place = city == null || city.isEmpty() ? addressLine : city;
        return hasDistance() ? place + " · " + distanceKm + " km" : place;
    }
}
