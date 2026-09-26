# Response Format, Error Codes and Status Conventions

## Approach

**Raw payload on success, problem object on failure.** No hand-rolled success envelope.

Viva defence: *"We did not wrap successful responses because ASP.NET Core already produces RFC 7807 problem details and `[ApiController]` already produces a standard validation response. Adding an envelope would have meant re-implementing both, and would have forced every Android parser and every React call through an extra unwrapping layer."*

## Success shapes

| Case | Shape |
|---|---|
| Single resource | The object. `GET /api/nodes/{id}` → `{ "id": "...", "stationName": "..." }` |
| Collection | A bare array. `GET /api/nodes` → `[ {...}, {...} ]` |
| Paged collection | `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 137, "totalPages": 7 }` |
| Created | `201` + the created resource + a `Location` header |
| Deleted | `204 No Content`, empty body. Only `DELETE /api/slots/{id}` |

Paged routes: `/prosumers`, `/reservations`, `/reservations/history`.

## Error shape

One object, every failure, every vertical.

| Field | Meaning |
|---|---|
| `type` | URI identifying the error class |
| `title` | Short human-readable summary |
| `status` | HTTP status code, repeated in the body |
| `code` | **Machine-readable. This is what clients switch on.** Never parse `detail`, never match on message text |
| `detail` | The message the UI shows the user |
| `traceId` | Correlates with the server log |
| `errors` | Field-level validation map. Present only on `400` |

Example:

    {
      "type": "https://smartsolar/errors/reservation-notice-period",
      "title": "Cancellation window has passed",
      "status": 409,
      "code": "RESERVATION_NOTICE_PERIOD",
      "detail": "Reservations must be cancelled at least 12 hours before the slot start time. This slot starts in 3 hours.",
      "traceId": "00-a1b2c3...",
      "errors": null
    }

## Business error code registry

Every code is owned by one member and added here as it is implemented. **This registry is your evidence that rules are enforced server-side** — at viva you can show a client rendering a message it did not author.

| Code | HTTP | Owner | Meaning |
|---|---|---|---|
| `AUTH_INVALID_CREDENTIALS` | 401 | M1 | Bad username or password |
| `AUTH_ACCOUNT_PENDING` | 403 | M1 | Registered but not yet activated by Backoffice |
| `AUTH_ACCOUNT_DEACTIVATED` | 403 | M1 | Needs Backoffice reactivation |
| `AUTH_FORBIDDEN_ROLE` | 403 | M1 | Role lacks permission, or touching another user's data |
| `AUTH_INVALID_TOKEN` | 401 | M1 | Token valid but missing a required claim, or its user no longer exists |
| `USER_NIC_INVALID` | 400 | M1 | NIC is not 9 digits + V/X (old) or 12 digits (new) |
| `USER_NIC_EXISTS` | 409 | M1 | Duplicate NIC at registration |
| `USER_USERNAME_EXISTS` | 409 | M1 | Username already taken (compared case-insensitively) |
| `USER_EMAIL_EXISTS` | 409 | M1 | Email already registered (compared case-insensitively) |
| `USER_LAST_BACKOFFICE` | 409 | M1 | Cannot demote or disable the final Backoffice account |
| `USER_HAS_ACTIVE_RESERVATIONS` | 409 | M1 | Prosumer deactivation blocked |
| `NODE_HAS_ACTIVE_RESERVATIONS` | 409 | M2 | Node deactivation blocked — include the count in `detail` |
| `NODE_CAPACITY_CONFLICT` | 409 | M2 | Battery slot count would drop below what is already reserved |
| `SLOT_OVERLAP` | 409 | M2 | Overlapping slot window on the same node |
| `SLOT_HAS_RESERVATIONS` | 409 | M2 | Slot deletion blocked |
| `RESERVATION_OUTSIDE_WINDOW` | 422 | M3 | Slot start is more than 7 days from now |
| `RESERVATION_NOTICE_PERIOD` | 409 | M3 | Update or cancel attempted inside 12 hours |
| `RESERVATION_SLOT_FULL` | 409 | M3 | No remaining capacity on the slot |
| `RESERVATION_DUPLICATE` | 409 | M3 | Same NIC already holds an active booking on this slot |
| `RESERVATION_INVALID_STATE` | 409 | M3 | Illegal status transition |
| `QR_INVALID` | 400 | M4 | Token hash does not match any reservation |
| `QR_EXPIRED` | 410 | M4 | Past `qrExpiresAt` |
| `QR_ALREADY_USED` | 409 | M4 | Reservation is no longer `Approved` |

## HTTP status conventions

| Code | When | Note |
|---|---|---|
| 200 | Success with a body | |
| 201 | Resource created | Include `Location` |
| 204 | Deleted, no body | Only slot deletion |
| 400 | Malformed request, model validation failure | `[ApiController]` produces this automatically — populate `errors` |
| 401 | Missing, expired or invalid token | Both clients: clear session, route to login |
| 403 | Authenticated but wrong role, or touching another user's data | **Never 404 to hide it.** The visible 403 is the demonstration |
| 404 | Resource genuinely does not exist | |
| 409 | Business rule conflicts with current state | **Most of your rules live here** — 12-hour notice, node block, slot full, duplicate |
| 410 | Expired QR token | Distinguishes "was valid, now gone" from "never valid" |
| 422 | Well-formed but semantically impossible — the 7-day breach | Distinguishes "you asked for something out of bounds" from "state conflict" |
| 500 | Unhandled | Global handler returns the problem shape and logs `traceId`. **Never leak a stack trace** |

## Implementation rule — all four members

These problem objects are produced by a **single global exception middleware** reacting to a shared `BusinessRuleException(code, title, detail, status)`. Not by `return Conflict(new {...})` scattered through forty controller actions.

Your **service layer throws**; the **middleware formats**. This is what keeps controllers thin, and it is a direct demonstration of the FAT-service pattern when the examiner asks where your logic lives.

## Client handling

| Client | Requirement |
|---|---|
| Web | Axios response interceptor parses the problem object into `{code, detail}`. Screens display `detail`. 401 clears the session |
| Android | `ErrorParser` reads the error body via `getErrorStream()` and returns `{code, detail}` through `ApiCallback.onError`. Screens display `detail` |

Neither client composes its own message for a business rule failure. If the text on screen was written in the client, the rule is not visibly server-side.
