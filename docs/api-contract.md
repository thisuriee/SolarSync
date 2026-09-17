# API Contract — Smart Solar Microgrid Trading System

**Status:** Locked at Day 2 group contract sign-off. Any change requires a message in the group chat, an update to this file in the same PR, and acknowledgement from every member whose client consumes the route.

**Base:** all routes are prefixed `/api`. All routes require `Authorization: Bearer <token>` unless the Role column says *Anonymous*.

---

## 1. Conventions

| Rule | Detail |
|---|---|
| Nouns, not verbs | Routes name resources. State changes use `PATCH /resource/{id}/action` (`/cancel`, `/approve`, `/activate`) |
| Verb semantics | `PUT` = full resource update. `PATCH` = status or single-field transition |
| **Identity never travels in the payload** | `nic`, `userId` and `role` are read from the JWT. A prosumer-owned action that accepts a NIC in the body is a security hole |
| Dates on the wire | ISO-8601 UTC with a trailing `Z`, e.g. `2026-09-24T09:30:00Z`. Clients convert for display only |
| `_id` serialisation | Serialised as a plain string, never `{"$oid": "..."}`. Configured once globally in the API |
| Success shape | The resource itself. Collections return a bare array. Paged collections return the paging envelope (see `response-format.md`) |
| Failure shape | The problem object with a machine-readable `code`. See `response-format.md` |
| PATCH from Android | `HttpURLConnection` cannot send PATCH. Android sends `POST` with header `X-HTTP-Method-Override: PATCH`. API middleware translates it **before routing**, for authenticated requests only |

### Role strings

`Backoffice` · `GridOperator` · `Prosumer` — exact casing, used in the JWT `role` claim and in `[Authorize(Roles = "...")]`.

### Who may do what with a reservation

| Action | Prosumer | Grid Operator | Backoffice |
|---|---|---|---|
| Create | ✅ own | ❌ | ✅ on behalf (`prosumerNIC` in body) |
| Update | ✅ own | ❌ | ✅ on behalf |
| Cancel | ✅ own | ✅ operator-assisted | ✅ on behalf |
| Approve / Reject | ❌ | ✅ **only** | ❌ |
| Verify QR / Complete | ❌ | ✅ **only** | ❌ |

---

## 2. Auth & Users — Member 1

| Method | Route | Request | Response | Role | Business rules enforced |
|---|---|---|---|---|---|
| POST | `/auth/login` | `{username, password}` | `{token, expiresAt, user:{id, nic?, fullName, role}}` | Anonymous | Verify password hash. Reject `Pending` → `AUTH_ACCOUNT_PENDING`; reject `Deactivated` → `AUTH_ACCOUNT_DEACTIVATED`. Role claim drives client home routing |
| POST | `/auth/register-prosumer` | `{nic, fullName, email, phone, address, username, password}` | `201` `{id, nic, status:"Pending"}` | Anonymous | NIC format + uniqueness (`USER_NIC_EXISTS`); username and email uniqueness; **status forced to `Pending` server-side** — the client cannot set it; `role` forced to `Prosumer` |
| GET | `/auth/me` | — | Current user profile | Any | Resolved from token. Lets both clients restore a session without re-login |
| GET | `/webusers` | `?role=&status=` | Array | Backoffice | |
| POST | `/webusers` | `{username, fullName, email, phone, password, role}` | `201` resource | Backoffice | `role` ∈ {`Backoffice`, `GridOperator`} only — **`Prosumer` rejected here**; username/email unique |
| PUT | `/webusers/{id}` | Full object | Updated | Backoffice | Cannot demote or disable the last active Backoffice account (`USER_LAST_BACKOFFICE`) |
| PATCH | `/webusers/{id}/status` | `{status}` | Updated | Backoffice | Same last-Backoffice guard |
| GET | `/prosumers` | `?status=&q=&page=&pageSize=` | Paged | Backoffice, GridOperator | |
| GET | `/prosumers/pending` | — | Array of `Pending` | **Backoffice** | Drives the pending-activations screen |
| GET | `/prosumers/{nic}` | — | Profile | Any | Prosumer: token `nic` must equal `{nic}`, else `403 AUTH_FORBIDDEN_ROLE` |
| PUT | `/prosumers/{nic}` | `{fullName, email, phone, address}` | Updated | Any | Prosumer edits own only. **`nic`, `role`, `status` are not accepted in the body** — silently ignored, never applied |
| PATCH | `/prosumers/{nic}/request-deactivation` | — | Updated | Prosumer | Own account only. Blocked if active reservations exist (`USER_HAS_ACTIVE_RESERVATIONS`). Sets `status=Deactivated`, `deactivationRequestedAt` |
| PATCH | `/prosumers/{nic}/activate` | — | Updated | **Backoffice** | **Grid Operator receives `403 AUTH_FORBIDDEN_ROLE`.** Sets `status=Active`, `activatedBy`, `activatedAt`. Re-checked inside the service, not only by the attribute |
| PATCH | `/prosumers/{nic}/deactivate` | `{reason?}` | Updated | **Backoffice** | Blocked if active reservations exist |

