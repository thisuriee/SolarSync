# Onboarding — First Hour After Cloning

Read `docs/api-contract.md`, `docs/db-schema.md`, `docs/auth.md` and `docs/response-format.md` before writing anything. They are the group contract. If your code disagrees with them, your code is wrong — or the contract needs a PR and the group's agreement.

---

## Prerequisites

| Tool | Version | Check |
|---|---|---|
| .NET SDK | **9** | `dotnet --version` |
| ASP.NET Core 9 Hosting Bundle | 9 | Required on the IIS machine; harmless elsewhere |
| Node.js + npm | LTS | `node -v` |
| VS Code | + C# Dev Kit extension | |
| Android Studio | Quail 2026.1.3 | Java, minSdk 24 |
| Git | any | `git --version` |
| MongoDB Compass | optional but recommended | For inspecting Atlas and demonstrating sample data at viva |

---

## Step 1 — Git identity (do this first, it cannot be fixed later)

    git config user.name "Your Name"
    git config user.email "the-email-on-your-github-account"

**The email must match your GitHub account** or your commits will not attribute to you. Individual contribution is a marked criterion and the commit graph is the evidence. If you pair-program on someone else's laptop, add `Co-authored-by: Name <email>` to the commit body.

---

## Step 2 — Branch

Never commit to `main` or `develop` directly.

    git checkout develop
    git pull
    git checkout -b feature/m<n>-<vertical>-<thing>

Examples: `feature/m1-identity-login-endpoint`, `feature/m3-reservations-cancel-rule`.

Commit format: `<type>(<scope>): <imperative summary>`
Types: `feat|fix|refactor|docs|chore|test` · Scopes: `api|web|mobile|db|deploy|docs`

`feat(api): enforce 12-hour notice on reservation cancel`

One commit never spans two members' verticals. Commit at least daily.

---

## Step 3 — MongoDB Atlas

1. Ask M2 for your **own** database user (`sm_m1`, `sm_m2`, …) — not a shared one.
2. Confirm your current IP is in the Atlas network-access list.
3. Connect with Compass to confirm you can see `SmartSolarDb` and the four collections with sample data.

---

## Step 4 — API secrets

From `api/SmartSolar.Api`:

    dotnet user-secrets init
    dotnet user-secrets set "MongoSettings:ConnectionString" "<your Atlas SRV string>"
    dotnet user-secrets set "MongoSettings:DatabaseName" "SmartSolarDb"
    dotnet user-secrets set "JwtSettings:Secret" "<the group's shared secret, 32+ chars>"
    dotnet user-secrets set "JwtSettings:Issuer" "SmartSolarAPI"
    dotnet user-secrets set "JwtSettings:Audience" "SmartSolarClients"
    dotnet user-secrets set "JwtSettings:ExpiryHours" "8"

User secrets live outside the repo. **Never put these in `appsettings.json`.** Key names are listed in `docs/environments.md`.

Run:

    dotnet run

Confirm `GET /api/ping` answers, and note the port — you need it for the other two clients.

---

## Step 5 — Web client

    cd web
    npm install
    cp .env.example .env.local

Set `VITE_API_BASE_URL` to your local API, e.g. `http://localhost:5199/api`. Then:

    npm run dev

`.env.local` is gitignored. Only `VITE_`-prefixed variables reach the browser, and they are visible in the shipped bundle — nothing secret goes there.

---

## Step 6 — Android

1. Open `mobile/` in Android Studio and let Gradle sync.
2. Create `local.properties` entries (gitignored):

       API_BASE_URL=http://10.0.2.2:5199/api
       MAPS_API_KEY=<your key>

   `10.0.2.2` is the emulator's alias for your host machine's loopback. `localhost` inside an emulator means the emulator itself.
3. Send your **debug keystore SHA-1** to whoever owns the Maps key — every member's debug keystore is different, and an unregistered fingerprint produces a grey map with no error dialog.
4. Create the AVD from a **Google Play** system image (not "Google APIs", not AOSP), or the map will not render.
5. In the AVD's advanced settings set the **back camera to `webcam0`** — this is how you will test QR scanning without a physical device.
6. Run the app and confirm it reaches `/api/ping`.

---

## Step 7 — Verify before you start building

- [ ] `dotnet run` starts and `/api/ping` answers
- [ ] Web dev server loads and can reach the API
- [ ] Emulator reaches the API
- [ ] Compass shows all four collections with sample data
- [ ] `git config user.email` matches your GitHub account
- [ ] You are on a `feature/` branch, not `develop`

---

## Non-negotiables — read these twice

**Architecture**
- No client ever opens a database connection. `/web` and `/mobile` have no MongoDB dependency, and you can prove it by opening `package.json` and `build.gradle`.
- No client ever decides whether a rule passes. The client displays the API's verdict and shows the API's message.
- Any rule you can bypass by editing a request in a REST client is a rule you implemented in the wrong place.
- SQLite holds the session and a read-through cache. Never authoritative. No offline queued writes.

**Code hygiene — code without these is not marked**
- Comment header block at the top of **every** `.cs` file: file name, author, date created, purpose.
- Inline comment at the start of **every** method — C#, Java and JS alike — saying what it does and which business rule it enforces.
- Any borrowed snippet carries its source URL in a comment directly above it. Unreferenced borrowed code is treated as plagiarism, not lost marks.
- Do these as you write, in the same commit. Auditing sixty files on Day 13 costs hours.

**Time**
- All rule evaluation uses `DateTime.UtcNow`. Never `DateTime.Now`. Sri Lanka is UTC+5:30 and mixing them turns "12 hours" into 6h30m.
- Clients convert to local time for display only.

**Shared code**
- `reservedCount`, the active-reservation predicate and the status transition guard are written **once**, by M3, and called by the others. Do not write your own copy — drift only shows up after a dozen operations, which means during a demo.

---

## Log your work

Append a line to `docs/CONTRIBUTIONS.md` after every working session: date, your name, branch, what you built. On Day 13 this becomes the report's Individual Contribution section with almost no editing, and it is backed by the commit graph.

---

## PR rule

One reviewer, not you, five minutes. You are checking two things:

1. Did business logic leak into a client?
2. Does every file have its header block and every method its opening comment?
