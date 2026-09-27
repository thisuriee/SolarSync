/*
 * File:    OperatorHomeActivity.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Home shell for a signed-in Grid Operator. Identity owns the header
 *          and logout; M2 (map) and M4 (QR scan) add their entry points to the
 *          homeContent area. Every operator action is still judged by the API.
 */
package com.sliit.smartsolar.activities;

import android.os.Bundle;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.sliit.smartsolar.R;
import com.sliit.smartsolar.database.SessionDao;
import com.sliit.smartsolar.utils.SessionManager;

public class OperatorHomeActivity extends AppCompatActivity {

    // Shows who is signed in (from the session row) and wires logout.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_operator_home);

        SessionDao session = new SessionDao(this);
        TextView textWelcome = findViewById(R.id.textWelcome);
        TextView textRole = findViewById(R.id.textRole);

        textWelcome.setText(getString(R.string.home_welcome, session.getFullName()));
        textRole.setText(R.string.home_operator_role);

        findViewById(R.id.buttonLogout).setOnClickListener(v -> SessionManager.logout(this));
    }
}
