# Shared Plumbing — Build Guide

**How to use this:** eight pieces of shared infrastructure, built by the group, not handed over pre-written. Items 1–5 are a **two-hour group session on Day 2**: one person types, the other three watch and question. Items 6–8 land Day 3–4.

**Why the group session matters.** These files attract the most viva questions in the whole project — *"where is the role check enforced?"*, *"walk me through a request from button tap to Mongo write"*, *"show me where the token gets attached"*. Shared code that nobody can explain is the worst possible outcome. Everyone in the room, everyone asking questions, everyone able to answer.

This guide gives you **what to create, in what order, and the specific traps**. No method bodies — you write those.

---

## Build order

```
1 → 2 → 3 → 4   before anyone writes an endpoint
5               before Integration Checkpoint 1 (Day 4)
6 → 7           before anyone writes an Android screen
8               before anyone writes a web screen
```

---

## 1. Configuration binding and the Mongo client — M2 types

**Create in `Configuration/`:** `MongoSettings`, `JwtSettings`, `QrSettings` — plain classes whose property names match the keys in `appsettings.Example.json`.

**Create in `Repositories/`:** a `MongoContext` class exposing one `IMongoCollection<T>` property per collection.

**Suggested shape**

| Member | Purpose |
|---|---|
| `MongoContext(IOptions<MongoSettings> settings)` | Builds the client and resolves the database |
| `IMongoCollection<User> Users { get; }` | `UsersDetail` |
| `IMongoCollection<SolarStation> Stations { get; }` | `SolarStationInfo` |
| `IMongoCollection<BookingSlot> Slots { get; }` | `EnergyBookingSlots` |
| `IMongoCollection<Reservation> Reservations { get; }` | `EnergyReservation` |

**Steps**
1. Bind all three settings classes in `Program.cs` via `builder.Services.Configure<T>(...)`.
2. Register `IMongoClient` as a **singleton**.
3. Register `MongoContext` as a **singleton** too (it holds no per-request state).
4. Configure `_id` to serialise as a string, once, globally.
5. Create the indexes listed in `db-schema.md` — a one-off startup routine or a manual Compass step, your choice; document which.

**Traps**
- `MongoClient` is designed to be reused. Creating one per request exhausts the connection pool and produces intermittent timeouts under demo load.
- Register settings **before** anything that consumes them.
- GeoJSON coordinates are `[longitude, latitude]`. Reversed coordinates put every Sri Lankan node in the Indian Ocean, and `$near` will still happily return results.

**Everyone must be able to explain:** why the client is a singleton, and what `IOptions<T>` is doing.

**Done when:** a throwaway endpoint returns a count from one collection against Atlas.

---

## 2. `BusinessRuleException` and the global exception middleware — M4 types

**Create in `Helpers/`:** `BusinessRuleException`, deriving from `Exception`, carrying `Code`, `Title`, `Detail`, `StatusCode`.

**Create in `Middleware/`:** `ExceptionHandlingMiddleware` with the standard `InvokeAsync(HttpContext)` shape.

**Behaviour**

| Caught | Produces |
|---|---|
| `BusinessRuleException` | Its own status, the problem object with its `code` and `detail` |
| Anything else | `500`, generic title, **no stack trace in the body**, full exception written to the log with the `traceId` |

**Steps**
1. Write the exception class.
2. Write the middleware: try/catch around `next`, translate, write the problem JSON, set the content type.
3. Register it **first** in the pipeline in `Program.cs`, before routing, so it catches everything downstream.
4. Test with a throwaway endpoint that throws one.

**Traps**
- Register it first, or exceptions thrown in later middleware escape it.
- Do not also add `app.UseExceptionHandler` — you will get two handlers and confusing double-writes.
- Once the response has started, you cannot change the status code. Write the response in one place.

**Everyone must be able to explain:** why services throw and middleware formats, rather than every controller action returning `Conflict(new {...})`. This is a direct FAT-service demonstration.

**Done when:** a thrown `BusinessRuleException("TEST_CODE", ...)` produces exactly the JSON in `response-format.md`.

---

## 3. PATCH method-override middleware — M4 types

Android's `HttpURLConnection` rejects `PATCH`. The contract has nine PATCH routes. Android sends `POST` with `X-HTTP-Method-Override: PATCH`; this middleware rewrites the request method.

**Create in `Middleware/`:** `MethodOverrideMiddleware`.

**Behaviour**
1. If the request method is `POST` **and** the header is present **and** its value is `PATCH`, rewrite `context.Request.Method`.
2. Only honour it for **authenticated** requests.
3. Pass everything else through untouched.

**Steps**
1. Write the middleware.
2. Register it **after authentication, before routing**. Ordering is the whole point: after routing, the endpoint is already selected and the rewrite has no effect.
3. Verify with a REST client: POST to a PATCH route with the header, confirm it reaches the PATCH action; without the header, confirm 405.

