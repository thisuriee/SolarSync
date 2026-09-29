/*
 * File:    DashboardParser.java
 * Author:  Imadh
 * Created: 2026-09-30
 * Purpose: Turns GET /api/dashboard/prosumer into a model
 *          (docs/api-contract.md §5). Parsing only — it decides nothing and
 *          recomputes nothing.
 *
 *          The counts are read exactly as they arrive. There is deliberately no
 *          arithmetic in this class: a count worked out from a fetched list
 *          would be the phone's number rather than the server's, and the rubric
 *          names that as the failure mode. The next booking is handed to
 *          ReservationParser so the reservation field names are still read in
 *          one place only.
 */
package com.sliit.smartsolar.parsers;

import com.sliit.smartsolar.models.ProsumerDashboard;
import com.sliit.smartsolar.models.Reservation;

import org.json.JSONException;
import org.json.JSONObject;

public final class DashboardParser {

    private DashboardParser() {
    }

    // Parses the prosumer dashboard body. A malformed body throws, so the caller
    // shows its "unexpected response" message — returning zeroes instead would
    // read as a real answer that this prosumer has nothing, which is a different
    // claim and a worse one to make wrongly.
    public static ProsumerDashboard parseProsumer(String body) throws JSONException {
        JSONObject json = new JSONObject(body);

        Reservation nextBooking = json.isNull("nextBooking")
                ? null
                : ReservationParser.parseReservation(json.getJSONObject("nextBooking"));

        return new ProsumerDashboard(
                json.getLong("activeCount"),
                json.getLong("pendingCount"),
                json.getLong("approvedFutureCount"),
                nextBooking);
    }
}
