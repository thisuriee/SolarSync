/*
 * File:    EditReservationActivity.java
 * Author:  Thisuri
 * Created: 2026-09-28
 * Purpose: One booking (GET /reservations/{id}): details, audit trail, and —
 *          while it is Pending or Approved — change it (PUT, another slot
 *          and/or energy) or cancel it (PATCH cancel, after a confirmation).
 *
 *          The 12-hour notice rule is NOT evaluated here. The screen shows
 *          how long until the slot starts (a fact) and keeps Save and Cancel
 *          enabled, so a late request reaches the API and its
 *          RESERVATION_NOTICE_PERIOD detail is what the prosumer reads. The
 *          rule's number exists in exactly one place: ReservationService.
 */
package com.sliit.smartsolar.activities;

import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;

import androidx.activity.result.ActivityResult;
import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.appcompat.app.AppCompatActivity;

import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.textfield.TextInputEditText;
import com.google.android.material.textfield.TextInputLayout;
import com.sliit.smartsolar.R;
import com.sliit.smartsolar.models.AvailableSlot;
import com.sliit.smartsolar.models.Reservation;
import com.sliit.smartsolar.models.ReservationStatuses;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.network.DateUtils;
import com.sliit.smartsolar.parsers.ReservationParser;
import com.sliit.smartsolar.utils.ReservationFormat;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONException;
import org.json.JSONObject;

public class EditReservationActivity extends AppCompatActivity {

    /** Id of the reservation to open. Required. */
    public static final String EXTRA_RESERVATION_ID = "reservation_id";

    private TextInputLayout layoutEnergy;
    private TextInputEditText inputEnergy;
    private TextView textTimeUntil, textChosenSlot, textError;
    private ProgressBar progress;

    private String reservationPath;
    private Reservation reservation;
    private String stationName;

    /** The slot a save will send: the booking's own until another is picked. */
    private String chosenSlotId;
    private String chosenStationName;

    // Receives the slot chosen in SlotSearchActivity's pick mode.
    private final ActivityResultLauncher<Intent> pickSlot = registerForActivityResult(
            new ActivityResultContracts.StartActivityForResult(), this::onSlotPicked);

    // Binds the screen and loads the booking.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_edit_reservation);

        layoutEnergy = findViewById(R.id.layoutEnergy);
        inputEnergy = findViewById(R.id.inputEnergy);
        textTimeUntil = findViewById(R.id.textTimeUntil);
        textChosenSlot = findViewById(R.id.textChosenSlot);
        textError = findViewById(R.id.textError);
        progress = findViewById(R.id.progress);

        String id = getIntent().getStringExtra(EXTRA_RESERVATION_ID);
        if (id == null) {
            finish();
            return;
        }
        reservationPath = "/reservations/" + Uri.encode(id);
        stationName = getIntent().getStringExtra(SlotSearchActivity.EXTRA_STATION_NAME);

        findViewById(R.id.buttonChooseSlot).setOnClickListener(v -> openSlotPicker());
        findViewById(R.id.buttonSave).setOnClickListener(v -> saveChanges());
        findViewById(R.id.buttonCancelBooking).setOnClickListener(v -> confirmCancel());

