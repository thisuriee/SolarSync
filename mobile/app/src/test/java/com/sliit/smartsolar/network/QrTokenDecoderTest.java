/*
 * File:    QrTokenDecoderTest.java
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: Proves the camera-to-token path without a device. QrTokenDecoder is
 *          pure Java (zxing core, no Android or camera types), so a JVM test can
 *          build the same luminance plane a camera frame yields and check the
 *          token comes back intact.
 *
 *          This is the only part of the scanner that can be verified off-device:
 *          the preview, the permission flow and the button wiring still need a
 *          real camera.
 */
package com.sliit.smartsolar.network;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;

import com.google.zxing.BarcodeFormat;
import com.google.zxing.EncodeHintType;
import com.google.zxing.common.BitMatrix;
import com.google.zxing.qrcode.QRCodeWriter;
import com.google.zxing.qrcode.decoder.ErrorCorrectionLevel;

import org.junit.Test;

import java.util.Arrays;
import java.util.Collections;
import java.util.Map;

public class QrTokenDecoderTest {

    /** The shape the API actually issues: 32 random bytes as Base64Url. */
    private static final String TOKEN = "L0Mp_1VSELhXUuvvpRJGoiI8qfnjmskHW-77M1YkP0E";

    // Encodes a token exactly as the prosumer's screen does — error correction M —
    // then decodes it back out of a grayscale plane of the same size.
    @Test
    public void decodesATokenFromALuminancePlane() throws Exception {
        byte[] yPlane = encodeToLuminancePlane(TOKEN, 512);

        String decoded = new QrTokenDecoder().decode(yPlane, 512, 512, 512);

        assertEquals(TOKEN, decoded);
    }

    // A row stride wider than the frame is what the camera actually reports, since
    // each row is padded. The decoder must honour the stride, not assume width.
    @Test
    public void decodesWhenRowsArePadded() throws Exception {
        byte[] packed = encodeToLuminancePlane(TOKEN, 512);
        int stride = 640;

        byte[] padded = new byte[stride * 512];
        Arrays.fill(padded, (byte) 0xFF);
        for (int y = 0; y < 512; y++) {
            System.arraycopy(packed, y * 512, padded, y * stride, 512);
        }

        String decoded = new QrTokenDecoder().decode(padded, stride, 512, 512);

        assertEquals(TOKEN, decoded);
    }

    // Most frames hold no QR while the operator lines the camera up. That is the
    // expected case, not an error, so it must come back as null rather than throw.
    @Test
    public void returnsNullForAFrameWithoutAQr() {
        byte[] blank = new byte[512 * 512];
        Arrays.fill(blank, (byte) 0xFF);

        assertNull(new QrTokenDecoder().decode(blank, 512, 512, 512));
    }

    // A frame that never arrived must not crash the analysis thread.
    @Test
    public void returnsNullForAMissingFrame() {
        assertNull(new QrTokenDecoder().decode(null, 0, 0, 0));
    }

    // Renders the token as black and white bytes, the way a luminance plane
    // arrives from the camera: one byte per pixel, 0x00 black and 0xFF white.
    private byte[] encodeToLuminancePlane(String token, int size) throws Exception {
        Map<EncodeHintType, Object> hints =
                Collections.singletonMap(EncodeHintType.ERROR_CORRECTION, ErrorCorrectionLevel.M);

        BitMatrix matrix = new QRCodeWriter().encode(token, BarcodeFormat.QR_CODE, size, size, hints);

        byte[] plane = new byte[matrix.getWidth() * matrix.getHeight()];
        for (int y = 0; y < matrix.getHeight(); y++) {
            for (int x = 0; x < matrix.getWidth(); x++) {
                plane[y * matrix.getWidth() + x] = (byte) (matrix.get(x, y) ? 0x00 : 0xFF);
            }
        }

        return plane;
    }
}
