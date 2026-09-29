/*
 * File:    QrDisplayActivity.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Lists the prosumer's approved bookings and, on selection, shows the
 *          transaction QR the Grid Operator will scan.
 *
 *          The screen holds no rules. It asks the API for the token, renders
 *          whatever the API returned, and displays the API's own error message
 *          when it refuses. Nothing here decides whether a booking is valid.
 */
package com.sliit.smartsolar.activities;

import android.content.ContentValues;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.ImageView;
import android.widget.ListView;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.google.zxing.BarcodeFormat;
import com.google.zxing.EncodeHintType;
import com.google.zxing.WriterException;
import com.google.zxing.common.BitMatrix;
import com.google.zxing.qrcode.QRCodeWriter;
import com.google.zxing.qrcode.decoder.ErrorCorrectionLevel;
import com.sliit.smartsolar.R;
import com.sliit.smartsolar.database.BookingDao;
import com.sliit.smartsolar.database.DbContract;
import com.sliit.smartsolar.database.NodeDao;
import com.sliit.smartsolar.models.ApprovedBooking;
import com.sliit.smartsolar.network.ApiCallback;
import com.sliit.smartsolar.network.ApiClient;
import com.sliit.smartsolar.network.DateUtils;
import com.sliit.smartsolar.parsers.QrParser;

import java.util.ArrayList;
import java.util.EnumMap;
import java.util.List;
import java.util.Map;

public class QrDisplayActivity extends AppCompatActivity {

    // The API filters by the caller's NIC, so this route takes no nic parameter.
    private static final String APPROVED_BOOKINGS_PATH = "/reservations/mine?status=Approved";
    private static final String APPROVED = "Approved";

    /**
     * Rendered bitmap size. The contract asks for at least 512 square so the code
     * survives a phone-to-phone scan without interpolation artefacts.
     */
    private static final int QR_SIZE_PX = 512;

    private final List<ApprovedBooking> bookings = new ArrayList<>();

    private ArrayAdapter<String> adapter;
    private BookingDao bookingDao;
    private NodeDao nodeDao;

    private View listPanel;
    private View qrPanel;
    private TextView listStatus;
    private TextView qrStatus;
    private ImageView qrImage;

