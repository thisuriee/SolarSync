/*
 * File:    ProsumerDashboard.java
 * Author:  Imadh
 * Created: 2026-09-30
 * Purpose: The body of GET /api/dashboard/prosumer (docs/api-contract.md §5).
 *          Holds data only.
 *
 *          Every count arrives from the server already computed. Nothing on the
 *          device re-counts a list to produce one of these numbers, which is the
 *          rubric's stated failure mode, so this is a field-for-field mapping of
 *          what the response said.
 */
package com.sliit.smartsolar.models;

public class ProsumerDashboard {

    /** Pending or Approved, with the slot still ahead. */
    public final long activeCount;

    /** Every booking still awaiting a decision, whenever it is for. */
    public final long pendingCount;

    /** Approved and still upcoming. */
    public final long approvedFutureCount;

    /** The soonest upcoming approved booking, or null when there is none. */
    public final Reservation nextBooking;

    public ProsumerDashboard(long activeCount, long pendingCount,
                             long approvedFutureCount, Reservation nextBooking) {
        this.activeCount = activeCount;
        this.pendingCount = pendingCount;
        this.approvedFutureCount = approvedFutureCount;
        this.nextBooking = nextBooking;
    }
}