**Traps**
- Ordering. This is the one thing that will silently not work.
- Ungated, this becomes a way to reach any PATCH route via POST from anywhere. Gate it on authentication.
- Accept only the exact value `PATCH` — do not build a general override for arbitrary verbs.

**Everyone must be able to explain:** why it must precede routing, and why it is gated.

**Done when:** an Android-shaped POST reaches a PATCH action, and the same request without the header does not.

---

## 4. JWT authentication and CORS — M1 types

**Create in `Helpers/`:** `JwtTokenGenerator` — one method taking the user's id, nic, full name and role, returning the token string and its expiry.

**Steps**
1. Add JWT bearer authentication in `Program.cs` with `TokenValidationParameters`: validate issuer, audience, lifetime, signing key; `ClockSkew = TimeSpan.Zero`.
2. **Set `RoleClaimType` to `ClaimTypes.Role`**, or emit the role claim using `ClaimTypes.Role` in the generator. Pick one and be consistent.
3. Add a named CORS policy reading `Cors:AllowedOrigins` from configuration.
4. `app.UseCors(...)` → `app.UseAuthentication()` → **method-override middleware** → `app.UseAuthorization()`.
5. Prove it: one endpoint with `[Authorize(Roles = "Backoffice")]`, one anonymous. Confirm 401 without a token, 403 with a Grid Operator token, 200 with a Backoffice token.

**Traps**
- **The `RoleClaimType` trap.** Emit a bare `"role"` claim without configuring this and `[Authorize(Roles = "...")]` never matches — every protected route returns 403 and nothing in the error tells you why. Budget for this being the confusing hour of Day 2.
- `UseCors` before `UseAuthentication`, and both before `UseAuthorization`.
- No `AllowAnyOrigin` combined with credentials — the browser rejects it.
- `ClockSkew` defaults to 5 minutes, which makes expiry behaviour confusing to test.

**Everyone must be able to explain:** what is in the token, why the role check happens before any service code runs, and what breaks without `RoleClaimType`.

**Done when:** the 401/403/200 matrix above behaves correctly, from the web client and from a REST tool.

---

## 5. Ping endpoint and first IIS publish — M4 types

**Create in `Controllers/`:** a minimal `PingController` — one anonymous `GET /api/ping` returning a status string, a UTC timestamp, and whether the Mongo connection responded.

Then follow `deployment-iis.md` end to end.

**Done when:** `GET /api/ping` answers from the IIS URL, in a browser on a second machine **and** from the Android emulator. Both. Until the emulator can reach it, cleartext and firewall problems are still ahead of you.

---

## 6. Android networking layer — M4 types

**Create in `network/`:**

| Class | Purpose |
|---|---|
| `ApiClient` | Static helpers: `get`, `post`, `put`, `delete`, and `patch` (POST + override header). Each takes a path and optional JSON body, returns `ApiResult` |
| `ApiResult` | `int statusCode`, `String body`, `boolean isSuccess`. **No parsing, no business decisions** |
| `ApiCallback<T>` | Interface: `onSuccess(T data)` / `onError(String code, String detail)` — so screens never touch status codes |
| `ErrorParser` | Takes an error body, returns `code` and `detail` from the problem JSON |
| `DateUtils` | ISO-8601 UTC parse and local-time format |

**`ApiClient` per-call sequence**
1. Open `HttpURLConnection` on `BuildConfig.API_BASE_URL + path`.
2. Set `Content-Type: application/json`, `Accept: application/json`.
3. Read the token from the `session` table; set `Authorization: Bearer ...` if present.
4. `setConnectTimeout(10000)`, `setReadTimeout(15000)`.
5. For POST/PUT: `setDoOutput(true)`, write the body via a `BufferedWriter` over `getOutputStream()`.
6. Read `getResponseCode()`.
7. **On 2xx read `getInputStream()`; on 4xx/5xx read `getErrorStream()`.**
8. Close the connection in a `finally`.
9. Return `ApiResult`.

**Threading:** one `ExecutorService` (fixed pool of 4) plus a `Handler(Looper.getMainLooper())` to post results back to the UI thread. **Not `AsyncTask`** — deprecated in API 30.

**Traps**
- **The `getErrorStream()` trap.** `getInputStream()` throws on a 4xx. Catch it and you lose the API's error message entirely — every business rule rejection becomes a generic failure, and the marker never sees your server-side messages surfacing in the client. This is the single most common bug in hand-rolled Android HTTP.
- **The date trap.** `SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'")` **must** have `setTimeZone(TimeZone.getTimeZone("UTC"))` set explicitly. Omit it and every timestamp shifts by 5:30, making your 12-hour rule look broken in the UI while being correct on the server.
- Use `optString`, `optInt`, `optDouble`, `isNull` everywhere in parsers — never the throwing getters. An absent field is not the same as a null field.
- Never touch views from the executor thread.

