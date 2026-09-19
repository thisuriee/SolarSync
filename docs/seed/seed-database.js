// =============================================================================
// seed-database.js
// Sample data for SmartSolarDb — run with mongosh.
//
//   mongosh "<your-atlas-connection-string>" --file docs/seed/seed-database.js
//
// Safe to re-run. It drops and recreates all four collections every time.
//
// All slot times are computed RELATIVE TO NOW, so the 7-day and 12-hour rule
// demos stay valid whenever you re-run it. Re-run before the Day 11 rehearsal
// and before recording the video.
//
// BEFORE FIRST RUN: replace PASSWORD_HASH below with one real hash produced by
// your own PasswordHasher<T> code (see note at the end of this file).
// =============================================================================

const DB_NAME = "SmartSolarDb";
const PASSWORD_HASH = "AQAAAAIAAYagAAAAELVMjjH5pePugJjJ7Elg0nAwESj7xZDE9+yL5RxOA5vouNvIZLl9ZMlEdHRKYN7buQ==";   // all seed users share one password

db = db.getSiblingDB(DB_NAME);

// --- helpers -----------------------------------------------------------------
function hrs(h) { return new Date(Date.now() + h * 3600 * 1000); }
function oid(s) { return ObjectId(s); }
const NOW = new Date();

// --- reset -------------------------------------------------------------------
["UsersDetail", "SolarStationInfo", "EnergyBookingSlots", "EnergyReservation"]
  .forEach(c => db.getCollection(c).drop());

// =============================================================================
// 1. UsersDetail
// =============================================================================
const U = {
  bo1: oid("650000000000000000000001"), bo2: oid("650000000000000000000002"),
  op1: oid("650000000000000000000011"), op2: oid("650000000000000000000012"),
  p1:  oid("650000000000000000000021"), p2:  oid("650000000000000000000022"),
  p3:  oid("650000000000000000000023"), p4:  oid("650000000000000000000024"),
  p5:  oid("650000000000000000000025"), p6:  oid("650000000000000000000026")
};

const NIC = {
  p1: "200012345671", p2: "199823456782", p3: "200134567893",
  p4: "199945678904", p5: "200256789015", p6: "199767890126"
};

db.UsersDetail.insertMany([
  { _id: U.bo1, nic: null, username: "backoffice.admin", fullName: "Amara Wickramasinghe",
    email: "amara.w@smartsolar.lk", phone: "0112345001", address: null,
    passwordHash: PASSWORD_HASH, role: "Backoffice", status: "Active",
    deactivationRequestedAt: null, activatedBy: null, activatedAt: null,
    createdAt: NOW, updatedAt: NOW },

  { _id: U.bo2, nic: null, username: "backoffice.officer", fullName: "Chathura Bandara",
    email: "chathura.b@smartsolar.lk", phone: "0112345002", address: null,
    passwordHash: PASSWORD_HASH, role: "Backoffice", status: "Active",
    deactivationRequestedAt: null, activatedBy: null, activatedAt: null,
    createdAt: NOW, updatedAt: NOW },

  { _id: U.op1, nic: null, username: "operator.colombo", fullName: "Isuru Gunawardena",
    email: "isuru.g@smartsolar.lk", phone: "0112345011", address: null,
    passwordHash: PASSWORD_HASH, role: "GridOperator", status: "Active",
    deactivationRequestedAt: null, activatedBy: null, activatedAt: null,
    createdAt: NOW, updatedAt: NOW },

  { _id: U.op2, nic: null, username: "operator.negombo", fullName: "Malsha Ekanayake",
    email: "malsha.e@smartsolar.lk", phone: "0112345012", address: null,
    passwordHash: PASSWORD_HASH, role: "GridOperator", status: "Active",
    deactivationRequestedAt: null, activatedBy: null, activatedAt: null,
    createdAt: NOW, updatedAt: NOW },

  // --- prosumers: 3 Active, 2 Pending, 1 Deactivated --------------------------
  { _id: U.p1, nic: NIC.p1, username: "nimal.perera", fullName: "Nimal Perera",
    email: "nimal.perera@gmail.com", phone: "0771234501", address: "45 Galle Road, Colombo 03",
    passwordHash: PASSWORD_HASH, role: "Prosumer", status: "Active",
    deactivationRequestedAt: null, activatedBy: U.bo1, activatedAt: hrs(-720),
    createdAt: hrs(-744), updatedAt: hrs(-720) },

  { _id: U.p2, nic: NIC.p2, username: "kasun.silva", fullName: "Kasun Silva",
    email: "kasun.silva@gmail.com", phone: "0771234502", address: "12 Main Street, Negombo",
    passwordHash: PASSWORD_HASH, role: "Prosumer", status: "Active",
    deactivationRequestedAt: null, activatedBy: U.bo1, activatedAt: hrs(-600),
    createdAt: hrs(-620), updatedAt: hrs(-600) },

  { _id: U.p3, nic: NIC.p3, username: "dilani.fernando", fullName: "Dilani Fernando",
    email: "dilani.f@gmail.com", phone: "0771234503", address: "88 Kandy Road, Gampaha",
    passwordHash: PASSWORD_HASH, role: "Prosumer", status: "Active",
    deactivationRequestedAt: null, activatedBy: U.bo2, activatedAt: hrs(-480),
    createdAt: hrs(-500), updatedAt: hrs(-480) },

  { _id: U.p4, nic: NIC.p4, username: "tharindu.j", fullName: "Tharindu Jayasuriya",
    email: "tharindu.j@gmail.com", phone: "0771234504", address: "7 Negombo Road, Ja-Ela",
    passwordHash: PASSWORD_HASH, role: "Prosumer", status: "Pending",
    deactivationRequestedAt: null, activatedBy: null, activatedAt: null,
    createdAt: hrs(-48), updatedAt: hrs(-48) },

  { _id: U.p5, nic: NIC.p5, username: "sanduni.r", fullName: "Sanduni Rathnayake",
    email: "sanduni.r@gmail.com", phone: "0771234505", address: "21 Station Road, Wattala",
    passwordHash: PASSWORD_HASH, role: "Prosumer", status: "Pending",
    deactivationRequestedAt: null, activatedBy: null, activatedAt: null,
    createdAt: hrs(-20), updatedAt: hrs(-20) },

  { _id: U.p6, nic: NIC.p6, username: "ruwan.d", fullName: "Ruwan Dissanayake",
    email: "ruwan.d@gmail.com", phone: "0771234506", address: "3 Temple Road, Ragama",
    passwordHash: PASSWORD_HASH, role: "Prosumer", status: "Deactivated",
    deactivationRequestedAt: hrs(-96), activatedBy: U.bo1, activatedAt: hrs(-400),
    createdAt: hrs(-420), updatedAt: hrs(-96) }
]);

