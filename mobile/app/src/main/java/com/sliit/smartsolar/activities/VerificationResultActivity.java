/*
 * File:    VerificationResultActivity.java
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: The operator's confirmation screen. Posts the scanned token to
 *          /api/fulfilment/verify-qr, shows what the server returned, and
 *          finalises the transfer with the verificationId it was given.
 *
 *          No rule lives here. The screen never decides whether a token is
 *          valid, whether a booking is approved, or whether a transfer may be
 *          completed — it asks and displays the answer, including on failure.
 */
package com.sliit.smartsolar.activities;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.google.android.material.button.MaterialButton;
import com.sliit.smartsolar.R;
import com.sliit.smartsolar.models.VerifiedTransfer;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.parsers.QrParser;
import com.sliit.smartsolar.utils.ReservationFormat;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONObject;

public class VerificationResultActivity extends AppCompatActivity {

    /** The token decoded by QrScannerActivity, passed through unchanged. */
    public static final String EXTRA_TOKEN = "qr_token";

    private static final String VERIFY_PATH = "/fulfilment/verify-qr";

    // Clients switch on the code, never on the message text (docs/response-format.md).
    // The code picks which recovery button to offer; the detail is what is shown.
    private static final String CODE_QR_INVALID = "QR_INVALID";
    private static final String CODE_QR_EXPIRED = "QR_EXPIRED";
    private static final String CODE_QR_ALREADY_USED = "QR_ALREADY_USED";

    private TextView statusText;
    private LinearLayout detailContainer;
    private MaterialButton finaliseButton;
    private MaterialButton rescanButton;
    private MaterialButton homeButton;

    private String reservationId;
    private String verificationId;

    // Reads the scanned token and starts verification.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_verification_result);

        statusText = findViewById(R.id.textStatus);
        detailContainer = findViewById(R.id.detailContainer);
        finaliseButton = findViewById(R.id.buttonFinalise);
        rescanButton = findViewById(R.id.buttonRescan);
        homeButton = findViewById(R.id.buttonHome);

        finaliseButton.setOnClickListener(v -> finalise());
        rescanButton.setOnClickListener(v -> finish());
        homeButton.setOnClickListener(v -> {
            SessionManager.openHome(this);
            finish();
        });

        String token = getIntent().getStringExtra(EXTRA_TOKEN);
        if (token == null || token.trim().isEmpty()) {
            showFailure(getString(R.string.qr_unreadable), null);
            return;
        }

        verify(token);
    }

    // Asks the API what this token is. The token itself is the only thing sent —
    // identity, expiry and reservation state are all resolved server-side.
    private void verify(String token) {
        statusText.setText(R.string.verify_preparing);
        detailContainer.removeAllViews();
        hideActions();

        String body;
        try {
            body = new JSONObject().put("token", token).toString();
        } catch (Exception cannotSerialise) {
            showFailure(getString(R.string.qr_unreadable), null);
            return;
        }

        ApiClient.post(VERIFY_PATH, body, new ApiCallback<String>() {

            @Override
            public void onSuccess(String responseBody) {
                VerifiedTransfer transfer = QrParser.parseVerification(responseBody);
                if (transfer == null) {
                    showFailure(getString(R.string.qr_unreadable), null);
                    return;
                }

                render(transfer);
            }

            @Override
            public void onError(String code, String detail) {
                // An invalid token is not a reason to be logged out; a stale
                // session is, and that is handled in one place.
                if (SessionManager.handleAuthError(VerificationResultActivity.this, code, detail)) {
                    return;
                }

                showFailure(detail, code);
            }
        });
    }

    // Shows what the operator is about to confirm: who, where, and which window.
    private void render(VerifiedTransfer transfer) {
        reservationId = transfer.reservation.id;
        verificationId = transfer.verificationId;

        addRow(R.string.verify_prosumer, transfer.prosumerFullName);
        addRow(R.string.verify_nic, transfer.prosumerNic);
        addRow(R.string.verify_phone, transfer.prosumerPhone);
        addRow(R.string.verify_station, transfer.stationName);
        addRow(R.string.verify_city, transfer.stationCity);
        addRow(R.string.verify_slot, ReservationFormat.slotRange(
                transfer.reservation.slotStart, transfer.reservation.slotEnd));
        addRow(R.string.verify_energy, ReservationFormat.energy(this, transfer.reservation.energyKWh));
        addRow(R.string.verify_status, transfer.reservation.status);

        statusText.setText("");
        finaliseButton.setVisibility(View.VISIBLE);
    }

    // Completes the transfer. The verificationId is what proves a scan happened,
    // so this is the one call that cannot be reached by simply invoking a route.
    private void finalise() {
        if (reservationId == null || verificationId == null) {
            return;
        }

        finaliseButton.setEnabled(false);

        String body;
        try {
            body = new JSONObject().put("verificationId", verificationId).toString();
        } catch (Exception cannotSerialise) {
            finaliseButton.setEnabled(true);
            return;
        }

        ApiClient.patch("/reservations/" + reservationId + "/complete", body, new ApiCallback<String>() {

            @Override
            public void onSuccess(String responseBody) {
                // 204 No Content on success: there is no body to parse.
                statusText.setText(R.string.verify_completed);
                hideActions();
                homeButton.setVisibility(View.VISIBLE);
            }

            @Override
            public void onError(String code, String detail) {
                finaliseButton.setEnabled(true);

                if (SessionManager.handleAuthError(VerificationResultActivity.this, code, detail)) {
                    return;
                }

                // The scan was real, so a failure here is a state problem, not a
                // scanning problem. The API's detail is the message.
                statusText.setText(detail);
            }
        });
    }

    // Shows the API's own message and offers the recovery its code implies.
    private void showFailure(String detail, String code) {
        statusText.setText(detail);
        detailContainer.removeAllViews();
        hideActions();

        // A consumed token means the transfer already happened; scanning again
        // would only repeat the same answer, so the only way on is out.
        if (CODE_QR_ALREADY_USED.equals(code)) {
            homeButton.setVisibility(View.VISIBLE);
            return;
        }

        // A bad or expired code is worth retrying: the operator may have the wrong
        // QR on screen, or the prosumer's screen may have timed out.
        boolean retryable = CODE_QR_INVALID.equals(code) || CODE_QR_EXPIRED.equals(code);
        rescanButton.setVisibility(retryable ? View.VISIBLE : View.GONE);
        homeButton.setVisibility(retryable ? View.GONE : View.VISIBLE);
    }

    // Adds one label/value line using the shared detail row.
    private void addRow(int labelResId, String value) {
        if (value == null || value.trim().isEmpty()) {
            return;
        }

        View row = LayoutInflater.from(this).inflate(R.layout.item_detail_row, detailContainer, false);
        ((TextView) row.findViewById(R.id.textLabel)).setText(labelResId);
        ((TextView) row.findViewById(R.id.textValue)).setText(value);
        detailContainer.addView(row);
    }

    // Hides every action, so a state change never leaves a stale button behind.
    private void hideActions() {
        finaliseButton.setVisibility(View.GONE);
        rescanButton.setVisibility(View.GONE);
        homeButton.setVisibility(View.GONE);
    }
}
