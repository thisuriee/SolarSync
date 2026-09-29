/*
 * File:    NodeParser.java
 * Author:  Aman
 * Created: 2026-09-29
 * Purpose: Turns the node endpoints' JSON into Node objects. Parsing only —
 *          it does not filter, sort or compute anything. The API already
 *          returns nearby nodes ordered nearest-first, and re-sorting here
 *          would throw away the ordering the geospatial index produced.
 *
 *          Every field is read with the opt* accessors. An absent field is not
 *          the same as a null one, and the throwing getters turn a missing
 *          optional into a crash — distanceKm is absent on GET /nodes and
 *          present on GET /nodes/nearby, and one parser reads both shapes.
 */
package com.sliit.smartsolar.parsers;

import com.sliit.smartsolar.models.Node;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

public final class NodeParser {

    private NodeParser() {
    }

    // Reads an array of nodes, keeping the order the API sent them in.
    public static List<Node> parseList(String body) throws JSONException {
        JSONArray array = new JSONArray(body);
        List<Node> nodes = new ArrayList<>(array.length());

        for (int i = 0; i < array.length(); i++) {
            nodes.add(parse(array.getJSONObject(i)));
        }

        return nodes;
    }

    // Reads one node object.
    public static Node parseOne(String body) throws JSONException {
        return parse(new JSONObject(body));
    }

    // Maps one JSON object onto the model. distanceKm falls back to a sentinel
    // rather than 0, because 0 km is a real answer: it is what a node sitting
    // exactly where the phone is would return.
    private static Node parse(JSONObject o) {
        return new Node(
                o.optString("id", ""),
                o.optString("stationName", ""),
                o.optString("addressLine", ""),
                o.optString("city", ""),
                o.optDouble("lat", 0),
                o.optDouble("lng", 0),
                o.optDouble("capacityKWh", 0),
                o.optInt("totalBatterySlots", 0),
                o.optString("status", ""),
                o.isNull("distanceKm") ? Node.NO_DISTANCE : o.optDouble("distanceKm", Node.NO_DISTANCE));
    }
}
