/*
 * File:    MyProfileActivity.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: A prosumer views and edits their own profile (GET/PUT
 *          /prosumers/{nic}) and can request deactivation (PATCH
 *          /prosumers/{nic}/request-deactivation). The API enforces
 *          own-profile-only and the active-reservations block; this screen
 *          shows the API's answer and never decides a rule itself.
 */
package com.sliit.smartsolar.activities;

import android.net.Uri;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.View;
import android.widget.Button;
import android.widget.ProgressBar;
import android.widget.TextView;

import androidx.activity.OnBackPressedCallback;
import androidx.appcompat.app.AppCompatActivity;

import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.textfield.TextInputEditText;
import com.google.android.material.textfield.TextInputLayout;
import com.sliit.smartsolar.R;
import com.sliit.smartsolar.utils.BottomNav;
import com.sliit.smartsolar.database.SessionDao;
import com.sliit.smartsolar.models.UserProfile;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.network.DateUtils;
import com.sliit.smartsolar.parsers.AuthParser;
import com.sliit.smartsolar.utils.InputFormat;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONException;
import org.json.JSONObject;

public class MyProfileActivity extends AppCompatActivity {

    // docs/response-format.md: the only field-specific code this screen can get.
    private static final String CODE_EMAIL_EXISTS = "USER_EMAIL_EXISTS";

    private TextInputLayout layoutFullName, layoutEmail, layoutPhone, layoutAddress;
    private TextInputEditText inputFullName, inputEmail, inputPhone, inputAddress;
    private TextView textNic, textUsername, textStatus, textError, textInfo;
    private ProgressBar progress;
    private Button buttonEdit, buttonDeactivate;
    private View groupEditActions;

    /** Route for this prosumer's own profile, built from the stored NIC. */
    private String profilePath;

    /** Last profile the API returned; Cancel restores the fields from it. */
    private UserProfile current;

    // Binds the screen and loads the profile. The NIC in the route comes from
    // the session (i.e. from the login response, the same value as the token's
    // nic claim). The API still compares it with the token and answers 403
    // AUTH_FORBIDDEN_ROLE on a mismatch, so a tampered route gains nothing.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_my_profile);
        BottomNav.attach(this, R.id.nav_profile);

        layoutFullName = findViewById(R.id.layoutFullName);
        layoutEmail = findViewById(R.id.layoutEmail);
        layoutPhone = findViewById(R.id.layoutPhone);
        layoutAddress = findViewById(R.id.layoutAddress);
        inputFullName = findViewById(R.id.inputFullName);
        inputEmail = findViewById(R.id.inputEmail);
        inputPhone = findViewById(R.id.inputPhone);
        inputAddress = findViewById(R.id.inputAddress);
        textNic = findViewById(R.id.textNic);
        textUsername = findViewById(R.id.textUsername);
        textStatus = findViewById(R.id.textStatus);
        textError = findViewById(R.id.textError);
        textInfo = findViewById(R.id.textInfo);
        progress = findViewById(R.id.progress);
        buttonEdit = findViewById(R.id.buttonEdit);
        buttonDeactivate = findViewById(R.id.buttonDeactivate);
        groupEditActions = findViewById(R.id.groupEditActions);

        buttonEdit.setOnClickListener(v -> setEditing(true));
        findViewById(R.id.buttonCancel).setOnClickListener(v -> cancelEdit());
        findViewById(R.id.buttonSave).setOnClickListener(v -> saveProfile());
        buttonDeactivate.setOnClickListener(v -> confirmDeactivation());
        findViewById(R.id.buttonDone).setOnClickListener(v -> SessionManager.goToLogin(this));

