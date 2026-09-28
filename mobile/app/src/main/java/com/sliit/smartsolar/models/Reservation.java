/*
 * File:    Reservation.java
 * Author:  Thisuri
 * Created: 2026-09-28
 * Purpose: A reservation as the API returns it (ReservationResponse). Every
 *          timestamp is kept as the API's ISO-8601 UTC string and converted
 *          to local time only when shown. Holds data only.
 */
package com.sliit.smartsolar.models;

public class Reservation {

    public final String id;
    public final String prosumerNic;
    public final String stationId;
    public final String slotId;

    /** Copied from the slot at booking time, ISO-8601 UTC. */
    public final String slotStart;
    public final String slotEnd;

    public final double energyKWh;
    public final String status;

    // Audit trail. Everything below createdAt/updatedAt may be null.
    public final String createdAt;
    public final String updatedAt;
    public final String approvedAt;
    public final String rejectionReason;
    public final String cancelledAt;
    public final String completedAt;

    // Plain value holder; ReservationParser fills it straight from the body.
    public Reservation(String id, String prosumerNic, String stationId, String slotId,
                       String slotStart, String slotEnd, double energyKWh, String status,
                       String createdAt, String updatedAt, String approvedAt,
                       String rejectionReason, String cancelledAt, String completedAt) {
        this.id = id;
        this.prosumerNic = prosumerNic;
        this.stationId = stationId;
        this.slotId = slotId;
        this.slotStart = slotStart;
        this.slotEnd = slotEnd;
        this.energyKWh = energyKWh;
        this.status = status;
        this.createdAt = createdAt;
        this.updatedAt = updatedAt;
        this.approvedAt = approvedAt;
        this.rejectionReason = rejectionReason;
        this.cancelledAt = cancelledAt;
        this.completedAt = completedAt;
    }
}