// =============================================================================
// 2. SolarStationInfo   (GeoJSON coordinates are [longitude, latitude])
// =============================================================================
const S = {
  s1: oid("660000000000000000000001"), s2: oid("660000000000000000000002"),
  s3: oid("660000000000000000000003"), s4: oid("660000000000000000000004"),
  s5: oid("660000000000000000000005")
};

const WEEK = [
  { dayOfWeek: 1, openTime: "06:00", closeTime: "18:00" },
  { dayOfWeek: 2, openTime: "06:00", closeTime: "18:00" },
  { dayOfWeek: 3, openTime: "06:00", closeTime: "18:00" },
  { dayOfWeek: 4, openTime: "06:00", closeTime: "18:00" },
  { dayOfWeek: 5, openTime: "06:00", closeTime: "18:00" },
  { dayOfWeek: 6, openTime: "07:00", closeTime: "16:00" },
  { dayOfWeek: 0, openTime: "07:00", closeTime: "16:00" }
];

db.SolarStationInfo.insertMany([
  { _id: S.s1, stationName: "Colombo Fort Solar Hub", addressLine: "12 Chatham Street", city: "Colombo",
    location: { type: "Point", coordinates: [79.8428, 6.9344] },
    capacityKWh: 250.0, totalBatterySlots: 8, status: "Active",
    operatingSchedule: WEEK, createdBy: U.bo1, createdAt: hrs(-1000), updatedAt: hrs(-1000) },

  { _id: S.s2, stationName: "Negombo Beach Microgrid", addressLine: "104 Lewis Place", city: "Negombo",
    location: { type: "Point", coordinates: [79.8358, 7.2083] },
    capacityKWh: 180.0, totalBatterySlots: 6, status: "Active",
    operatingSchedule: WEEK, createdBy: U.bo1, createdAt: hrs(-990), updatedAt: hrs(-990) },

  { _id: S.s3, stationName: "Gampaha Central Node", addressLine: "56 Bauddhaloka Mawatha", city: "Gampaha",
    location: { type: "Point", coordinates: [80.0000, 7.0917] },
    capacityKWh: 320.0, totalBatterySlots: 10, status: "Active",
    operatingSchedule: WEEK, createdBy: U.bo2, createdAt: hrs(-980), updatedAt: hrs(-980) },

  { _id: S.s4, stationName: "Ja-Ela Industrial Array", addressLine: "Zone 2, Ekala Industrial Park", city: "Ja-Ela",
    location: { type: "Point", coordinates: [79.8919, 7.0744] },
    capacityKWh: 400.0, totalBatterySlots: 12, status: "Active",
    operatingSchedule: WEEK, createdBy: U.bo2, createdAt: hrs(-970), updatedAt: hrs(-970) },

  { _id: S.s5, stationName: "Wattala Grid Hub", addressLine: "9 Hendala Road", city: "Wattala",
    location: { type: "Point", coordinates: [79.8917, 6.9897] },
    capacityKWh: 150.0, totalBatterySlots: 5, status: "Inactive",
    operatingSchedule: WEEK, createdBy: U.bo1, createdAt: hrs(-960), updatedAt: hrs(-200) }
]);

