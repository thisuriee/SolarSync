/*
 * File:    LoginActivity.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Launcher screen. Restores a saved session (asking the API whether it
 *          is still valid) or signs the user in, then routes to the home screen
 *          for the role the API returned. Pending/Deactivated accounts are
 *          rejected by the API; this screen only shows the API's detail.
 */
package com.sliit.smartsolar.activities;

import android.content.Intent;
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
import com.sliit.smartsolar.database.DatabaseHelper;
import com.sliit.smartsolar.models.LoginResult;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.network.ErrorParser;
import com.sliit.smartsolar.parsers.AuthParser;
import com.sliit.smartsolar.utils.SessionManager;

import org.json.JSONException;
import org.json.JSONObject;

import java.util.Locale;

public class LoginActivity extends AppCompatActivity {

    private TextInputLayout layoutUsername;
    private TextInputLayout layoutPassword;
    private TextInputEditText inputUsername;
    private TextInputEditText inputPassword;
    private TextView textError;
    private ProgressBar progress;
    private Button buttonLogin;

    // Binds the form (and the link to prosumer registration), shows any
    // message handed over by a logout/401, and restores a saved session
    // instead of asking for credentials again.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_login);

        layoutUsername = findViewById(R.id.layoutUsername);
        layoutPassword = findViewById(R.id.layoutPassword);
        inputUsername = findViewById(R.id.inputUsername);
        inputPassword = findViewById(R.id.inputPassword);
        textError = findViewById(R.id.textError);
        progress = findViewById(R.id.progress);
        buttonLogin = findViewById(R.id.buttonLogin);

        buttonLogin.setOnClickListener(v -> attemptLogin());
        findViewById(R.id.buttonRegister).setOnClickListener(v ->
                startActivity(new Intent(this, RegisterProsumerActivity.class)));

        String message = getIntent().getStringExtra(SessionManager.EXTRA_LOGIN_MESSAGE);
        if (message != null) {
            showError(message);
        }

        if (SessionManager.hasSession(this)) {
            restoreSession();
        }
    }

    // Session survives an app restart. The stored token is sent to GET /auth/me
    // and the API decides whether it is still good (expired → 401, account
    // since deactivated → 403). Offline, the cached role is used to open the
    // home screen; every later call is still judged by the API.
    private void restoreSession() {
        setBusy(true);

        ApiClient.get("/auth/me", new ApiCallback<String>() {
            @Override
            public void onSuccess(String body) {
                openHomeOrReject();
            }

            @Override
            public void onError(String code, String detail) {
                if (ErrorParser.CODE_NO_RESPONSE.equals(code)) {
                    openHomeOrReject();
                    return;
                }
                // Token expired/invalid, or the account is no longer Active.
                DatabaseHelper.getInstance(LoginActivity.this).clearAll();
                setBusy(false);
                showError(detail);
            }
        });
    }

    // Required-field check only, so an empty form never reaches the API. Whether
    // the credentials and account status are acceptable is decided server-side.
    private void attemptLogin() {
        String username = textOf(inputUsername);
        // Not trimmed: spaces can be part of a password.
        String password = inputPassword.getText() == null ? "" : inputPassword.getText().toString();

        layoutUsername.setError(username.isEmpty() ? getString(R.string.error_required) : null);
        layoutPassword.setError(password.isEmpty() ? getString(R.string.error_required) : null);
        if (username.isEmpty() || password.isEmpty()) {
            return;
        }

        String body;
        try {
            body = new JSONObject()
                    .put("username", username)
                    .put("password", password)
                    .toString();
        } catch (JSONException e) {
            return; // Unreachable: put() only throws for non-finite numbers.
        }

        setBusy(true);
        hideError();

        ApiClient.post("/auth/login", body, new ApiCallback<String>() {
            @Override
            public void onSuccess(String responseBody) {
                onLoggedIn(responseBody, username);
            }

            @Override
            public void onError(String code, String detail) {
                // AUTH_INVALID_CREDENTIALS, AUTH_ACCOUNT_PENDING,
                // AUTH_ACCOUNT_DEACTIVATED or no response: shown as the API says.
                setBusy(false);
                inputPassword.setText(null);
                showError(detail);
            }
        });
    }

    // Rule: login lands the user on the home for their role. The role comes
    // from the API's response (the same value as the token's role claim), never
    // from anything the user chose. Backoffice has no mobile home, so its token
    // is not stored at all.
    private void onLoggedIn(String responseBody, String username) {
        LoginResult login;
        try {
            login = AuthParser.parseLogin(responseBody);
        } catch (JSONException e) {
            setBusy(false);
            showError(getString(R.string.error_unexpected_response));
            return;
        }

        if (SessionManager.homeFor(login.role) == null) {
            setBusy(false);
            showError(getString(R.string.login_use_web_portal));
            return;
        }

        SessionManager.save(this, login, username.toLowerCase(Locale.ROOT));
        SessionManager.openHome(this);
    }

    // Opens the home for the stored role. A stored role without a mobile home
    // should not exist, but if it does the session is discarded.
    private void openHomeOrReject() {
        if (!SessionManager.openHome(this)) {
            DatabaseHelper.getInstance(this).clearAll();
            setBusy(false);
        }
    }

    // Disables the form while a request is in flight so it cannot be sent twice.
    private void setBusy(boolean busy) {
        progress.setVisibility(busy ? View.VISIBLE : View.GONE);
        buttonLogin.setEnabled(!busy);
        inputUsername.setEnabled(!busy);
        inputPassword.setEnabled(!busy);
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

    // Trimmed text of an input, never null. Used for the username only.
    private static String textOf(TextInputEditText input) {
        return input.getText() == null ? "" : input.getText().toString().trim();
    }
}