**Everyone must be able to explain:** the error-stream branch, the UTC timezone, and how the token is attached.

**Per-vertical parsing:** each member writes their own parser file in `parsers/` — `UserParser`, `NodeParser`, `ReservationParser`, `FulfilmentParser`. One file per member, so no shared editing and no merge conflicts.

**Done when:** a throwaway Activity calls `/api/ping` through `ApiClient` and shows the result, and a deliberate 401 surfaces through `onError` with the real `code`.

---

## 7. `DatabaseHelper` and SQLite tables — M4 types

**Create in `database/`:** `DatabaseHelper extends SQLiteOpenHelper`, plus one DAO per table.

| Table | Columns | Owner of the DAO |
|---|---|---|
| `session` | `id` (always 1), `user_id`, `username`, `nic`, `full_name`, `role`, `token`, `issued_at`, `expires_at` | M1 |
| `cached_nodes` | `station_id` PK, `station_name`, `city`, `lat`, `lng`, `capacity_kwh`, `total_slots`, `status`, `cached_at` | M2 |
| `cached_bookings` | `reservation_id` PK, `station_id`, `station_name`, `slot_start`, `slot_end`, `energy_kwh`, `status`, `cached_at` | M4 |
| `sync_meta` | `entity` PK, `last_synced_at` | M4 |

**Steps**
1. Write `onCreate` with the four `CREATE TABLE` statements.
2. Write `onUpgrade` — drop and recreate is acceptable for this project; say so in a comment and be ready to justify it.
3. Constants for table and column names in one place.
4. DAOs with plain insert/replace/query/clear methods.
5. A single `clearAll()` called on logout.

**The four cache rules — these are what protect the 12-mark integration criterion**
1. **Read-through only.** Every screen calls the API first, overwrites the cache on success, renders from the response. The cache renders only when the call fails.
2. **No local writes of user intent.** A booking made offline is not queued. The create button requires connectivity. The moment you queue local mutations, you have business logic in the client.
3. **Cached counts are labelled stale** — "Last updated 14:32". Live counts come from `/dashboard/prosumer`.
4. **Cleared on logout**, including the session row.

**Do not cache `/slots/available`.** Slot capacity changes by the second; a stale slot list produces booking failures that look like your bugs.

**Everyone must be able to explain:** why there is no password column, and why the cache is never authoritative.

**Done when:** login persists across an app restart, and the node list renders from cache with aeroplane mode on.

---

## 8. Web app shell — M1 types

**Steps**
1. Import Bootstrap CSS once, in `main.jsx`.
2. `services/apiClient.js` — one axios instance with `baseURL` from `import.meta.env.VITE_API_BASE_URL`.
3. **Request interceptor** — attach the bearer token from `sessionStorage`.
4. **Response interceptor** — on 401, clear the session and redirect to `/login`; on any error, normalise the problem object into `{code, detail}` before rejecting.
5. `context/AuthContext.jsx` — holds user and role, exposes login/logout, restores session on mount via `/auth/me`.
6. `layouts/AppLayout.jsx` — navbar, sidebar, content outlet. **Navigation items filtered by role**, so a Grid Operator never sees Backoffice-only links.
7. Route setup with a `ProtectedRoute` wrapper taking an allowed-roles list.
8. `utils/errorMessages.js` — maps a `code` to a display message where a friendlier phrasing than `detail` is wanted. Default to showing `detail`.

**Traps**
- Hiding a nav link is **not** authorisation. The API must reject the call too — hidden links are convenience, `[Authorize]` is the rule. Expect this exact question at viva.
- Only `VITE_`-prefixed variables reach the browser, and they are visible in the bundle. Nothing secret.
- Restore the session on mount, or a refresh logs the user out.

**Everyone must be able to explain:** why 401 clears the session, and why hiding a menu item is not a security control.

**Done when:** login routes Backoffice and Grid Operator to different home pages, refresh preserves the session, and an expired token bounces to login.

---

## Group session agenda — Day 2, two hours

| Time | Item | Typed by |
|---|---|---|
| 0:00–0:25 | 1. Config binding + Mongo client | M2 |
| 0:25–0:50 | 2. `BusinessRuleException` + exception middleware | M4 |
| 0:50–1:05 | 3. Method-override middleware | M4 |
| 1:05–1:40 | 4. JWT + CORS + the 401/403/200 proof | M1 |
| 1:40–2:00 | 5. Ping endpoint, commit, push to `develop` | M4 |

Everyone pulls at the end and confirms the API builds and `/api/ping` answers locally. Then M4 continues to the first IIS publish on Day 3.

**Each member adds their own file header block and method comments to whatever they typed, in the same commit.** Doing this from the first file is far cheaper than auditing sixty files on Day 13 — and code without them is not marked.
