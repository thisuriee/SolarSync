/*
 * File:    QrScannerActivity.java
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: The operator's scanner. Shows a live preview, decodes the prosumer's
 *          QR on the frame that contains one, and hands the token to the
 *          verification screen.
 *
 *          This screen decides nothing. It does not know whether a token is
 *          valid, approved or expired — it extracts a string and passes it on,
 *          and the API answers.
 */
package com.sliit.smartsolar.activities;

import android.Manifest;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Bundle;
import android.provider.Settings;
import android.view.View;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import androidx.camera.core.CameraSelector;
import androidx.camera.core.ImageAnalysis;
import androidx.camera.core.ImageProxy;
import androidx.camera.core.Preview;
import androidx.camera.lifecycle.ProcessCameraProvider;
import androidx.camera.view.PreviewView;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;

import com.google.common.util.concurrent.ListenableFuture;
import com.sliit.smartsolar.R;
import com.sliit.smartsolar.network.QrTokenDecoder;

import java.nio.ByteBuffer;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;

public class QrScannerActivity extends AppCompatActivity {

    private static final int REQUEST_CAMERA = 1001;

    private PreviewView previewView;
    private View unavailablePanel;
    private TextView unavailableText;

    private ProcessCameraProvider cameraProvider;
    private ImageAnalysis analysis;
    private ExecutorService analysisExecutor;

    /**
     * Guards against a burst of frames each launching the next screen. Set on the
     * first decode and cleared in onResume, so returning to scan again works.
     */
    private final AtomicBoolean handled = new AtomicBoolean(false);

    private final QrTokenDecoder decoder = new QrTokenDecoder();

    // Sets up the preview and kicks off the permission check.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_qr_scanner);

        previewView = findViewById(R.id.previewView);
        unavailablePanel = findViewById(R.id.unavailablePanel);
        unavailableText = findViewById(R.id.textUnavailable);

        findViewById(R.id.buttonOpenSettings).setOnClickListener(v -> openAppSettings());

        // One frame at a time: decoding is cheap but the camera outruns it.
        analysisExecutor = Executors.newSingleThreadExecutor();

        start();
    }

    // The operator may back out of the verification screen to scan again, and the
    // analyzer was cleared when the token was decoded, so it is armed again here.
    @Override
    protected void onResume() {
        super.onResume();
        handled.set(false);

        if (analysis != null) {
            analysis.setAnalyzer(analysisExecutor, this::analyze);
        }
    }

    // Releases the executor and the camera when the screen goes away.
    @Override
    protected void onDestroy() {
        super.onDestroy();

        if (analysis != null) {
            analysis.clearAnalyzer();
        }
        if (cameraProvider != null) {
            cameraProvider.unbindAll();
        }
        analysisExecutor.shutdown();
    }

    // Entry point for the whole flow, and the retry target. Asks for the camera
    // permission if it is missing; otherwise goes straight to the preview.
    private void start() {
        hideUnavailable();

        if (!hasCameraPermission()) {
            ActivityCompat.requestPermissions(this, new String[]{Manifest.permission.CAMERA}, REQUEST_CAMERA);
            return;
        }

        startCamera();
    }

    // Rule: a denial is an outcome, not a failure. The screen explains what is
    // missing and offers the one route that can fix it; it never crashes and
    // never pretends to scan.
    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions,
                                           @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode != REQUEST_CAMERA) {
            return;
        }

        if (hasCameraPermission()) {
            startCamera();
            return;
        }

        // "Don't ask again" leaves shouldShowRequestPermissionRationale false, and
        // only then is the settings route the one that can help.
        boolean canAskAgain = ActivityCompat.shouldShowRequestPermissionRationale(
                this, Manifest.permission.CAMERA);

        showUnavailable(getString(R.string.scan_permission_denied), !canAskAgain);
    }

    // True when the camera permission is already granted.
    private boolean hasCameraPermission() {
        return ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA)
                == PackageManager.PERMISSION_GRANTED;
    }

    // Binds the back camera to the preview and to a YUV frame analyser.
    private void startCamera() {
        ListenableFuture<ProcessCameraProvider> providerFuture =
                ProcessCameraProvider.getInstance(this);

        providerFuture.addListener(() -> {
            try {
                cameraProvider = providerFuture.get();
            } catch (Exception unavailable) {
                showUnavailable(getString(R.string.scan_permission_denied), true);
                return;
            }

            Preview preview = new Preview.Builder().build();
            preview.setSurfaceProvider(previewView.getSurfaceProvider());

            // KEEP_ONLY_LATEST: a slow decoder must never build a backlog of stale
            // frames. The operator would be scanning an image from seconds ago.
            analysis = new ImageAnalysis.Builder()
                    .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                    .build();
            analysis.setAnalyzer(analysisExecutor, this::analyze);

            try {
                cameraProvider.unbindAll();
                cameraProvider.bindToLifecycle(
                        this, CameraSelector.DEFAULT_BACK_CAMERA, preview, analysis);
            } catch (Exception cannotBind) {
                showUnavailable(getString(R.string.scan_permission_denied), true);
            }
        }, ContextCompat.getMainExecutor(this));
    }

    // Runs per camera frame on the analysis thread. Most frames hold no QR, which
    // is the expected case rather than an error.
    private void analyze(ImageProxy image) {
        try {
            if (handled.get()) {
                return;
            }

            // Plane 0 is the luminance plane; a QR is black and white, so colour
            // would only cost time. rowStride may exceed the frame width because
            // the camera pads each row, so it is passed on rather than assumed.
            ImageProxy.PlaneProxy plane = image.getPlanes()[0];
            ByteBuffer buffer = plane.getBuffer();
            byte[] yPlane = new byte[buffer.remaining()];
            buffer.get(yPlane);

            String token = decoder.decode(
                    yPlane, plane.getRowStride(), image.getWidth(), image.getHeight());

            if (token != null && handled.compareAndSet(false, true)) {
                runOnUiThread(() -> onQrDecoded(token));
            }
        } finally {
            // Always. A frame that is never closed stalls the whole pipeline.
            image.close();
        }
    }

    // Stops decoding and passes the scanned token to the verification screen. The
    // preview stays bound on purpose: CameraX stops the camera while this activity
    // is covered and restarts it on return, so scanning again needs no rebind.
    private void onQrDecoded(String token) {
        if (analysis != null) {
            analysis.clearAnalyzer();
        }

        Intent intent = new Intent(this, VerificationResultActivity.class);
        intent.putExtra(VerificationResultActivity.EXTRA_TOKEN, token);
        startActivity(intent);
    }

    // Hides the preview and explains why the scanner cannot run.
    private void showUnavailable(String message, boolean offerSettings) {
        unavailableText.setText(message);
        unavailablePanel.setVisibility(View.VISIBLE);
        previewView.setVisibility(View.GONE);
        findViewById(R.id.buttonOpenSettings)
                .setVisibility(offerSettings ? View.VISIBLE : View.GONE);
    }

    // Restores the preview after a retry.
    private void hideUnavailable() {
        unavailablePanel.setVisibility(View.GONE);
        previewView.setVisibility(View.VISIBLE);
    }

    // Opens this app's detail page, the only place a "don't ask again" denial can
    // be undone.
    private void openAppSettings() {
        Intent intent = new Intent(
                Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                Uri.fromParts("package", getPackageName(), null));
        startActivity(intent);
    }
}