    // Sets up the list, the QR panel and the read-through load.
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_qr_display);

        bookingDao = new BookingDao(this);
        nodeDao = new NodeDao(this);

        listPanel = findViewById(R.id.listPanel);
        qrPanel = findViewById(R.id.qrPanel);
        listStatus = findViewById(R.id.listStatus);
        qrStatus = findViewById(R.id.qrStatus);
        qrImage = findViewById(R.id.qrImage);

        ListView listView = findViewById(R.id.bookingList);
        adapter = new ArrayAdapter<>(this, android.R.layout.simple_list_item_1, new ArrayList<>());
        listView.setAdapter(adapter);
        listView.setOnItemClickListener((parent, view, position, id) -> showQr(bookings.get(position)));

        Button back = findViewById(R.id.qrBack);
        back.setOnClickListener(v -> showList());

        loadBookings();
    }

    // Asks the API for the approved bookings and overwrites the cache on success.
    // The cache renders only when the call fails, and is labelled with the API's
    // message rather than a locally invented one.
    private void loadBookings() {
        listStatus.setText(R.string.qr_loading);

        ApiClient.get(APPROVED_BOOKINGS_PATH, new ApiCallback<String>() {

            @Override
            public void onSuccess(String body) {
                List<ApprovedBooking> fetched = QrParser.parseBookings(body);
                bookingDao.replaceAll(QrParser.toCacheRows(fetched));
                showBookings(fetched, null);
            }

            @Override
            public void onError(String code, String detail) {
                List<ContentValues> cached = bookingDao.findByStatus(APPROVED);
                showBookings(QrParser.fromCacheRows(cached), detail);
            }
        });
    }

    // Renders the list and a status line. A non-null failureDetail means the
    // rows came from the cache, so the API's message is shown above them.
    private void showBookings(List<ApprovedBooking> items, String failureDetail) {
        bookings.clear();
        bookings.addAll(items);

        adapter.clear();
        for (ApprovedBooking booking : bookings) {
            adapter.add(labelFor(booking));
        }
        adapter.notifyDataSetChanged();

        if (failureDetail != null) {
            listStatus.setText(failureDetail);
        } else if (bookings.isEmpty()) {
            listStatus.setText(getString(R.string.qr_empty) + " " + getString(R.string.qr_create_hint));
        } else {
            listStatus.setText("");
        }
    }

    // Builds one list row: station, then the slot window in local time.
    private String labelFor(ApprovedBooking booking) {
        String station = resolveStationName(booking);
        String window = getString(R.string.qr_slot,
                DateUtils.toLocalDisplay(booking.slotStartIso),
                DateUtils.toLocalDisplay(booking.slotEndIso));

        return station + " — " + window;
    }

    // Prefers the cached station name and falls back to the station id, so a row
    // is still identifiable when the node cache has not been populated yet.
    private String resolveStationName(ApprovedBooking booking) {
        ContentValues station = nodeDao.findById(booking.stationId);
        if (station == null) {
            return booking.stationId;
        }

        String name = station.getAsString(DbContract.CachedNodes.STATION_NAME);
        return name == null || name.isEmpty() ? booking.stationId : name;
    }

    // Requests the QR token for one booking and renders it.
    private void showQr(ApprovedBooking booking) {
        listPanel.setVisibility(View.GONE);
        qrPanel.setVisibility(View.VISIBLE);
        qrImage.setImageDrawable(null);
        qrStatus.setText(R.string.qr_preparing);

        ApiClient.get("/reservations/" + booking.reservationId + "/qr", new ApiCallback<String>() {

            @Override
            public void onSuccess(String body) {
                QrParser.Token token = QrParser.parseToken(body);
                if (token == null) {
                    qrStatus.setText(R.string.qr_unreadable);
                    return;
                }

                qrImage.setImageBitmap(renderQr(token.token));
                qrStatus.setText(getString(R.string.qr_expires,
                        DateUtils.toLocalDisplay(token.expiresAtIso)));
            }

            @Override
            public void onError(String code, String detail) {
                // The API refused — not Approved, not the owner, expired session.
                // Its detail is the message; this screen authors none.
                qrStatus.setText(detail);
            }
        });
    }

    // Returns to the list and clears the previous QR, so a stale token is never
    // left on screen behind the list.
    private void showList() {
        qrPanel.setVisibility(View.GONE);
        listPanel.setVisibility(View.VISIBLE);
        qrImage.setImageDrawable(null);
        qrStatus.setText(R.string.qr_preparing);
    }

    // Encodes the token as a QR bitmap at error correction level M. The encode is
    // bounded work (one matrix, a few milliseconds), so it runs inline rather than
    // adding another executor.
    private Bitmap renderQr(String content) {
        Map<EncodeHintType, Object> hints = new EnumMap<>(EncodeHintType.class);
        hints.put(EncodeHintType.ERROR_CORRECTION, ErrorCorrectionLevel.M);
        hints.put(EncodeHintType.CHARACTER_SET, "UTF-8");
        hints.put(EncodeHintType.MARGIN, 1);

        try {
            BitMatrix matrix = new QRCodeWriter()
                    .encode(content, BarcodeFormat.QR_CODE, QR_SIZE_PX, QR_SIZE_PX, hints);

            int[] pixels = new int[QR_SIZE_PX * QR_SIZE_PX];
            for (int y = 0; y < QR_SIZE_PX; y++) {
                int rowOffset = y * QR_SIZE_PX;
                for (int x = 0; x < QR_SIZE_PX; x++) {
                    pixels[rowOffset + x] = matrix.get(x, y) ? Color.BLACK : Color.WHITE;
                }
            }

            Bitmap bitmap = Bitmap.createBitmap(QR_SIZE_PX, QR_SIZE_PX, Bitmap.Config.ARGB_8888);
            bitmap.setPixels(pixels, 0, QR_SIZE_PX, 0, 0, QR_SIZE_PX, QR_SIZE_PX);
            return bitmap;
        } catch (WriterException e) {
            // Not reachable for a 43-character token; a blank image beats a crash.
            return null;
        }
    }
}
