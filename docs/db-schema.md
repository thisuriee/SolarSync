# Database Schema — MongoDB

**Database:** `SmartSolarDb` on MongoDB Atlas (free tier M0).
**Four collections, exactly as named in the marking scheme.** Field names below are final — both clients are built against the JSON they produce.

## Global conventions

| Rule | Detail |
|---|---|
| Time | Everything stored and compared in **UTC**. Sri Lanka is UTC+5:30, so mixing `DateTime.Now` into a rule silently turns "12 hours" into 6h30m. Always `DateTime.UtcNow` |
| `_id` | `ObjectId`, serialised to the wire as a plain string |
| Audit fields | `createdAt` / `updatedAt` on every document, set server-side, never accepted from a client |
| Enums | Stored as strings, not ints — readable in Compass, and the sample data is legible to a marker |
| References | Held by the **child** document. No embedded arrays of ids on the parent |

---

## 1. `UsersDetail`

All three roles live here, discriminated by `role`.

| Field | Type | Required | Notes |
|---|---|---|---|
| `_id` | ObjectId | ✓ | |
| `nic` | string | Prosumer only | **Unique partial index.** Null for web users. Old (`9 digits + V/X`) or new (`12 digits`) format, stored upper-case — see `api-contract.md` §2. **Immutable after creation** — the API never accepts it in an update body |
| `username` | string | ✓ | **Unique index.** Login identifier for all three roles |
| `fullName` | string | ✓ | |
| `email` | string | ✓ | Unique index |
| `phone` | string | ✓ | |
| `address` | string | Prosumer | |
| `passwordHash` | string | ✓ | PBKDF2 via `PasswordHasher<T>`. **Never serialised into any response** — excluded at the DTO layer, not by hoping |
| `role` | string enum | ✓ | `Backoffice` \| `GridOperator` \| `Prosumer` |
| `status` | string enum | ✓ | `Pending` \| `Active` \| `Deactivated` |
| `deactivationRequestedAt` | date? | | Set by the prosumer self-service request |
| `deactivationReason` | string? | | Optional note from `PATCH /prosumers/{nic}/deactivate` (Backoffice). Cleared on reactivation |
| `activatedBy` | ObjectId? | | `_id` of the Backoffice officer — the audit evidence for the Backoffice-only reactivation rule |
| `activatedAt` | date? | | |
| `createdAt` / `updatedAt` | date | ✓ | UTC |

**Indexes**

| Index | Type | Why |
|---|---|---|
| `{nic: 1}` | unique, partial (`nic` is a string) | NIC as the business primary key. Partial, not sparse: web users store `nic: null`, and a sparse index still indexes an explicit null, so the second web user would collide |
| `{username: 1}` | unique | Login lookup |
| `{email: 1}` | unique | |
| `{role: 1, status: 1}` | compound | Drives `/prosumers/pending` and the user-management filters |

**Design note for viva:** NIC is the business primary key — uniquely indexed and immutable in the API — while `_id` stays an ObjectId so one collection holds all three roles with a uniform identity type, and a data-entry error in a NIC is correctable without orphaning reservations.

---

## 2. `SolarStationInfo`

The microgrid nodes.

| Field | Type | Required | Notes |
|---|---|---|---|
| `_id` | ObjectId | ✓ | |
| `stationName` | string | ✓ | |
| `addressLine` | string | ✓ | |
| `city` | string | ✓ | |
| `location` | `{ type: "Point", coordinates: [lng, lat] }` | ✓ | GeoJSON. **Longitude first** — the most common bug in this collection. Enables a real `$near` query rather than a C# distance loop |
| `capacityKWh` | double | ✓ | > 0 |
| `totalBatterySlots` | int | ✓ | ≥ 1. Upper bound for any slot's `totalCapacity` |
| `status` | string enum | ✓ | `Active` \| `Inactive` |
| `operatingSchedule` | array of `{ dayOfWeek: int, openTime: "HH:mm", closeTime: "HH:mm" }` | ✓ | Embedded — a schedule entry has no life outside its station |
| `createdBy` | ObjectId | ✓ | → `UsersDetail._id` |
| `createdAt` / `updatedAt` | date | ✓ | |

**Indexes**

