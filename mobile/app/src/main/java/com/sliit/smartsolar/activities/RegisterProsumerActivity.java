/*
 * File:    RegisterProsumerActivity.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Prosumer self-registration (POST /auth/register-prosumer) and its
 *          "awaiting activation" outcome. The API validates the NIC, enforces
 *          NIC/username/email uniqueness and forces role=Prosumer and
 *          status=Pending; this screen checks field shape only and shows the
 *          API's detail for every rule failure.
 */
package com.sliit.smartsolar.activities;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.View;
import android.widget.Button;
import android.widget.ProgressBar;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.google.android.material.textfield.TextInputEditText;
import com.google.android.material.textfield.TextInputLayout;
import com.sliit.smartsolar.R;
import com.sliit.smartsolar.models.RegisterResult;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.parsers.AuthParser;
import com.sliit.smartsolar.utils.InputFormat;

import org.json.JSONException;
import org.json.JSONObject;

public class RegisterProsumerActivity extends AppCompatActivity {

    // Error codes from docs/response-format.md that belong to one field, so the
    // API's detail is shown under that field instead of at the bottom.
    private static final String CODE_NIC_INVALID = "USER_NIC_INVALID";
    private static final String CODE_NIC_EXISTS = "USER_NIC_EXISTS";
    private static final String CODE_USERNAME_EXISTS = "USER_USERNAME_EXISTS";
    private static final String CODE_EMAIL_EXISTS = "USER_EMAIL_EXISTS";

    private TextInputLayout layoutNic, layoutFullName, layoutEmail, layoutPhone,
            layoutAddress, layoutUsername, layoutPassword, layoutConfirmPassword;
    private TextInputEditText inputNic, inputFullName, inputEmail, inputPhone,
            inputAddress, inputUsername, inputPassword, inputConfirmPassword;
    private TextView textError;
    private ProgressBar progress;
    private Button buttonRegister;

    // Binds the form. Back-to-login simply closes this screen: the login
    // screen is underneath it on the back stack.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_register_prosumer);

        layoutNic = findViewById(R.id.layoutNic);
        layoutFullName = findViewById(R.id.layoutFullName);
        layoutEmail = findViewById(R.id.layoutEmail);
        layoutPhone = findViewById(R.id.layoutPhone);
        layoutAddress = findViewById(R.id.layoutAddress);
        layoutUsername = findViewById(R.id.layoutUsername);
        layoutPassword = findViewById(R.id.layoutPassword);
        layoutConfirmPassword = findViewById(R.id.layoutConfirmPassword);

        inputNic = findViewById(R.id.inputNic);
        inputFullName = findViewById(R.id.inputFullName);
        inputEmail = findViewById(R.id.inputEmail);
        inputPhone = findViewById(R.id.inputPhone);
        inputAddress = findViewById(R.id.inputAddress);
        inputUsername = findViewById(R.id.inputUsername);
        inputPassword = findViewById(R.id.inputPassword);
        inputConfirmPassword = findViewById(R.id.inputConfirmPassword);

        textError = findViewById(R.id.textError);
        progress = findViewById(R.id.progress);
        buttonRegister = findViewById(R.id.buttonRegister);