// =============================================================================
// 3. EnergyBookingSlots
//    reservedCount and status are set LAST, computed from the reservations
//    actually inserted below — so the data can never be internally inconsistent.
// =============================================================================
const L = {};
for (let i = 1; i <= 20; i++) {
  L["l" + i] = oid("6700000000000000000000" + (i < 10 ? "0" + i : i));
}

function slot(id, stationId, startH, endH, cap, kwh) {
  return { _id: id, stationId: stationId, slotStart: hrs(startH), slotEnd: hrs(endH),
           totalCapacity: cap, reservedCount: 0, energyPerSlotKWh: kwh,
           status: "Open", createdAt: hrs(-200), updatedAt: hrs(-200) };
}

db.EnergyBookingSlots.insertMany([
  // Station 1 — includes the INSIDE-12-HOURS slot for the notice-period demo
  slot(L.l1,  S.s1,    8,  12, 4, 30.0),   // starts in 8h  -> cancel/update must FAIL
  slot(L.l2,  S.s1,   26,  30, 4, 30.0),
  slot(L.l3,  S.s1,   50,  54, 4, 30.0),
  slot(L.l4,  S.s1,   98, 102, 4, 30.0),

  // Station 2
  slot(L.l5,  S.s2,   28,  32, 3, 25.0),
  slot(L.l6,  S.s2,   52,  56, 3, 25.0),
  slot(L.l7,  S.s2,   76,  80, 3, 25.0),
  slot(L.l8,  S.s2,  124, 128, 3, 25.0),

  // Station 3
  slot(L.l9,  S.s3,   30,  34, 5, 40.0),
  slot(L.l10, S.s3,   54,  58, 5, 40.0),
  slot(L.l11, S.s3,  102, 106, 5, 40.0),
  slot(L.l12, S.s3,  150, 154, 5, 40.0),

  // Station 4
  slot(L.l13, S.s4,   34,  38, 6, 50.0),
  slot(L.l14, S.s4,   58,  62, 6, 50.0),
  slot(L.l15, S.s4,  106, 110, 6, 50.0),
  slot(L.l16, S.s4,  154, 158, 6, 50.0),

  // Station 5 (Inactive station)
  slot(L.l17, S.s5,   36,  40, 2, 20.0),
  slot(L.l18, S.s5,   60,  64, 2, 20.0),

  // Small-capacity slot — fill it to demonstrate RESERVATION_SLOT_FULL
  slot(L.l19, S.s1,   72,  76, 1, 30.0),

  // OUTSIDE the 7-day window (>168h) — booking this must return 422
  slot(L.l20, S.s3,  200, 204, 5, 40.0)
]);

// One slot is administratively closed
db.EnergyBookingSlots.updateOne({ _id: L.l18 }, { $set: { status: "Closed" } });

// =============================================================================
// 4. EnergyReservation
// =============================================================================
function res(idSuffix, nic, stationId, slotId, slotStartHours, kwh, status, extra) {
  const base = {
    _id: oid("6800000000000000000000" + idSuffix),
    prosumerNIC: nic, stationId: stationId, slotId: slotId,
    slotStart: hrs(slotStartHours), energyKWh: kwh, status: status,
    createdAt: hrs(-72), updatedAt: hrs(-72),
    approvedBy: null, approvedAt: null, rejectionReason: null, cancelledAt: null,
    qrTokenHash: null, qrIssuedAt: null, qrExpiresAt: null,
    verifiedByOperatorId: null, completedAt: null
  };
  return Object.assign(base, extra || {});
}

