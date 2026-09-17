# Environment Configuration and Secrets

## Never committed — under any circumstances

- MongoDB Atlas connection string (contains username and password)
- JWT signing secret
- Google Maps API key
- `appsettings.Development.json`
- `.env`, `.env.local`
- `local.properties`
- Any keystore file

If a secret is committed by accident: **rotate it immediately.** Rotating is the fix. Deleting the commit is not — it stays in the history and in every clone. Atlas passwords and Maps keys are both one-click regenerable.

## Committed as templates

- `api/SmartSolar.Api/appsettings.Example.json`
- `web/.env.example`
- This file

## API configuration

`appsettings.json` holds structure with empty secret values. Real values live in:

| Environment | Mechanism |
|---|---|
| Local development | **.NET user secrets** — `dotnet user-secrets set "MongoSettings:ConnectionString" "..."`. Stored outside the repo entirely |
| IIS host | **Environment variables** — set on the application pool or machine |

Bound via `IOptions<MongoSettings>` and `IOptions<JwtSettings>`.

Viva answer: *"Secrets never enter source control — user secrets in development, environment variables on the IIS host."*

### Key names

| Key | Purpose |
|---|---|
| `MongoSettings:ConnectionString` | Atlas SRV string |
| `MongoSettings:DatabaseName` | `SmartSolarDb` |
| `JwtSettings:Secret` | ≥ 32 chars |
| `JwtSettings:Issuer` | `SmartSolarAPI` |
| `JwtSettings:Audience` | `SmartSolarClients` |
| `JwtSettings:ExpiryHours` | `8` |
| `QrSettings:TokenExpiryHours` | Hours after `slotEnd` that a QR stays valid — `2` |
| `Cors:AllowedOrigins` | Array. Dev origin plus the IIS web origin |

## MongoDB Atlas setup

| Item | Decision |
|---|---|
| Cluster | One shared M0 free cluster for the group |
| Database | `SmartSolarDb` |
| Users | **One database user per member**, so an accidental leak is revocable without breaking everyone. Naming: `sm_m1`, `sm_m2`, … |
| Network access | All four members' IPs plus the IIS machine. `0.0.0.0/0` is acceptable for the development window — **state in the report that this would be tightened in production**, which turns a shortcut into a discussed decision |
| Indexes | Created by M2 on Day 2, listed in `db-schema.md` |

## Web configuration

Vite exposes only `VITE_`-prefixed variables, and **they end up in the shipped bundle** — nothing secret goes there.

| Variable | Where | Example |
|---|---|---|
| `VITE_API_BASE_URL` | `.env.local`, gitignored | `http://localhost:5199/api` |

`.env.example` is committed with the key present and the value empty.

If the web app ever needs a Maps key, it must be a **separate key restricted by HTTP referrer** — never the Android key.

## Android configuration

| Value | Mechanism |
|---|---|
| `API_BASE_URL` | `buildConfigField` in `build.gradle`, read from gitignored `local.properties`. Each member points at their own machine or the IIS box without touching a committed file |
| `MAPS_API_KEY` | Read from `local.properties`, injected into the manifest via a placeholder |

**Maps key restrictions:** restrict to the package name `com.sliit.smartsolar` plus SHA-1 fingerprints. Because you are developing on emulators, **every member's debug keystore has a different SHA-1** — all four must be registered, plus the release SHA-1. A missing fingerprint produces a grey map with no error dialog.

**Cleartext traffic:** IIS on `http://192.168.x.x` is blocked by Android 9+ by default. You need a `network_security_config.xml` permitting cleartext for your LAN subnet, referenced from the manifest. Get this working on Day 3 — the failure mode is a silent connection error that looks like a server fault.

## Base URL per environment

| Environment | API base | Web origin |
|---|---|---|
| Local dev (each member) | `http://localhost:5199/api` | `http://localhost:5173` |
| Android emulator → host machine | `http://10.0.2.2:5199/api` | — |
| IIS (integration + demo) | `http://<iis-machine-ip>:<port>/api` | `http://<iis-machine-ip>:<web-port>` |

`10.0.2.2` is the emulator's alias for the host machine's loopback. `localhost` inside an emulator means the emulator itself.

**Discipline rule:** develop against localhost individually, but **every integration checkpoint runs against the IIS URL**. A group that only ever tests localhost discovers CORS, cleartext and firewall problems on Day 12, with no time left.
