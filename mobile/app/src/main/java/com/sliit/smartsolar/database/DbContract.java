/*
 * File:    DbContract.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Table and column names for the local SQLite cache in one place, so
 *          no DAO, parser or screen hard-codes a string. Plumbing only.
 *
 *          The database is a read-through cache and a session store. It is
 *          never authoritative: the API decides everything, and every screen
 *          calls the API first and overwrites the cache on success.
 */
package com.sliit.smartsolar.database;

public final class DbContract {

    public static final String DB_NAME = "smartsolar.db";

    /** Bump when a table changes. onUpgrade drops and recreates. */
    public static final int DB_VERSION = 1;

    private DbContract() {
    }

    /** Single row holding the signed-in user. The password is never stored. */
    public static final class Session {
        public static final String TABLE = "session";
        public static final String ID = "id";
        public static final String USER_ID = "user_id";
        public static final String USERNAME = "username";
        public static final String NIC = "nic";
        public static final String FULL_NAME = "full_name";
        public static final String ROLE = "role";
        public static final String TOKEN = "token";
        public static final String ISSUED_AT = "issued_at";
        public static final String EXPIRES_AT = "expires_at";

        /** The session table always holds exactly this one row id. */
        public static final String SINGLE_ROW_ID = "1";

        private Session() {
        }
    }

    /** Read-through cache of the node list, owned by M2. */
    public static final class CachedNodes {
        public static final String TABLE = "cached_nodes";
        public static final String STATION_ID = "station_id";
        public static final String STATION_NAME = "station_name";
        public static final String CITY = "city";
        public static final String LAT = "lat";
        public static final String LNG = "lng";
        public static final String CAPACITY_KWH = "capacity_kwh";
        public static final String TOTAL_SLOTS = "total_slots";
        public static final String STATUS = "status";
        public static final String CACHED_AT = "cached_at";

        private CachedNodes() {
        }
    }

    /** Read-through cache of the prosumer's bookings, owned by M4. */
    public static final class CachedBookings {
        public static final String TABLE = "cached_bookings";
        public static final String RESERVATION_ID = "reservation_id";
        public static final String STATION_ID = "station_id";
        public static final String STATION_NAME = "station_name";
        public static final String SLOT_START = "slot_start";
        public static final String SLOT_END = "slot_end";
        public static final String ENERGY_KWH = "energy_kwh";
        public static final String STATUS = "status";
        public static final String CACHED_AT = "cached_at";

        private CachedBookings() {
        }
    }

    /** When each cache entity was last refreshed, so screens can label staleness. */
    public static final class SyncMeta {
        public static final String TABLE = "sync_meta";
        public static final String ENTITY = "entity";
        public static final String LAST_SYNCED_AT = "last_synced_at";

        private SyncMeta() {
        }
    }
}