        profilePath = "/prosumers/" + Uri.encode(new SessionDao(this).getNic());
        loadProfile();
    }

    // GET /prosumers/{nic}. Always read from the API, never from a cache, so
    // the screen shows the server's current values.
    private void loadProfile() {
        setBusy(true);

        ApiClient.get(profilePath, new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                setBusy(false);
                if (showParsedProfile(body)) {
                    buttonEdit.setEnabled(true);
                    buttonDeactivate.setEnabled(true);
                }
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false);
                if (!SessionManager.handleAuthError(MyProfileActivity.this, code, detail)) {
                    showError(detail);
                }
            }
        });
    }

    // Rule: a prosumer edits only their own profile, and only these four
    // fields. nic, role and status are not in the body at all — the API's
    // UpdateProsumerRequest has no such properties, so they cannot be applied.
    private void saveProfile() {
        hideMessages();
        if (!validateForm()) {
            return;
        }

        String body;
        try {
            body = new JSONObject()
                    .put("fullName", textOf(inputFullName))
                    .put("email", textOf(inputEmail))
                    .put("phone", textOf(inputPhone))
                    .put("address", textOf(inputAddress))
                    .toString();
        } catch (JSONException e) {
            return; // Unreachable: put() only throws for non-finite numbers.
        }

        setBusy(true);

        ApiClient.put(profilePath, body, new ApiCallback<String>() {
            @Override
            public void onSuccess(String responseBody) {
                setBusy(false);
                if (showParsedProfile(responseBody)) {
                    setEditing(false);
                    // The header name is refreshed from the API's saved value.
                    SessionManager.updateFullName(MyProfileActivity.this, current.fullName);
                    showInfo(getString(R.string.profile_saved));
                }
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false);
                if (SessionManager.handleAuthError(MyProfileActivity.this, code, detail)) {
                    return;
                }
                if (CODE_EMAIL_EXISTS.equals(code)) {
                    layoutEmail.setError(detail);
                } else {
                    showError(detail);
                }
            }
        });
    }

    // Field-shape checks mirroring UpdateProsumerRequest.cs (same annotations
    // as registration), so a typo never produces the API's code-less 400.
    private boolean validateForm() {
        boolean ok = check(layoutFullName, InputFormat.fullName(textOf(inputFullName)));
        ok &= check(layoutEmail, InputFormat.email(textOf(inputEmail)));
        ok &= check(layoutPhone, InputFormat.phone(textOf(inputPhone)));
        ok &= check(layoutAddress, InputFormat.address(textOf(inputAddress)));
        return ok;
    }

    // Asks for confirmation first: deactivation signs the prosumer out and
    // only a Backoffice officer can undo it.
    private void confirmDeactivation() {
        new MaterialAlertDialogBuilder(this)
                .setTitle(R.string.deactivate_confirm_title)
                .setMessage(R.string.deactivate_confirm_body)
                .setNegativeButton(R.string.action_cancel, null)
                .setPositiveButton(R.string.action_deactivate, (dialog, which) -> requestDeactivation())
                .show();
    }

    // Rule 6: PATCH /prosumers/{nic}/request-deactivation. The API checks that
    // it is the caller's own account and refuses with
    // USER_HAS_ACTIVE_RESERVATIONS while reservations are active; the app only
    // shows that detail. Sent as POST + X-HTTP-Method-Override by ApiClient.
    private void requestDeactivation() {
        hideMessages();
        setBusy(true);

        // "{}" rather than no body: IIS answers 411 Length Required to a POST
        // without a Content-Length, and the route takes no fields.
        ApiClient.patch(profilePath + "/request-deactivation", "{}", new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                setBusy(false);
                onDeactivated(body);
            }

            @Override
            public void onError(String code, String detail) {
                setBusy(false);
                if (!SessionManager.handleAuthError(MyProfileActivity.this, code, detail)) {
                    showError(detail);
                }
            }
        });
    }

    // Outcome: the account is Deactivated, so the session is wiped at once
    // (every table, as on logout) and the outcome panel replaces the profile.
    // Status and time are the API's values, shown in local time.
    private void onDeactivated(String body) {
        UserProfile profile;
        try {
            profile = AuthParser.parseProfile(body);
        } catch (JSONException e) {
            showError(getString(R.string.error_unexpected_response));
            return;
        }

        SessionManager.clear(this);

        TextView textOutcome = findViewById(R.id.textDeactivatedStatus);
        textOutcome.setText(getString(R.string.deactivated_status,
                profile.status, DateUtils.toLocalDisplay(profile.deactivationRequestedAt)));

        findViewById(R.id.panelProfile).setVisibility(View.GONE);
        findViewById(R.id.panelDeactivated).setVisibility(View.VISIBLE);

        // With the session gone there is no home to go back to: Back = login.
        getOnBackPressedDispatcher().addCallback(this, new OnBackPressedCallback(true) {
            @Override
            public void handleOnBackPressed() {
                SessionManager.goToLogin(MyProfileActivity.this);
            }
        });
    }

    // Parses a profile body and shows it. Returns false (with a message) when
    // the body cannot be read.
    private boolean showParsedProfile(String body) {
        try {
            current = AuthParser.parseProfile(body);
        } catch (JSONException e) {
            showError(getString(R.string.error_unexpected_response));
            return false;
        }
        showProfile(current);
        return true;
    }

    // Fills the screen from the API's values. NIC, username and status are
    // display-only text, never inputs.
    private void showProfile(UserProfile profile) {
        textNic.setText(getString(R.string.profile_nic, profile.nic));
        textUsername.setText(getString(R.string.profile_username, profile.username));
        textStatus.setText(getString(R.string.profile_status, profile.status));

        inputFullName.setText(profile.fullName);
        inputEmail.setText(profile.email);
        inputPhone.setText(profile.phone);
        inputAddress.setText(profile.address);
    }

    // Discards unsaved edits by reloading the fields from the last API values.
    private void cancelEdit() {
        if (current != null) {
            showProfile(current);
        }
        setEditing(false);
    }

    // Switches between view mode and edit mode.
    private void setEditing(boolean editing) {
        hideMessages();
        for (TextInputLayout layout : new TextInputLayout[]{layoutFullName, layoutEmail, layoutPhone, layoutAddress}) {
            layout.setError(null);
        }
        for (TextInputEditText input : new TextInputEditText[]{inputFullName, inputEmail, inputPhone, inputAddress}) {
            input.setEnabled(editing);
        }
        groupEditActions.setVisibility(editing ? View.VISIBLE : View.GONE);
        buttonEdit.setVisibility(editing ? View.GONE : View.VISIBLE);
        buttonDeactivate.setVisibility(editing ? View.GONE : View.VISIBLE);
    }

    // Shows or clears one field's error. Returns true when the field is fine.
    private boolean check(TextInputLayout layout, int errorRes) {
        layout.setError(errorRes == InputFormat.OK ? null : getString(errorRes));
        return errorRes == InputFormat.OK;
    }

    // Shows the spinner and disables Save while a request is in flight, so an
    // edit is never sent twice.
    private void setBusy(boolean busy) {
        progress.setVisibility(busy ? View.VISIBLE : View.GONE);
        findViewById(R.id.buttonSave).setEnabled(!busy);
    }

    // Shows the API's detail (or transport wording) under the form.
    private void showError(String message) {
        textInfo.setVisibility(View.GONE);
        textError.setText(message);
        textError.setVisibility(TextUtils.isEmpty(message) ? View.GONE : View.VISIBLE);
    }

    // Shows a confirmation that an API call succeeded.
    private void showInfo(String message) {
        textError.setVisibility(View.GONE);
        textInfo.setText(message);
        textInfo.setVisibility(View.VISIBLE);
    }

    // Hides both message lines before a new action.
    private void hideMessages() {
        textError.setVisibility(View.GONE);
        textInfo.setVisibility(View.GONE);
    }

    // Trimmed text of an input, never null.
    private static String textOf(TextInputEditText input) {
        return input.getText() == null ? "" : input.getText().toString().trim();
    }
}
