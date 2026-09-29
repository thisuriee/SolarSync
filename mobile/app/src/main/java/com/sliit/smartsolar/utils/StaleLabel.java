/*
 * File:    StaleLabel.java
 * Author:  Imadh
 * Created: 2026-09-29
 * Purpose: The one place a screen asks "when was this cache taken?", so a
 *          cached list is described identically whichever cache it came from.
 *
 *          A cached list is never presented as current. Every screen that falls
 *          back to the cache shows this value beside the rows, and that label is
 *          what keeps a stale list visibly stale rather than passing as live.
 *
 *          Deliberately takes an already-read timestamp rather than a DAO or a
 *          Context: it owns the formatting decision and nothing else, so it has
 *          no dependency on the database package and either cache can use it.
 */
package com.sliit.smartsolar.utils;

import com.sliit.smartsolar.network.DateUtils;

public final class StaleLabel {

    private StaleLabel() {
    }

    // The instant a cache was taken, formatted in the device's local time, or
    // null when there is no usable timestamp — because nothing has been cached
    // yet, or because the stored value is not the contract's ISO-8601 shape.
    //
    // Null rather than an empty string on purpose: a caller shows "no cache
    // yet" and "cached at a known time" as two different lines, and an empty
    // string would render the second one as "Showing saved data from ".
    public static String lastUpdated(String lastSyncedAtIso) {
        String when = DateUtils.toLocalDisplay(lastSyncedAtIso);
        return when.isEmpty() ? null : when;
    }
}
