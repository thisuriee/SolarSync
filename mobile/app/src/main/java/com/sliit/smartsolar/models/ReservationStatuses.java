/*
 * File:    ReservationStatuses.java
 * Author:  Thisuri
 * Created: 2026-09-28
 * Purpose: The reservation status strings the API issues, mirrored from the
 *          API's Models/StationStatuses.cs (ReservationStatuses) so no screen
 *          hard-codes "Pending". Used to group and label bookings for display
 *          only — which transitions are legal is decided by the API's guard.
 */
package com.sliit.smartsolar.models;

public final class ReservationStatuses {

    public static final String PENDING = "Pending";
    public static final String APPROVED = "Approved";
    public static final String REJECTED = "Rejected";
    public static final String CANCELLED = "Cancelled";
    public static final String COMPLETED = "Completed";

    private ReservationStatuses() {
    }

    // True for the two statuses a prosumer may still ask to change or cancel.
    // A display hint only (which buttons to show): the API re-checks the status
    // and the notice period on every request and answers with its own detail.
    public static boolean isOpen(String status) {
        return PENDING.equals(status) || APPROVED.equals(status);
    }
}
