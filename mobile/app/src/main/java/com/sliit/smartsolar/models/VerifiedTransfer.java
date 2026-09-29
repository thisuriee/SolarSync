/*
 * File:    VerifiedTransfer.java
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: What POST /api/fulfilment/verify-qr returns, as the operator's
 *          confirmation screen needs it. Holds data only — the API decided
 *          everything before this object existed.
 */
package com.sliit.smartsolar.models;

public class VerifiedTransfer {

    /** The booking being fulfilled. Reuses M3's Reservation model. */
    public final Reservation reservation;

    /** Safe projection of the prosumer; the API never sends the user document. */
    public final String prosumerNic;
    public final String prosumerFullName;
    public final String prosumerPhone;

    public final String stationName;
    public final String stationCity;

    /**
     * Single-use proof that the scan happened. Required by
     * PATCH /api/reservations/{id}/complete, so finalising cannot be reached
     * without a verified QR.
     */
    public final String verificationId;

    public VerifiedTransfer(Reservation reservation,
                            String prosumerNic, String prosumerFullName, String prosumerPhone,
                            String stationName, String stationCity,
                            String verificationId) {
        this.reservation = reservation;
        this.prosumerNic = prosumerNic;
        this.prosumerFullName = prosumerFullName;
        this.prosumerPhone = prosumerPhone;
        this.stationName = stationName;
        this.stationCity = stationCity;
        this.verificationId = verificationId;
    }
}
