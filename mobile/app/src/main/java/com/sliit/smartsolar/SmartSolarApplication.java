/*
 * File:    SmartSolarApplication.java
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Initialises ApiClient once per process, before any Activity runs.
 *          Needed because Android can restore a process straight into a home
 *          screen after it was killed, skipping the launcher Activity entirely.
 */
package com.sliit.smartsolar;

import android.app.Application;

import com.sliit.smartsolar.network.ApiClient;

public class SmartSolarApplication extends Application {

    // Runs before any Activity of this process, so every screen can call the
    // API without first checking that ApiClient.init() happened.
    @Override
    public void onCreate() {
        super.onCreate();
        ApiClient.init(this);
    }
}