db.EnergyReservation.insertMany([
  // --- Pending -------------------------------------------------------------
  res("01", NIC.p1, S.s1, L.l2,  26, 12.0, "Pending"),
  res("02", NIC.p2, S.s2, L.l5,  28, 10.0, "Pending"),
  res("03", NIC.p3, S.s3, L.l9,  30, 15.0, "Pending"),
  res("04", NIC.p1, S.s4, L.l13, 34, 20.0, "Pending"),

  // --- Approved (future) — these drive approvedFutureCount ------------------
  res("05", NIC.p1, S.s1, L.l3,  50, 12.0, "Approved",
      { approvedBy: U.op1, approvedAt: hrs(-40) }),
  res("06", NIC.p2, S.s2, L.l6,  52,  9.0, "Approved",
      { approvedBy: U.op2, approvedAt: hrs(-38) }),
  res("07", NIC.p3, S.s3, L.l10, 54, 18.0, "Approved",
      { approvedBy: U.op1, approvedAt: hrs(-36) }),

  // --- Approved WITH a live QR issued (for the scan demo) -------------------
  // qrTokenHash is a placeholder; issue a real one via the API when demoing.
  res("08", NIC.p1, S.s4, L.l14, 58, 22.0, "Approved",
      { approvedBy: U.op1, approvedAt: hrs(-20),
        qrTokenHash: "__SEEDED_PLACEHOLDER_HASH__",
        qrIssuedAt: hrs(-20), qrExpiresAt: hrs(64) }),

  // --- Approved, starting INSIDE 12 hours (notice-period demo) --------------
  res("09", NIC.p2, S.s1, L.l1,   8, 11.0, "Approved",
      { approvedBy: U.op1, approvedAt: hrs(-30) }),

  // --- Slot 19 filled to capacity (RESERVATION_SLOT_FULL demo) -------------
  res("10", NIC.p3, S.s1, L.l19, 72, 30.0, "Approved",
      { approvedBy: U.op1, approvedAt: hrs(-25) }),

  // --- Completed (with operator verification) ------------------------------
  res("11", NIC.p1, S.s1, L.l1, -160, 14.0, "Completed",
      { createdAt: hrs(-260), updatedAt: hrs(-158),
        approvedBy: U.op1, approvedAt: hrs(-200),
        qrTokenHash: "__USED_TOKEN_HASH_1__", qrIssuedAt: hrs(-200), qrExpiresAt: hrs(-154),
        verifiedByOperatorId: U.op1, completedAt: hrs(-158) }),

  res("12", NIC.p2, S.s2, L.l5, -136, 8.5, "Completed",
      { createdAt: hrs(-240), updatedAt: hrs(-134),
        approvedBy: U.op2, approvedAt: hrs(-180),
        qrTokenHash: "__USED_TOKEN_HASH_2__", qrIssuedAt: hrs(-180), qrExpiresAt: hrs(-130),
        verifiedByOperatorId: U.op2, completedAt: hrs(-134) }),

  res("13", NIC.p3, S.s3, L.l9, -112, 16.0, "Completed",
      { createdAt: hrs(-220), updatedAt: hrs(-110),
        approvedBy: U.op1, approvedAt: hrs(-160),
        qrTokenHash: "__USED_TOKEN_HASH_3__", qrIssuedAt: hrs(-160), qrExpiresAt: hrs(-106),
        verifiedByOperatorId: U.op1, completedAt: hrs(-110) }),

  // --- Cancelled -----------------------------------------------------------
  res("14", NIC.p1, S.s2, L.l7, 76, 10.0, "Cancelled",
      { cancelledAt: hrs(-12), updatedAt: hrs(-12) }),

  res("15", NIC.p6, S.s3, L.l11, 102, 13.0, "Cancelled",
      { cancelledAt: hrs(-96), updatedAt: hrs(-96) }),

  // --- Rejected ------------------------------------------------------------
  res("16", NIC.p2, S.s4, L.l15, 106, 45.0, "Rejected",
      { rejectionReason: "Requested energy exceeds available allocation for this slot.",
        updatedAt: hrs(-60) })
]);

// =============================================================================
// 5. Derive reservedCount and slot status from the reservations just inserted
//    Active = status in {Pending, Approved} AND slotStart in the future
// =============================================================================
const active = db.EnergyReservation.aggregate([
  { $match: { status: { $in: ["Pending", "Approved"] }, slotStart: { $gt: new Date() } } },
  { $group: { _id: "$slotId", n: { $sum: 1 } } }
]).toArray();

