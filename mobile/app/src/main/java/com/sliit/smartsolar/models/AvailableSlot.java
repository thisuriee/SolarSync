/*
 * File:    AvailableSlot.java
 * Author:  Thisuri
 * Created: 2026-09-28
 * Purpose: One bookable window from GET /slots/available (the API's
 *          SlotResponse). Never stored: capacity changes with every booking,
 *          so it is re-fetched each time the search screen is shown. Holds
 *          data only.
 */
package com.sliit.smartsolar.models;

public class AvailableSlot {

    public final String id;
    public final String stationId;

    /** ISO-8601 UTC. */
    public final String slotStart;
    public final String slotEnd;

    public final int totalCapacity;
    public final int availableCapacity;
    public final double energyPerSlotKWh;

    /**
     * The slot exactly as the API sent it, so it can be handed to the next
     * screen in an Intent and re-parsed there by the same parser.
     */
    public final String rawJson;

    // Plain value holder; ReservationParser fills it straight from the body.
    public AvailableSlot(String id, String stationId, String slotStart, String slotEnd,
                         int totalCapacity, int availableCapacity, double energyPerSlotKWh,
                         String rawJson) {
        this.id = id;
        this.stationId = stationId;
        this.slotStart = slotStart;
        this.slotEnd = slotEnd;
        this.totalCapacity = totalCapacity;
        this.availableCapacity = availableCapacity;
        this.energyPerSlotKWh = energyPerSlotKWh;
        this.rawJson = rawJson;
    }
}