| Index | Type | Why |
|---|---|---|
| `{location: "2dsphere"}` | geospatial | `/nodes/nearby` server-side distance |
| `{status: 1}` | single | Prosumer sees active nodes only |

**Constraints the API enforces** — added when the node endpoints were built. No field
was added, removed or renamed; this records what `NodeService` now guarantees about
documents written through the API, so the seed script and the API agree.

| Field | Guarantee | Violation |
|---|---|---|
| `location` | Written only by `NodeService`, which maps an inbound `lat`/`lng` pair into `[longitude, latitude]`. Clients never send GeoJSON | — |
| `capacityKWh` | Strictly greater than 0 | `400 NODE_INVALID_CAPACITY` |
| `totalBatterySlots` | At least 1, and **never reducible below the largest `totalCapacity` of any slot on this station** | `400 NODE_INVALID_CAPACITY` / `409 NODE_CAPACITY_CONFLICT` |
| `status` | Set to `Active` on create and thereafter changed only by the activate/deactivate routes. Not accepted in a create or update body | — |
| `operatingSchedule` | At least one row; each `dayOfWeek` appears at most once; `closeTime` strictly after `openTime`; both `HH:mm` 24-hour | `400 NODE_INVALID_SCHEDULE` |
| `createdBy` | Taken from the caller's JWT `sub` claim. Not accepted in a request body | — |
| `createdAt` / `updatedAt` | `DateTime.UtcNow`, server-side. `createdAt` is preserved across updates | — |

> `status`, `createdBy` and `createdAt` are absent from `NodeCreateRequest` and
> `NodeUpdateRequest` altogether. A field a client cannot bind is a rule that cannot be
> broken by editing a request.

---

## 3. `EnergyBookingSlots`

Bookable time windows belonging to a node. Separate collection, not embedded — the marking scheme names it as one of four collections.

| Field | Type | Required | Notes |
|---|---|---|---|
| `_id` | ObjectId | ✓ | |
| `stationId` | ObjectId | ✓ | → `SolarStationInfo._id`. The child holds the reference |
| `slotStart` | date (UTC) | ✓ | |
| `slotEnd` | date (UTC) | ✓ | > `slotStart` |
| `totalCapacity` | int | ✓ | ≤ station's `totalBatterySlots`. Capacity > 1 — multiple prosumers may share a slot |
| `reservedCount` | int | ✓ | **Mutated only by `ReservationService`.** Slot CRUD endpoints ignore it in request bodies |
| `energyPerSlotKWh` | double | ✓ | Energy available in this window |
| `status` | string enum | ✓ | `Open` \| `Full` \| `Closed`. **Stored, not computed** — so the mobile slot list filters server-side without post-processing. M3 owns the invariant that `status` and `reservedCount` change in the same write |
| `createdAt` / `updatedAt` | date | ✓ | |

**Indexes**

| Index | Type | Why |
|---|---|---|
| `{stationId: 1, slotStart: 1}` | compound | Slot listing per node, ordered |
| `{slotStart: 1}` | single | `/slots/available` 7-day window filter |

**Constraints the API enforces** — added when the slot endpoints were built. No field was
added, removed or renamed; this records what `SlotService` guarantees about documents
written through the API.

| Field | Guarantee | Violation |
|---|---|---|
| `stationId` | Taken from the route on create and carried over unchanged on update. A window cannot be moved to another node, which would silently relocate any booking on it | — |
| `slotStart` / `slotEnd` | `slotEnd` strictly after `slotStart` | `400 SLOT_INVALID_WINDOW` |
| window vs. siblings | No two windows on the same node may cover the same time. Ranges are **half-open**, so a window ending exactly as the next begins is adjacent, not overlapping | `409 SLOT_OVERLAP` |
| `totalCapacity` | At least 1; never above the station's `totalBatterySlots`; never reduced below the slot's own `reservedCount` | `400 SLOT_INVALID_CAPACITY` · `409 NODE_CAPACITY_CONFLICT` · `409 SLOT_CAPACITY_CONFLICT` |
| `energyPerSlotKWh` | Strictly greater than 0 | `400 SLOT_INVALID_CAPACITY` |
| `reservedCount` | Set to 0 on create and **never written again by slot CRUD**. Update rebuilds the document from the stored value | — |
| `status` | Set to `Open` on create and never written again by slot CRUD | — |
| `createdAt` / `updatedAt` | `DateTime.UtcNow`, server-side. `createdAt` preserved across updates | — |
| deletion | Refused while any reservation references the slot — active ones because someone is relying on them, historical ones because their `slotId` would be left dangling | `409 SLOT_HAS_RESERVATIONS` |

