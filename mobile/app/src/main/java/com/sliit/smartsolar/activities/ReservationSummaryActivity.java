/*
 * File:    ReservationSummaryActivity.java
 * Author:  Thisuri
 * Created: 2026-09-28
 * Purpose: ONE reusable outcome screen for the three reservation writes:
 *          CREATED (after POST), UPDATED (after PUT) and CANCELLED (after
 *          PATCH cancel). The caller passes the result type and the API's own
 *          response body; the type only picks the heading, message and which
 *          timestamp to highlight. Every value shown comes from that body, so
 *          the summary is what the server saved, not what the user typed.
 */
package com.sliit.smartsolar.activities;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.sliit.smartsolar.R;
import com.sliit.smartsolar.models.Reservation;
import com.sliit.smartsolar.network.DateUtils;
import com.sliit.smartsolar.parsers.ReservationParser;
import com.sliit.smartsolar.utils.ReservationFormat;

import org.json.JSONException;

public class ReservationSummaryActivity extends AppCompatActivity {

    private static final String EXTRA_RESULT_TYPE = "result_type";
    private static final String EXTRA_RESERVATION_JSON = "reservation_json";
    private static final String EXTRA_STATION_NAME = "station_name";

    /**
     * Which write just succeeded. Each type carries its own heading, message
     * and the label of the timestamp that records it in the audit trail.
     */
    public enum ResultType {
        CREATED(R.string.summary_created_title, R.string.summary_created_message, R.string.label_booked_at),
        UPDATED(R.string.summary_updated_title, R.string.summary_updated_message, R.string.label_updated_at),
        CANCELLED(R.string.summary_cancelled_title, R.string.summary_cancelled_message, R.string.label_cancelled_at);

        final int titleRes;
        final int messageRes;
        final int timeLabelRes;

        // Binds the three strings to the type.
        ResultType(int titleRes, int messageRes, int timeLabelRes) {
            this.titleRes = titleRes;
            this.messageRes = messageRes;
            this.timeLabelRes = timeLabelRes;
        }

        // The audit timestamp this outcome wrote: createdAt, updatedAt or cancelledAt.
        String timeOf(Reservation r) {
            switch (this) {
                case CREATED:
                    return r.createdAt;
                case CANCELLED:
                    return r.cancelledAt;
                default:
                    return r.updatedAt;
            }
        }
    }

    // The single way to open this screen, so every caller passes the same
    // three extras. responseBody is the API's ReservationResponse, unchanged.
    public static void open(Activity from, ResultType type, String responseBody, String stationName) {
        Intent intent = new Intent(from, ReservationSummaryActivity.class)
                .putExtra(EXTRA_RESULT_TYPE, type.name())
                .putExtra(EXTRA_RESERVATION_JSON, responseBody)
                .putExtra(EXTRA_STATION_NAME, stationName);
        from.startActivity(intent);
    }

    // Parses the API's body and fills the heading and details for the type.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_reservation_summary);

        findViewById(R.id.buttonMyBookings).setOnClickListener(v -> openAndClose(MyBookingsActivity.class));
        findViewById(R.id.buttonHome).setOnClickListener(v -> openAndClose(ProsumerHomeActivity.class));

        ResultType type;
        Reservation reservation;
        try {
            type = ResultType.valueOf(getIntent().getStringExtra(EXTRA_RESULT_TYPE));
            reservation = ReservationParser.parseReservation(getIntent().getStringExtra(EXTRA_RESERVATION_JSON));
        } catch (JSONException | IllegalArgumentException | NullPointerException e) {
            ((TextView) findViewById(R.id.textSummaryTitle)).setText(R.string.error_unexpected_response);
            return;
        }

        ((TextView) findViewById(R.id.textSummaryTitle)).setText(type.titleRes);
        ((TextView) findViewById(R.id.textSummaryMessage)).setText(type.messageRes);
        showDetails(type, reservation, getIntent().getStringExtra(EXTRA_STATION_NAME));
    }

    // The details card. The status is the API's value (Pending after a create,
    // Cancelled after a cancel), and the highlighted time is the audit field
    // the write set, in local time.
    private void showDetails(ResultType type, Reservation r, String stationName) {
        LinearLayout card = findViewById(R.id.cardDetails);

        ReservationFormat.addDetailRow(card, R.string.label_reference, ReservationFormat.shortId(r.id));
        ReservationFormat.addDetailRow(card, R.string.label_status, r.status);
        ReservationFormat.addDetailRow(card, R.string.label_node,
                stationName != null ? stationName : ReservationFormat.nodeName(this, null, r.stationId));
        ReservationFormat.addDetailRow(card, R.string.label_slot,
                ReservationFormat.slotRange(r.slotStart, r.slotEnd));
        ReservationFormat.addDetailRow(card, R.string.label_energy,
                ReservationFormat.energy(this, r.energyKWh));
        ReservationFormat.addDetailRow(card, type.timeLabelRes, DateUtils.toLocalDisplay(type.timeOf(r)));
    }

    // Returns to an existing My bookings / Home screen if it is in the back
    // stack (CLEAR_TOP), rather than stacking a second copy, then closes.
    private void openAndClose(Class<? extends Activity> target) {
        Intent intent = new Intent(this, target)
                .addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        startActivity(intent);
        finish();
    }
}
