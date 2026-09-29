/*
 * File:    ReservationParser.java
 * Author:  Thisuri
 * Created: 2026-09-28
 * Purpose: Turns the reservation vertical's success bodies (docs/api-contract.md
 *          §4, plus the two reads it depends on: GET /slots/available and
 *          GET /nodes) into model objects with org.json. Parsing only — no
 *          decisions, no filtering by rule.
 */
package com.sliit.smartsolar.parsers;

import com.sliit.smartsolar.models.AvailableSlot;
import com.sliit.smartsolar.models.NodeOption;
import com.sliit.smartsolar.models.PagedReservations;
import com.sliit.smartsolar.models.Reservation;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

public final class ReservationParser {

    private ReservationParser() {
    }

    // Parses one ReservationResponse body: POST /reservations, GET/PUT
    // /reservations/{id} and PATCH /reservations/{id}/cancel all return it.
    public static Reservation parseReservation(String body) throws JSONException {
        return parseReservation(new JSONObject(body));
    }

    // Parses the array returned by GET /reservations/mine.
    public static List<Reservation> parseReservationList(String body) throws JSONException {
        JSONArray array = new JSONArray(body);
        List<Reservation> list = new ArrayList<>(array.length());

        for (int i = 0; i < array.length(); i++) {
            list.add(parseReservation(array.getJSONObject(i)));
        }
        return list;
    }

    // Parses the paged envelope returned by GET /reservations/history
    // (docs/response-format.md: {items, page, pageSize, totalCount, totalPages}).
    // The page numbers arrive with the items, so the screen is told where it is
    // rather than working it out.
    public static PagedReservations parseHistoryPage(String body) throws JSONException {
        JSONObject json = new JSONObject(body);
        JSONArray array = json.getJSONArray("items");
        List<Reservation> list = new ArrayList<>(array.length());

        for (int i = 0; i < array.length(); i++) {
            list.add(parseReservation(array.getJSONObject(i)));
        }

        return new PagedReservations(
                list,
                json.optInt("page", 1),
                json.optInt("pageSize", list.size()),
                json.optLong("totalCount", list.size()),
                json.optInt("totalPages", 0));
    }

    // Parses the array returned by GET /slots/available. Each slot keeps its
    // own JSON so a screen can pass it on in an Intent unchanged.
    public static List<AvailableSlot> parseSlots(String body) throws JSONException {
        JSONArray array = new JSONArray(body);
        List<AvailableSlot> list = new ArrayList<>(array.length());

        for (int i = 0; i < array.length(); i++) {
            list.add(parseSlot(array.getJSONObject(i)));
        }
        return list;
    }

    // Parses one slot previously passed between screens as raw JSON.
    public static AvailableSlot parseSlot(String json) throws JSONException {
        return parseSlot(new JSONObject(json));
    }

    // Parses the array returned by GET /nodes into id + name pairs. The API
    // already limits a prosumer to active nodes; nothing is filtered here.
    public static List<NodeOption> parseNodes(String body) throws JSONException {
        JSONArray array = new JSONArray(body);
        List<NodeOption> list = new ArrayList<>(array.length());

        for (int i = 0; i < array.length(); i++) {
            JSONObject node = array.getJSONObject(i);
            list.add(new NodeOption(
                    node.getString("id"),
                    node.getString("stationName"),
                    AuthParser.optionalString(node, "city")));
        }
        return list;
    }

    // Maps one ReservationResponse object. Audit fields the API leaves null
    // (not yet approved, never cancelled, ...) stay null, never "null".
    //
    // Public because the dashboard's nextBooking is the same object nested in a
    // different body: re-reading these field names elsewhere would let the two
    // copies drift.
    public static Reservation parseReservation(JSONObject json) throws JSONException {
        return new Reservation(
                json.getString("id"),
                json.getString("prosumerNIC"),
                json.getString("stationId"),
                json.getString("slotId"),
                json.getString("slotStart"),
                json.getString("slotEnd"),
                json.getDouble("energyKWh"),
                json.getString("status"),
                json.getString("createdAt"),
                json.getString("updatedAt"),
                AuthParser.optionalString(json, "approvedAt"),
                AuthParser.optionalString(json, "rejectionReason"),
                AuthParser.optionalString(json, "cancelledAt"),
                AuthParser.optionalString(json, "completedAt"));
    }

    // Maps one SlotResponse object.
    private static AvailableSlot parseSlot(JSONObject json) throws JSONException {
        return new AvailableSlot(
                json.getString("id"),
                json.getString("stationId"),
                json.getString("slotStart"),
                json.getString("slotEnd"),
                json.getInt("totalCapacity"),
                json.getInt("availableCapacity"),
                json.getDouble("energyPerSlotKWh"),
                json.toString());
    }
}
