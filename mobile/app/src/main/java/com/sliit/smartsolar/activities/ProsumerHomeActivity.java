/*
 * File:    ProsumerHomeActivity.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Home shell for a signed-in Prosumer. Identity owns the header and
 *          logout; M3 and M4 add their entry points to the homeContent area.
 *          Opening this screen grants nothing: every call is judged by the API.
 */
package com.sliit.smartsolar.activities;

import android.os.Bundle;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.sliit.smartsolar.R;
import com.sliit.smartsolar.database.SessionDao;
import com.sliit.smartsolar.utils.SessionManager;

public class ProsumerHomeActivity extends AppCompatActivity {

    // Shows who is signed in (from the session row) and wires logout.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_prosumer_home);

        SessionDao session = new SessionDao(this);
        TextView textWelcome = findViewById(R.id.textWelcome);
        TextView textRole = findViewById(R.id.textRole);

        textWelcome.setText(getString(R.string.home_welcome, session.getFullName()));
        textRole.setText(getString(R.string.home_prosumer_role, session.getNic()));

        findViewById(R.id.buttonLogout).setOnClickListener(v -> SessionManager.logout(this));
    }
}
