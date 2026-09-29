/*
 * File:    QrTokenDecoder.java
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: Decodes a QR code out of one camera frame's luminance plane.
 *
 *          Deliberately free of Android and camera types, so the YUV-to-token
 *          path can be unit-tested on the JVM instead of only on a device. The
 *          activity owns the camera; this owns the decoding.
 */
package com.sliit.smartsolar.network;

import com.google.zxing.BarcodeFormat;
import com.google.zxing.BinaryBitmap;
import com.google.zxing.DecodeHintType;
import com.google.zxing.LuminanceSource;
import com.google.zxing.MultiFormatReader;
import com.google.zxing.NotFoundException;
import com.google.zxing.PlanarYUVLuminanceSource;
import com.google.zxing.Result;
import com.google.zxing.common.HybridBinarizer;

import java.util.Collections;
import java.util.EnumMap;
import java.util.Map;

public final class QrTokenDecoder {

    private final MultiFormatReader reader = new MultiFormatReader();

    // QR only: the operator scans a transaction token, and restricting the format
    // removes both false positives and the cost of probing every symbology.
    public QrTokenDecoder() {
        Map<DecodeHintType, Object> hints = new EnumMap<>(DecodeHintType.class);
        hints.put(DecodeHintType.POSSIBLE_FORMATS, Collections.singletonList(BarcodeFormat.QR_CODE));
        hints.put(DecodeHintType.TRY_HARDER, Boolean.TRUE);
        reader.setHints(hints);
    }

    /**
     * Decodes the Y (luminance) plane of a YUV_420_888 frame.
     *
     * @param yPlane    plane 0 bytes, whose length may exceed width × height
     *                  because the camera pads each row.
     * @param rowStride bytes per row as reported by the plane, not the frame width.
     * @return the token, or null when this frame holds no readable QR.
     */
    public String decode(byte[] yPlane, int rowStride, int width, int height) {
        if (yPlane == null || rowStride <= 0 || width <= 0 || height <= 0) {
            return null;
        }

        try {
            LuminanceSource source = new PlanarYUVLuminanceSource(
                    yPlane, rowStride, height, 0, 0, width, height, false);

            BinaryBitmap bitmap = new BinaryBitmap(new HybridBinarizer(source));
            Result result = reader.decodeWithState(bitmap);

            return result == null ? null : result.getText();
        } catch (NotFoundException notReadable) {
            // Not a failure. Most frames have no QR in them, which is exactly what
            // happens while the operator is still lining the camera up.
            // decodeWithState reports every unreadable frame this way: a bad
            // checksum or an unparseable format is folded into it upstream.
            return null;
        } finally {
            reader.reset();
        }
    }
}
