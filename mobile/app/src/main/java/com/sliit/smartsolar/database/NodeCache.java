/*
 * File:    NodeCache.java
 * Author:  Aman
 * Created: 2026-09-29
 * Purpose: Maps between the Node model and the cached_nodes table, and records
 *          when the cache was last refreshed.
 *
 *          The cache is READ-THROUGH and never authoritative. Every screen calls
 *          the API first and renders the response; this is written only after a
 *          call succeeds, and read only when one fails. Nothing decides anything
 *          from it — a stale row is shown with the time it was taken, never
 *          treated as current.
 *
 *          It stores no distance. Distance is measured from wherever the phone
 *          is standing at the time, so a saved one would be wrong the moment the
 *          user moved, and quietly so.
 */
package com.sliit.smartsolar.database;

import android.content.ContentValues;
import android.content.Context;

import com.sliit.smartsolar.models.Node;
import com.sliit.smartsolar.network.DateUtils;
import com.sliit.smartsolar.utils.StaleLabel;

import java.util.ArrayList;
import java.util.Date;
import java.util.List;

public class NodeCache {

    /** Key this cache is recorded under in the sync bookkeeping. */
    public static final String ENTITY = "nodes";

    private final NodeDao nodes;
    private final SyncMetaDao syncMeta;

    public NodeCache(Context context) {
        this.nodes = new NodeDao(context);
        this.syncMeta = new SyncMetaDao(context);
    }

    // Replaces the cache with a freshly fetched list and stamps the time.
    // Called only after the API answered, which is what makes this a cache of
    // what the server said rather than a store of what the app believes.
    public void save(List<Node> fresh) {
        List<ContentValues> rows = new ArrayList<>(fresh.size());
        String now = DateUtils.toUtcWire(new Date());

        for (Node node : fresh) {
            ContentValues row = new ContentValues();
            row.put(DbContract.CachedNodes.STATION_ID, node.id);
            row.put(DbContract.CachedNodes.STATION_NAME, node.stationName);
            row.put(DbContract.CachedNodes.CITY, node.city);
            row.put(DbContract.CachedNodes.LAT, node.lat);
            row.put(DbContract.CachedNodes.LNG, node.lng);
            row.put(DbContract.CachedNodes.CAPACITY_KWH, node.capacityKWh);
            row.put(DbContract.CachedNodes.TOTAL_SLOTS, node.totalBatterySlots);
            row.put(DbContract.CachedNodes.STATUS, node.status);
            row.put(DbContract.CachedNodes.CACHED_AT, now);
            rows.add(row);
        }

        nodes.replaceAll(rows);
        syncMeta.markSynced(ENTITY, now);
    }

    // Reads the cache back as Nodes. Empty when nothing has ever been cached,
    // which a screen must tell apart from "the server returned nothing".
    //
    // The address line is not stored, and no distance is reconstructed: a
    // cached row carries only what a list needs to name a node, and the
    // sentinel keeps the distance line off the screen entirely.
    public List<Node> load() {
        List<ContentValues> rows = nodes.findAll();
        List<Node> cached = new ArrayList<>(rows.size());

        for (ContentValues row : rows) {
            cached.add(new Node(
                    asString(row, DbContract.CachedNodes.STATION_ID),
                    asString(row, DbContract.CachedNodes.STATION_NAME),
                    "",
                    asString(row, DbContract.CachedNodes.CITY),
                    asDouble(row, DbContract.CachedNodes.LAT),
                    asDouble(row, DbContract.CachedNodes.LNG),
                    asDouble(row, DbContract.CachedNodes.CAPACITY_KWH),
                    asInt(row, DbContract.CachedNodes.TOTAL_SLOTS),
                    asString(row, DbContract.CachedNodes.STATUS),
                    Node.NO_DISTANCE));
        }

        return cached;
    }

    // When the cache was last refreshed, already formatted for display, or null
    // when it has never been filled. Screens show this beside cached data so a
    // stale list is visibly stale rather than passing as current.
    //
    // The formatting itself lives in StaleLabel, shared with the booking cache,
    // so both caches describe a stale list the same way.
    public String lastRefreshedDisplay() {
        return StaleLabel.lastUpdated(syncMeta.getLastSyncedAt(ENTITY));
    }

    // SQLite has no strict typing, so each value is read defensively rather
    // than assuming the column came back as the type it was written with.
    private static String asString(ContentValues row, String key) {
        String value = row.getAsString(key);
        return value == null ? "" : value;
    }

    private static double asDouble(ContentValues row, String key) {
        Double value = row.getAsDouble(key);
        return value == null ? 0 : value;
    }

    private static int asInt(ContentValues row, String key) {
        Integer value = row.getAsInteger(key);
        return value == null ? 0 : value;
    }
}