active.forEach(a => db.EnergyBookingSlots.updateOne({ _id: a._id }, { $set: { reservedCount: a.n } }));

db.EnergyBookingSlots.find({ status: { $ne: "Closed" } }).forEach(s => {
  db.EnergyBookingSlots.updateOne(
    { _id: s._id },
    { $set: { status: (s.reservedCount >= s.totalCapacity ? "Full" : "Open") } }
  );
});
// re-read is needed because the cursor above predates the reservedCount update
db.EnergyBookingSlots.find({ status: { $ne: "Closed" } }).forEach(s => {
  db.EnergyBookingSlots.updateOne(
    { _id: s._id },
    { $set: { status: (s.reservedCount >= s.totalCapacity ? "Full" : "Open") } }
  );
});

// =============================================================================
// 6. Indexes  (see docs/db-schema.md)
// =============================================================================
db.UsersDetail.createIndex({ nic: 1 },      { unique: true, partialFilterExpression: { nic: { $type: "string" } } });  // partial, not sparse: web users store nic: null
db.UsersDetail.createIndex({ username: 1 }, { unique: true });
db.UsersDetail.createIndex({ email: 1 },    { unique: true });
db.UsersDetail.createIndex({ role: 1, status: 1 });

db.SolarStationInfo.createIndex({ location: "2dsphere" });
db.SolarStationInfo.createIndex({ status: 1 });

db.EnergyBookingSlots.createIndex({ stationId: 1, slotStart: 1 });
db.EnergyBookingSlots.createIndex({ slotStart: 1 });

db.EnergyReservation.createIndex({ prosumerNIC: 1, slotStart: -1 });
db.EnergyReservation.createIndex({ slotId: 1, status: 1 });
db.EnergyReservation.createIndex({ stationId: 1, status: 1 });
db.EnergyReservation.createIndex({ status: 1, slotStart: 1 });
db.EnergyReservation.createIndex({ qrTokenHash: 1 }, { sparse: true });

// =============================================================================
// 7. Verification — every reference must resolve
// =============================================================================
print("\n--- counts ---");
print("UsersDetail        : " + db.UsersDetail.countDocuments());
print("SolarStationInfo   : " + db.SolarStationInfo.countDocuments());
print("EnergyBookingSlots : " + db.EnergyBookingSlots.countDocuments());
print("EnergyReservation  : " + db.EnergyReservation.countDocuments());

const nics = db.UsersDetail.distinct("nic");
const stationIds = db.SolarStationInfo.distinct("_id").map(String);
const slotIds = db.EnergyBookingSlots.distinct("_id").map(String);

let bad = 0;
db.EnergyReservation.find().forEach(r => {
  if (nics.indexOf(r.prosumerNIC) === -1) { print("DANGLING NIC: " + r._id); bad++; }
  if (stationIds.indexOf(String(r.stationId)) === -1) { print("DANGLING stationId: " + r._id); bad++; }
  if (slotIds.indexOf(String(r.slotId)) === -1) { print("DANGLING slotId: " + r._id); bad++; }
});
db.EnergyBookingSlots.find().forEach(s => {
  if (stationIds.indexOf(String(s.stationId)) === -1) { print("DANGLING stationId on slot: " + s._id); bad++; }
  if (s.reservedCount > s.totalCapacity) { print("OVER CAPACITY: " + s._id); bad++; }
});

print("\n--- reference check ---");
print(bad === 0 ? "OK — every reference resolves, no slot over capacity."
                : "FAILED — " + bad + " problem(s) above.");

print("\n--- demo fixtures ---");
print("Inside-12-hours slot   : " + L.l1  + " (reservation ...09 starts in ~8h)");
print("Outside-7-days slot    : " + L.l20 + " (booking it must return 422)");
print("Full slot              : " + L.l19 + " (booking it must return 409)");
print("Closed slot            : " + L.l18);
print("Inactive station       : " + S.s5);
print("Pending prosumers      : " + NIC.p4 + ", " + NIC.p5);
print("Deactivated prosumer   : " + NIC.p6);

// =============================================================================
// PASSWORD HASH — do this once, on Day 1
// -----------------------------------------------------------------------------
// PasswordHasher<T> output cannot be produced outside .NET. Once M1 has the
// hashing helper working:
//   1. Hash the string  Test@1234  with your own code.
//   2. Replace PASSWORD_HASH at the top of this file with that value.
//   3. Re-run this script.
// Every seed user then logs in with password  Test@1234
// Usernames are listed in the UsersDetail block above.
// =============================================================================