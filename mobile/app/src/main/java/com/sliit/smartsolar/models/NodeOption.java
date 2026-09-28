/*
 * File:    NodeOption.java
 * Author:  Thisuri
 * Created: 2026-09-28
 * Purpose: The two fields of a node (GET /nodes) the reservation screens
 *          need: its id, to filter slots, and a name to show instead of the
 *          id. Deliberately not a full node model — that belongs to M2's
 *          vertical. Holds data only.
 */
package com.sliit.smartsolar.models;

public class NodeOption {

    public final String id;
    public final String stationName;
    public final String city;

    // Plain value holder; ReservationParser fills it from the node list.
    public NodeOption(String id, String stationName, String city) {
        this.id = id;
        this.stationName = stationName;
        this.city = city;
    }

    // Shown by the node drop-down's ArrayAdapter.
    @Override
    public String toString() {
        return city == null || city.isEmpty() ? stationName : stationName + " · " + city;
    }
}
