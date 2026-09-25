/*
 * File:    ApprovedBooking.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: One approved reservation as the QR screen needs it. A view of the
 *          API's response, not a rule holder — nothing here decides anything.
 */
package com.sliit.smartsolar.models;

public class ApprovedBooking {

    /** Reservation id, used to request its QR token. */
    public final String reservationId;

    /** Station the transfer happens at. */
    public final String stationId;

    /** Station display name, resolved from the node cache when available. */
    public final String stationName;

    /** Slot window in the API's UTC wire format; formatted for display on screen. */
    public final String slotStartIso;
    public final String slotEndIso;

    /** Always "Approved" for these — the API is what selected them. */
    public final String status;

    public final double energyKWh;

    public ApprovedBooking(String reservationId, String stationId, String stationName,
                           String slotStartIso, String slotEndIso, String status, double energyKWh) {
        this.reservationId = reservationId;
        this.stationId = stationId;
        this.stationName = stationName;
        this.slotStartIso = slotStartIso;
        this.slotEndIso = slotEndIso;
        this.status = status;
        this.energyKWh = energyKWh;
    }
}
