/*
 * File:    OperatorHomeActivity.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Home shell for a signed-in Grid Operator. Identity owns the header
 *          and logout; M2 (map) and M4 (QR scan) add their entry points to the
 *          homeContent area. Every operator action is still judged by the API.
 */
package com.sliit.smartsolar.activities;

import android.content.Intent;
import android.os.Bundle;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.sliit.smartsolar.R;
import com.sliit.smartsolar.database.SessionDao;
import com.sliit.smartsolar.utils.BottomNav;
import com.sliit.smartsolar.utils.SessionManager;

public class OperatorHomeActivity extends AppCompatActivity {

    // Shows who is signed in (from the session row), wires the scan card and
    // logout, and adds the bottom bar. The map and node list are the bar's tabs.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_operator_home);
        BottomNav.attach(this, R.id.nav_home);

        SessionDao session = new SessionDao(this);
        TextView textWelcome = findViewById(R.id.textWelcome);
        TextView textRole = findViewById(R.id.textRole);

        textWelcome.setText(getString(R.string.home_welcome, session.getFullName()));
        textRole.setText(R.string.home_operator_role);

        findViewById(R.id.buttonScanQr).setOnClickListener(v ->
                startActivity(new Intent(this, QrScannerActivity.class)));
        findViewById(R.id.buttonLogout).setOnClickListener(v -> SessionManager.logout(this));
    }
}