        buttonRegister.setOnClickListener(v -> attemptRegister());
        findViewById(R.id.buttonBackToLogin).setOnClickListener(v -> finish());
        findViewById(R.id.buttonDone).setOnClickListener(v -> finish());
    }

    // Rule 3 (registration): sends only the seven fields in the contract. There
    // is deliberately no role or status in the body — the API forces
    // Prosumer/Pending, and would ignore them anyway.
    private void attemptRegister() {
        hideError();
        if (!validateForm()) {
            return;
        }

        String body;
        try {
            body = new JSONObject()
                    .put("nic", textOf(inputNic))
                    .put("fullName", textOf(inputFullName))
                    .put("email", textOf(inputEmail))
                    .put("phone", textOf(inputPhone))
                    .put("address", textOf(inputAddress))
                    .put("username", textOf(inputUsername))
                    .put("password", rawTextOf(inputPassword))
                    .toString();
        } catch (JSONException e) {
            return; // Unreachable: put() only throws for non-finite numbers.
        }

        setBusy(true);

        ApiClient.post("/auth/register-prosumer", body, new ApiCallback<String>() {
            @Override
            public void onSuccess(String responseBody) {
                setBusy(false);
                onRegistered(responseBody);
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false);
                showApiError(code, detail);
            }
        });
    }

    // Field-shape checks mirroring the API's request annotations (see
    // InputFormat). The NIC is only checked for presence: whether its format is
    // valid, and whether it is already registered, is the API's decision.
    // Shows every problem at once and returns true when the form may be sent.
    private boolean validateForm() {
        boolean ok = check(layoutNic, InputFormat.required(textOf(inputNic)));
        ok &= check(layoutFullName, InputFormat.fullName(textOf(inputFullName)));
        ok &= check(layoutEmail, InputFormat.email(textOf(inputEmail)));
        ok &= check(layoutPhone, InputFormat.phone(textOf(inputPhone)));
        ok &= check(layoutAddress, InputFormat.address(textOf(inputAddress)));
        ok &= check(layoutUsername, InputFormat.username(textOf(inputUsername)));
        ok &= check(layoutPassword, InputFormat.password(rawTextOf(inputPassword)));

        // Typing aid only; the confirmation is never sent to the API.
        boolean match = rawTextOf(inputPassword).equals(rawTextOf(inputConfirmPassword));
        ok &= check(layoutConfirmPassword, match ? InputFormat.OK : R.string.error_password_mismatch);

        return ok;
    }

    // Shows or clears one field's error. Returns true when the field is fine.
    private boolean check(TextInputLayout layout, int errorRes) {
        layout.setError(errorRes == InputFormat.OK ? null : getString(errorRes));
        return errorRes == InputFormat.OK;
    }

    // Switches on the API's code to decide WHERE to show its detail, never
    // what it says. Codes that belong to no single field go under the form.
    private void showApiError(String code, String detail) {
        if (CODE_NIC_INVALID.equals(code) || CODE_NIC_EXISTS.equals(code)) {
            layoutNic.setError(detail);
        } else if (CODE_USERNAME_EXISTS.equals(code)) {
            layoutUsername.setError(detail);
        } else if (CODE_EMAIL_EXISTS.equals(code)) {
            layoutEmail.setError(detail);
        } else {
            showError(detail);
        }
    }

    // Replaces the form with the "awaiting activation" outcome. The NIC and
    // status shown are the API's values (status is always Pending, set by the
    // server). No token is issued: a Pending account cannot log in.
    private void onRegistered(String responseBody) {
        RegisterResult result;
        try {
            result = AuthParser.parseRegister(responseBody);
        } catch (JSONException e) {
            showError(getString(R.string.error_unexpected_response));
            return;
        }

        TextView textStatus = findViewById(R.id.textSuccessStatus);
        textStatus.setText(getString(R.string.register_success_status, result.nic, result.status));

        findViewById(R.id.panelForm).setVisibility(View.GONE);
        findViewById(R.id.panelSuccess).setVisibility(View.VISIBLE);
    }

    // Disables the button while the request is in flight so it is sent once.
    private void setBusy(boolean busy) {
        progress.setVisibility(busy ? View.VISIBLE : View.GONE);
        buttonRegister.setEnabled(!busy);
    }

    // Shows a message under the form: the API's detail, or transport wording.
    private void showError(String message) {
        textError.setText(message);
        textError.setVisibility(TextUtils.isEmpty(message) ? View.GONE : View.VISIBLE);
    }

    // Hides the message line before a new attempt.
    private void hideError() {
        textError.setVisibility(View.GONE);
    }

    // Trimmed text of an input, never null.
    private static String textOf(TextInputEditText input) {
        return rawTextOf(input).trim();
    }

    // Untrimmed text, for passwords: spaces can be part of a password.
    private static String rawTextOf(TextInputEditText input) {
        return input.getText() == null ? "" : input.getText().toString();
    }
}
