# Authentication & Authorisation

## Scheme

**JWT bearer, HS256, single access token, 8-hour expiry, no refresh token.**

Viva defence: *"The API is stateless and serves two clients on different origins. A signed bearer token carries the role claim, so authorisation is enforced by the framework at the controller before any service code runs. We accepted that tokens cannot be revoked mid-session because expiry is short and the alternative — refresh-token rotation — adds failure modes without adding marks."*

| Parameter | Value |
|---|---|
| Algorithm | HS256 (symmetric) |
| Secret | ≥ 32 characters, from configuration. **Never committed** |
| Issuer | `SmartSolarAPI` |
| Audience | `SmartSolarClients` |
| Expiry | 8 hours |
| Clock skew | Set to `TimeSpan.Zero` — the default 5-minute tolerance makes expiry testing confusing |

## JWT claims

| Claim | Value | Used for |
|---|---|---|
| `sub` | Mongo `_id` of the user | Audit fields: `activatedBy`, `approvedBy`, `verifiedByOperatorId` |
| `nic` | NIC — prosumers only | `/reservations/mine`, profile scoping. **The API never reads a NIC from a request body for a prosumer-owned action** |
| `role` | `Backoffice` \| `GridOperator` \| `Prosumer` | `[Authorize(Roles = "...")]`, client home routing |
| `name` | Display name | UI header |
| `exp` / `iss` / `aud` | 8h / `SmartSolarAPI` / `SmartSolarClients` | Validation |

**Critical implementation detail:** emit the role using `ClaimTypes.Role`, or set `RoleClaimType` explicitly in `TokenValidationParameters`. If you emit a bare `"role"` claim without this, `[Authorize(Roles = "Backoffice")]` silently never matches and every protected route returns 403. This costs groups an hour every year.

## Password handling

`PasswordHasher<T>` from `Microsoft.AspNetCore.Identity` — PBKDF2, no extra package, explainable at viva. Group-wide; nobody uses a different hasher.

`passwordHash` is excluded at the **DTO layer**, not by attribute alone. No response anywhere in the system contains it.

## Token storage — Web

| Aspect | Decision |
|---|---|
| Where | `sessionStorage` |
| Attached by | Axios **request interceptor** — one place, all four members' calls |
| On 401 | Axios **response interceptor** clears storage and redirects to `/login` |
| Trade-off to state at viva | `httpOnly` cookies would resist XSS better, but cross-origin cookies between two IIS sites require `SameSite=None` plus HTTPS, which on a local self-signed certificate is disproportionate. No marks ride on it |

## Token storage — Android

Single-row `session` table in SQLite. See `docs/environments.md` and the plumbing guide for the table shape.

**The password is never stored — not plaintext, not hashed.** Only the token.

Viva answer if asked why not `EncryptedSharedPreferences`: the rubric requires SQLite local persistence of login details, and the stored value is a short-lived, scoped bearer token rather than a credential.

## Anonymous endpoints

Exactly three. Everything else requires a bearer token.

- `POST /api/auth/login`
- `POST /api/auth/register-prosumer`
- `GET /api/ping` (health check, dev only)

## Authorisation rules

| Rule | Where enforced |
|---|---|
| Role gate | `[Authorize(Roles = "...")]` on the controller action |
| Backoffice-only reactivation | Attribute **and** a re-check inside `UserService.Activate`. Belt and braces: the rule survives someone removing the attribute during a refactor, and it gives you something to point at in the service layer when asked where business logic lives |
| Own-data-only | Every service compares the route/body identifier against the token `nic` or `sub`. Returns `403 AUTH_FORBIDDEN_ROLE`, **never 404** — you need the visible 403 to demonstrate the rule at viva |

## CORS

The API names the web client's origin explicitly in a policy. No `AllowAnyOrigin` together with credentials. The allowed origins list lives in configuration (`Cors:AllowedOrigins`) so the IIS origin can differ from the dev origin without a code change.