---

## 3. Microgrid Nodes & Slots — Member 2

| Method | Route | Request | Response | Role | Business rules enforced |
|---|---|---|---|---|---|
| GET | `/nodes` | `?status=&q=` | Array | Any | Prosumer sees `Active` only — **filtered in the API, not the client** |
| GET | `/nodes/{id}` | — | Node + slot summary + active reservation count | Any | |
| POST | `/nodes` | `{stationName, addressLine, city, lat, lng, capacityKWh, totalBatterySlots, operatingSchedule[]}` | `201` | Backoffice | `lat` ∈ [-90,90], `lng` ∈ [-180,180]; `capacityKWh` > 0; `totalBatterySlots` ≥ 1. API maps lat/lng into GeoJSON `[lng, lat]` — **longitude first** |
| PUT | `/nodes/{id}` | Full object | Updated | Backoffice | `totalBatterySlots` cannot drop below the largest `totalCapacity` of any existing slot (`NODE_CAPACITY_CONFLICT`) |
| PATCH | `/nodes/{id}/deactivate` | — | Updated | Backoffice | **Blocked while active reservations exist** → `409 NODE_HAS_ACTIVE_RESERVATIONS`, with the count in `detail`. Uses M3's shared `CountActiveForStation` |
| PATCH | `/nodes/{id}/activate` | — | Updated | Backoffice | |
| GET | `/nodes/nearby` | `?lat=&lng=&radiusKm=` | Array + `distanceKm` per item | Any | **`$near` on the 2dsphere index — distance computed server-side.** `Active` nodes only. Default `radiusKm` = 25 |
| GET | `/nodes/{id}/slots` | `?from=&to=` | Array | Any | |
| POST | `/nodes/{id}/slots` | `{slotStart, slotEnd, totalCapacity, energyPerSlotKWh}` | `201` | Backoffice, GridOperator | No overlap with an existing slot on the same node (`SLOT_OVERLAP`); `totalCapacity` ≤ node's `totalBatterySlots`; `slotEnd` > `slotStart`; `reservedCount` initialised to 0 **server-side**; `status` set to `Open` |
| PUT | `/slots/{id}` | Slot object | Updated | Backoffice, GridOperator | `totalCapacity` ≥ current `reservedCount`; overlap re-check. **`reservedCount` and `status` in the body are ignored** |
| DELETE | `/slots/{id}` | — | `204` | Backoffice | `409 SLOT_HAS_RESERVATIONS` if any active reservation references it |
| GET | `/slots/available` | `?nodeId=&date=` | Array | Prosumer, staff | `reservedCount < totalCapacity`; `status = Open`; **`slotStart` within 7 days of now** — visible here and re-checked on create |

---

## 4. Reservations — Member 3