> `reservedCount` and `status` appear on neither `SlotCreateRequest` nor `SlotUpdateRequest`.
> A field a client cannot bind cannot be applied, which is why the contract's "ignored in
> request bodies" needs no defensive code to honour.

**A slot is only offered for booking when its station is `Active`.** `/slots/available`
filters on the parent station's status as well as the slot's own, so taking a node out of
service withdraws its windows from the booking list without anything being written to them.
Slot documents are never touched by a node status change — the node's status is the single
place that fact is recorded.

---

## 4. `EnergyReservation`

| Field | Type | Required | Notes |
|---|---|---|---|
| `_id` | ObjectId | ✓ | |
| `prosumerNIC` | string | ✓ | → `UsersDetail.nic`. Referenced by NIC, not `_id`, because NIC is the business key and it keeps history queries readable |
| `stationId` | ObjectId | ✓ | Denormalised from the slot — lets history filter by node without a join |
| `slotId` | ObjectId | ✓ | → `EnergyBookingSlots._id` |
| `slotStart` | date (UTC) | ✓ | **Copied at creation.** The 12-hour rule evaluates against this, so a later slot edit cannot silently move someone's cancellation deadline |
| `energyKWh` | double | ✓ | Requested amount |
| `status` | string enum | ✓ | `Pending` \| `Approved` \| `Rejected` \| `Cancelled` \| `Completed` |
| `createdAt` | date | ✓ | |
| `updatedAt` | date | ✓ | |
| `approvedBy` | ObjectId? | | Grid Operator `_id` |
| `approvedAt` | date? | | |
| `rejectionReason` | string? | | |
| `cancelledAt` | date? | | |
| `qrTokenHash` | string? | | **SHA-256 of the issued token. The plaintext token is never stored** |
| `qrIssuedAt` | date? | | |
| `qrExpiresAt` | date? | | `slotEnd + 2h` |
| `verifiedByOperatorId` | ObjectId? | | Evidence for the operator-verification criterion |
| `completedAt` | date? | | |

**Indexes**

| Index | Type | Why |
|---|---|---|
| `{prosumerNIC: 1, slotStart: -1}` | compound | `/reservations/mine`, booking history |
| `{slotId: 1, status: 1}` | compound | The active-reservation check on slot deletion |
| `{stationId: 1, status: 1}` | compound | Node deactivation block |
| `{status: 1, slotStart: 1}` | compound | Dashboard counts |
| `{qrTokenHash: 1}` | sparse | QR verification lookup |

---

## Sample data targets

Seeded by M2 on Day 3, refreshed and verified on Day 12. **Every reference must resolve** — that is what the "references between collections consistent" wording is marking.

| Collection | Minimum | Must include |
|---|---|---|
| `UsersDetail` | ≥ 2 Backoffice, ≥ 2 Grid Operators, ≥ 6 prosumers | A mix of `Pending`, `Active` and `Deactivated` prosumers, so the pending-activation screen has content on first load |
| `SolarStationInfo` | ≥ 5 nodes | Real lat/lng spread around Colombo, Negombo and Gampaha, so the map demo looks credible and `radiusKm` actually discriminates. At least one `Inactive` node |
| `EnergyBookingSlots` | ≥ 20 slots | Spread across the next 7 days. At least one slot already `Full`, at least one `Closed`, several `Open` with `reservedCount` between 1 and `totalCapacity - 1` |
| `EnergyReservation` | ≥ 15 | Every status represented. ≥ 2 `Completed` with `verifiedByOperatorId` and `completedAt` populated. At least one `Approved` with a live QR hash. At least one starting inside 12 hours, so the notice-period rejection can be demonstrated on command |

**Verification step before submission:** for every `EnergyReservation`, confirm `prosumerNIC` matches a real `UsersDetail.nic`, `slotId` matches a real slot, and that slot's `stationId` matches the reservation's `stationId`. A dangling reference is a visible defect in a 4-mark criterion.
