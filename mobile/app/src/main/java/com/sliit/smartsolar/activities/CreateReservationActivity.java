/*
 * File:    CreateReservationActivity.java
 * Author:  Thisuri
 * Created: 2026-09-28
 * Purpose: A prosumer books the slot chosen in SlotSearchActivity:
 *          POST /reservations {slotId, energyKWh}. The API enforces every rule
 *          — 7-day booking window, active prosumer and node, slot open with
 *          space (claimed atomically), energy limit, no duplicate booking —
 *          and this screen shows its detail when one fails. On success the
 *          shared ReservationSummaryActivity shows the created booking.
 */
package com.sliit.smartsolar.activities;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.google.android.material.textfield.TextInputEditText;
import com.google.android.material.textfield.TextInputLayout;
import com.sliit.smartsolar.R;
import com.sliit.smartsolar.models.AvailableSlot;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.parsers.ReservationParser;
import com.sliit.smartsolar.utils.ReservationFormat;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONException;
import org.json.JSONObject;

public class CreateReservationActivity extends AppCompatActivity {

    private TextInputLayout layoutEnergy;
    private TextInputEditText inputEnergy;
    private TextView textError;
    private ProgressBar progress;

    private AvailableSlot slot;
    private String stationName;

    // Reads the slot handed over by the search screen and shows it. A missing
    // or unreadable slot closes the screen: there is nothing to book.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_create_reservation);

        layoutEnergy = findViewById(R.id.layoutEnergy);
        inputEnergy = findViewById(R.id.inputEnergy);
        textError = findViewById(R.id.textError);
        progress = findViewById(R.id.progress);

        stationName = getIntent().getStringExtra(SlotSearchActivity.EXTRA_STATION_NAME);
        try {
            slot = ReservationParser.parseSlot(getIntent().getStringExtra(SlotSearchActivity.EXTRA_SLOT_JSON));
        } catch (JSONException | NullPointerException e) {
            finish();
            return;
        }

        showSlot();
        findViewById(R.id.buttonBook).setOnClickListener(v -> book());
    }

    // The slot as it was when the list was fetched. The helper text shows the
    // slot's own energy figure as a hint; the limit itself is checked by the API.
    private void showSlot() {
        LinearLayout card = findViewById(R.id.cardDetails);
        ReservationFormat.addDetailRow(card, R.string.label_node, stationName);
        ReservationFormat.addDetailRow(card, R.string.label_slot,
                ReservationFormat.slotRange(slot.slotStart, slot.slotEnd));
        ReservationFormat.addDetailRow(card, R.string.label_spaces_left,
                getString(R.string.slot_spaces_value, slot.availableCapacity, slot.totalCapacity));

        layoutEnergy.setHelperText(getString(R.string.create_energy_helper,
                ReservationFormat.energy(this, slot.energyPerSlotKWh)));
    }

    // POST /reservations. The body has no prosumerNIC: for a prosumer the API
    // takes the NIC from the JWT and would ignore one here anyway, so a
    // tampered body cannot book for someone else.
    private void book() {
        hideError();
        Double energy = readEnergy();
        if (energy == null) {
            return;
        }

        String body;
        try {
            body = new JSONObject()
                    .put("slotId", slot.id)
                    .put("energyKWh", energy)
                    .toString();
        } catch (JSONException e) {
            return; // Unreachable: energy is a finite number.
        }

        setBusy(true);

        ApiClient.post("/reservations", body, new ApiCallback<String>() {
            @Override
            public void onSuccess(String responseBody) {
                setBusy(false);
                ReservationSummaryActivity.open(CreateReservationActivity.this,
                        ReservationSummaryActivity.ResultType.CREATED, responseBody, stationName);
                finish();
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false);
                // RESERVATION_OUTSIDE_WINDOW, _SLOT_FULL, _DUPLICATE, ... all
                // arrive here; the API's detail is the message, word for word.
                if (!SessionManager.handleAuthError(CreateReservationActivity.this, code, detail)) {
                    showError(detail);
                }
            }
        });
    }

    // Field shape only: the entry must be a number, because a non-number
    // could not be sent as JSON at all. Whether the amount is allowed
    // (above zero, within the slot's limit) is the API's decision.
    private Double readEnergy() {
        String text = inputEnergy.getText() == null ? "" : inputEnergy.getText().toString().trim();
        if (text.isEmpty()) {
            layoutEnergy.setError(getString(R.string.error_required));
            return null;
        }

        Double value = ReservationFormat.parseEnergy(text);
        layoutEnergy.setError(value == null ? getString(R.string.error_energy_number) : null);
        return value;
    }

    // Shows the spinner and disables Book while a request is in flight, so a
    // double tap cannot send two bookings.
    private void setBusy(boolean busy) {
        progress.setVisibility(busy ? View.VISIBLE : View.GONE);
        findViewById(R.id.buttonBook).setEnabled(!busy);
    }

    // Shows the API's detail (or transport wording) under the form.
    private void showError(String message) {
        textError.setText(message);
        textError.setVisibility(TextUtils.isEmpty(message) ? View.GONE : View.VISIBLE);
    }

    // Hides the error line before a new attempt.
    private void hideError() {
        textError.setVisibility(View.GONE);
    }
}