        loadReservation();
    }

    // GET /reservations/{id}. The API answers 403 AUTH_FORBIDDEN_ROLE if the
    // booking is not the caller's, and that detail is shown as-is.
    private void loadReservation() {
        setBusy(true);

        ApiClient.get(reservationPath, new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                setBusy(false);
                try {
                    reservation = ReservationParser.parseReservation(body);
                } catch (JSONException e) {
                    showError(getString(R.string.error_unexpected_response));
                    return;
                }
                showReservation();
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false);
                if (!SessionManager.handleAuthError(EditReservationActivity.this, code, detail)) {
                    showError(detail);
                }
            }
        });
    }

    // Fills details, audit trail and the change section from the API's values.
    private void showReservation() {
        Reservation r = reservation;
        if (stationName == null) {
            stationName = ReservationFormat.nodeName(this, null, r.stationId);
        }

        String until = ReservationFormat.timeUntil(this, r.slotStart);
        textTimeUntil.setText(until == null
                ? getString(R.string.edit_started)
                : getString(R.string.edit_starts_in, until));

        LinearLayout details = findViewById(R.id.cardDetails);
        details.removeAllViews();
        ReservationFormat.addDetailRow(details, R.string.label_reference, ReservationFormat.shortId(r.id));
        ReservationFormat.addDetailRow(details, R.string.label_status, r.status);
        ReservationFormat.addDetailRow(details, R.string.label_node, stationName);
        ReservationFormat.addDetailRow(details, R.string.label_slot,
                ReservationFormat.slotRange(r.slotStart, r.slotEnd));
        ReservationFormat.addDetailRow(details, R.string.label_energy, ReservationFormat.energy(this, r.energyKWh));
        findViewById(R.id.cardDetailsFrame).setVisibility(View.VISIBLE);

        // Audit trail: each row appears only once the API has recorded it.
        LinearLayout audit = findViewById(R.id.cardAudit);
        audit.removeAllViews();
        ReservationFormat.addDetailRow(audit, R.string.label_booked_at, DateUtils.toLocalDisplay(r.createdAt));
        ReservationFormat.addDetailRow(audit, R.string.label_updated_at, DateUtils.toLocalDisplay(r.updatedAt));
        ReservationFormat.addDetailRow(audit, R.string.label_approved_at, DateUtils.toLocalDisplay(r.approvedAt));
        ReservationFormat.addDetailRow(audit, R.string.label_rejection_reason, r.rejectionReason);
        ReservationFormat.addDetailRow(audit, R.string.label_cancelled_at, DateUtils.toLocalDisplay(r.cancelledAt));
        ReservationFormat.addDetailRow(audit, R.string.label_completed_at, DateUtils.toLocalDisplay(r.completedAt));
        findViewById(R.id.textAuditHeader).setVisibility(View.VISIBLE);
        findViewById(R.id.cardAuditFrame).setVisibility(View.VISIBLE);

        // UI hint only: a Rejected/Cancelled/Completed booking has no change
        // section. The API's transition guard refuses such a change anyway.
        boolean open = ReservationStatuses.isOpen(r.status);
        findViewById(R.id.groupChange).setVisibility(open ? View.VISIBLE : View.GONE);
        findViewById(R.id.textClosed).setVisibility(open ? View.GONE : View.VISIBLE);

        chosenSlotId = r.slotId;
        chosenStationName = stationName;
        textChosenSlot.setText(getString(R.string.edit_chosen_slot,
                ReservationFormat.slotRange(r.slotStart, r.slotEnd), stationName));
        inputEnergy.setText(ReservationFormat.plainNumber(r.energyKWh));
    }

    // Opens the slot search in pick mode, starting at this booking's node.
    private void openSlotPicker() {
        pickSlot.launch(new Intent(this, SlotSearchActivity.class)
                .putExtra(SlotSearchActivity.EXTRA_PICK_MODE, true)
                .putExtra(SlotSearchActivity.EXTRA_NODE_ID, reservation.stationId));
    }

    // Remembers the picked slot for the next save. Nothing is sent yet.
    private void onSlotPicked(ActivityResult result) {
        if (result.getResultCode() != RESULT_OK || result.getData() == null) {
            return;
        }

        try {
            AvailableSlot slot = ReservationParser.parseSlot(
                    result.getData().getStringExtra(SlotSearchActivity.EXTRA_SLOT_JSON));
            chosenSlotId = slot.id;
            chosenStationName = result.getData().getStringExtra(SlotSearchActivity.EXTRA_STATION_NAME);
            textChosenSlot.setText(getString(R.string.edit_chosen_slot,
                    ReservationFormat.slotRange(slot.slotStart, slot.slotEnd), chosenStationName));
            layoutEnergy.setHelperText(getString(R.string.create_energy_helper,
                    ReservationFormat.energy(this, slot.energyPerSlotKWh)));
        } catch (JSONException | NullPointerException e) {
            showError(getString(R.string.error_unexpected_response));
        }
    }

    // PUT /reservations/{id} {slotId, energyKWh}. The API applies the 12-hour
    // rule to the CURRENT booking, the 7-day rule and capacity to the new
    // slot, and claims the new space before releasing the old one.
    private void saveChanges() {
        hideError();
        Double energy = readEnergy();
        if (energy == null) {
            return;
        }

        String body;
        try {
            body = new JSONObject()
                    .put("slotId", chosenSlotId)
                    .put("energyKWh", energy)
                    .toString();
        } catch (JSONException e) {
            return; // Unreachable: energy is a finite number.
        }

        setBusy(true);

        ApiClient.put(reservationPath, body, new ApiCallback<String>() {
            @Override
            public void onSuccess(String responseBody) {
                setBusy(false);
                ReservationSummaryActivity.open(EditReservationActivity.this,
                        ReservationSummaryActivity.ResultType.UPDATED, responseBody, chosenStationName);
                finish();
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false);
                if (!SessionManager.handleAuthError(EditReservationActivity.this, code, detail)) {
                    showError(detail);
                }
            }
        });
    }

    // Asks before cancelling: a cancelled booking cannot be restored, and its
    // space goes back to other prosumers straight away.
    private void confirmCancel() {
        new MaterialAlertDialogBuilder(this)
                .setTitle(R.string.cancel_confirm_title)
                .setMessage(getString(R.string.cancel_confirm_body,
                        ReservationFormat.slotRange(reservation.slotStart, reservation.slotEnd), stationName))
                .setNegativeButton(R.string.action_keep_booking, null)
                .setPositiveButton(R.string.action_cancel_booking, (dialog, which) -> cancelBooking())
                .show();
    }

    // PATCH /reservations/{id}/cancel, sent by ApiClient as POST +
    // X-HTTP-Method-Override. The API applies the 12-hour rule and releases
    // the slot space; a late cancel comes back as its own detail.
    private void cancelBooking() {
        hideError();
        setBusy(true);

        // "{}" rather than no body: IIS answers 411 to a POST without a
        // Content-Length, and the route takes no fields.
        ApiClient.patch(reservationPath + "/cancel", "{}", new ApiCallback<String>() {
            @Override
            public void onSuccess(String responseBody) {
                setBusy(false);
                ReservationSummaryActivity.open(EditReservationActivity.this,
                        ReservationSummaryActivity.ResultType.CANCELLED, responseBody, stationName);
                finish();
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false);
                if (!SessionManager.handleAuthError(EditReservationActivity.this, code, detail)) {
                    showError(detail);
                }
            }
        });
    }

    // Field shape only: the entry must be a number. Whether the amount is
    // allowed is decided by the API (RESERVATION_INVALID_ENERGY).
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

    // Shows the spinner and disables Save / Cancel while a request is in
    // flight, so neither can be sent twice.
    private void setBusy(boolean busy) {
        progress.setVisibility(busy ? View.VISIBLE : View.GONE);
        findViewById(R.id.buttonSave).setEnabled(!busy);
        findViewById(R.id.buttonCancelBooking).setEnabled(!busy);
        findViewById(R.id.buttonChooseSlot).setEnabled(!busy);
    }

    // Shows the API's detail (or transport wording).
    private void showError(String message) {
        textError.setText(message);
        textError.setVisibility(TextUtils.isEmpty(message) ? View.GONE : View.VISIBLE);
    }

    // Hides the error line before a new attempt.
    private void hideError() {
        textError.setVisibility(View.GONE);
    }
}