| Method | Route | Request | Response | Role | Business rules enforced |
|---|---|---|---|---|---|
| POST | `/reservations` | `{slotId, energyKWh}` — Backoffice may add `prosumerNIC` | `201` full reservation | Prosumer (own), **Backoffice** (on behalf) | **R1: `slotStart` ≤ `utcNow + 7 days`** else `422 RESERVATION_OUTSIDE_WINDOW`. Prosumer `Active`; node `Active`; slot has capacity; no duplicate active booking of the same slot by the same NIC (`RESERVATION_DUPLICATE`). **R6: atomic `FindOneAndUpdate` claims capacity and flips slot `status` in one operation.** Status → `Pending` |
| GET | `/reservations/{id}` | — | Detail | Any | Prosumer sees own only |
| GET | `/reservations/mine` | `?status=` | Array | Prosumer | **NIC from the token — this route has no `nic` parameter** |
| GET | `/reservations` | `?status=&nodeId=&nic=&from=&to=&page=&pageSize=` | Paged | Backoffice, GridOperator | |
| PUT | `/reservations/{id}` | `{slotId, energyKWh}` | Updated | Prosumer (own), **Backoffice** | **R2: ≥12h before `slotStart`** else `409 RESERVATION_NOTICE_PERIOD`. Status ∈ {`Pending`, `Approved`}. New slot must satisfy R1 and have capacity. Releases the old slot and claims the new one |
| PATCH | `/reservations/{id}/cancel` | — | Updated | Prosumer (own), GridOperator, Backoffice | **R2: ≥12h before `slotStart`.** Status ∈ {`Pending`, `Approved`}. Decrements `reservedCount`, reopens the slot, sets `cancelledAt` |
| PATCH | `/reservations/{id}/approve` | — | Updated | **GridOperator only** | From `Pending` only (`RESERVATION_INVALID_STATE`). Sets `approvedBy`, `approvedAt`. **Triggers M4's QR issuance** |
| PATCH | `/reservations/{id}/reject` | `{reason}` | Updated | **GridOperator only** | From `Pending` only. Releases capacity. Sets `rejectionReason` |

### Reservation status transitions

| From | Legal to |
|---|---|
| `Pending` | `Approved`, `Rejected`, `Cancelled` |
| `Approved` | `Cancelled`, `Completed` |
| `Rejected` | — terminal |
| `Cancelled` | — terminal |
| `Completed` | — terminal |

Enforced by one guard method in `ReservationService`. Any illegal transition → `409 RESERVATION_INVALID_STATE`.

---

## 5. Fulfilment, Dashboards & History — Member 4

| Method | Route | Request | Response | Role | Business rules enforced |
|---|---|---|---|---|---|
| GET | `/reservations/{id}/qr` | — | `{token, expiresAt}` | Prosumer (own) | Status must be `Approved`. Token is 32 random bytes, Base64Url. Server stores `SHA256(token)` as `qrTokenHash`, plus `qrIssuedAt` and `qrExpiresAt = slotEnd + 2h`. **Plaintext token is never persisted** |
| POST | `/fulfilment/verify-qr` | `{token}` | `{reservation, prosumer, station, verificationId}` | **GridOperator** | Hash the submitted token, look up by `qrTokenHash`. `400 QR_INVALID` if no match. `410 QR_EXPIRED` past `qrExpiresAt`. `409 QR_ALREADY_USED` if status ≠ `Approved`. Returns a `verificationId` valid ~5 minutes |
| PATCH | `/reservations/{id}/complete` | `{verificationId}` | Updated | **GridOperator** | `verificationId` must be valid and unexpired — this is what prevents finalising without scanning. From `Approved` only. Sets `verifiedByOperatorId`, `completedAt`, status → `Completed`. Single-use by virtue of the status check |
| GET | `/reservations/history` | `?nic=&nodeId=&status=&from=&to=&q=&page=&pageSize=` | Paged | Any | Prosumer is scoped to the token NIC **regardless of the `nic` parameter**. **Filtering happens in the Mongo query, never in memory** |
| GET | `/dashboard/prosumer` | — | `{activeCount, pendingCount, approvedFutureCount, nextBooking}` | Prosumer | All counts computed in the API from the token NIC. "Future" = `slotStart > utcNow`. `activeCount` = status ∈ {`Pending`,`Approved`} and `slotStart > utcNow` |
| GET | `/dashboard/operator` | `?nodeId=` | `{pendingReservations[], pendingCount, approvedFutureCount, completedToday}` | GridOperator, Backoffice | Counts computed via Mongo aggregation. **Never counted client-side, never hardcoded** |

---

## 6. Cross-vertical ownership

Three pieces of logic are shared. They are written **once**, by Member 3, and called by the others. Writing your own copy will produce drift that only appears after a dozen operations.

| Shared piece | Definition | Owner | Callers |
|---|---|---|---|
| Active-reservation predicate | `status ∈ {Pending, Approved} AND slotStart > utcNow` | M3 | M2 (node deactivate, slot delete), M1 (prosumer deactivate) |
| `reservedCount` mutation | Only `ReservationService` writes it, always together with the slot `status` flip, in one atomic operation | M3 | M2 must call, never write directly |
| Status transition guard | The table in §4 | M3 | M4 (approve → complete path) |

Exposed as: `CountActiveForStation(stationId)`, `CountActiveForSlot(slotId)`, `CountActiveForProsumer(nic)`.
