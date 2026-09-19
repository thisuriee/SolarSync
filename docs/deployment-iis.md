# IIS Deployment — Smart Solar Microgrid Trading System

**Owner:** Member 4 · **Backup:** Member 2 · **First run: Day 3, before any feature exists.**

Publishing a hello-world API on Day 3 turns a submission-week catastrophe into a Day 3 annoyance. It also means these notes — written as you go — become the report's deployment section, which is worth marks on its own.

Two IIS sites are published: the API and the web client, on different ports.

---

## Prerequisites (on the IIS machine)

- [ ] Windows with IIS enabled — Control Panel → Turn Windows features on or off → Internet Information Services, including **IIS Management Console**
- [ ] **ASP.NET Core 9 Hosting Bundle** installed — *not* the runtime alone. A version mismatch here is the usual cause of HTTP 500.30
- [ ] `iisreset` run after installing the bundle, so the ANCM module registers
- [ ] Inbound firewall rules for both chosen ports (API and web)
- [ ] The machine's LAN IP noted — this is what the emulator and the other members will target
- [ ] Atlas network-access list includes this machine's public IP

---

## Publish the API

- [ ] `dotnet publish -c Release -o <publish-folder>` from `api/SmartSolar.Api`
- [ ] Copy the publish output to a folder outside the repo, e.g. `C:\inetpub\smartsolar-api`
- [ ] IIS Manager → Add Website: site name, physical path = publish folder, port = API port
- [ ] Application Pool → **.NET CLR version = No Managed Code**. ASP.NET Core runs out of process; leaving this on a CLR version is the second most common failure
- [ ] Application Pool → Identity: `ApplicationPoolIdentity` is fine; grant it read access to the publish folder
- [ ] Set secrets as environment variables (see `environments.md`) — Mongo connection string, JWT secret. **Do not copy `appsettings.Development.json` to the server**
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Browse `http://localhost:<api-port>/api/ping` on the server itself
- [ ] Browse the same from another machine using the LAN IP

---

## Publish the web client

- [ ] Set `VITE_API_BASE_URL` to the **IIS API URL**, not localhost, then `npm run build`
- [ ] Copy `dist/` to e.g. `C:\inetpub\smartsolar-web`
- [ ] IIS Manager → Add Website: physical path = that folder, port = web port
- [ ] Install the **URL Rewrite** module and add a rewrite rule sending all non-file requests to `/index.html` — without it, refreshing on any route other than `/` returns 404, because the SPA owns routing
- [ ] Add the web origin to `Cors:AllowedOrigins` on the API and restart the API site

---

## Verify from both clients

- [ ] Browser on another machine reaches the web site and can log in
- [ ] Network tab shows requests going to the IIS API URL, not localhost
- [ ] Android emulator reaches `http://<lan-ip>:<api-port>/api/ping`
- [ ] Android app logs in and loads data against the IIS API
- [ ] A business rule rejection (try cancelling inside 12 hours) shows the API's message in both clients

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| **500.19** | Bad `web.config`, or the ASP.NET Core Module is not installed. Reinstall the Hosting Bundle, then `iisreset` |
| **500.30 — start failure** | App crashed on boot. Almost always a missing configuration value (Mongo connection string, JWT secret). Enable `stdoutLogEnabled` in `web.config` temporarily and read the log |
| **502.5** | Process failed to start — wrong .NET runtime version on the machine |
| **403.14 on the web site** | Directory browsing; `index.html` missing or wrong physical path |
| **404 on refresh of a SPA route** | URL Rewrite rule missing |
| **CORS error in the browser console** | Web origin not in `Cors:AllowedOrigins`, or the API site was not restarted after the change |
| **Emulator cannot connect** | Missing `network_security_config.xml` for cleartext, wrong IP (`localhost` inside an emulator is the emulator), or the firewall rule is missing |
| **Mongo timeout on the server but not locally** | Atlas network-access list is missing the IIS machine's public IP |
| **`querySrv ECONNREFUSED` / DNS error on the Mongo string** | The network's resolver blocks SRV lookups. Use the non-SRV `mongodb://host1,host2,host3/?replicaSet=…` form (see notes below) |
| **Emulator request hangs with no error** | Stale LAN IP after a DHCP change, and `HttpURLConnection` has no default timeout. Re-check the IP; set connect/read timeouts in the networking layer |
| **Env vars vanished after a republish** | They were saved to `web.config`. Set them in `ApplicationHost.config` via Configuration Editor instead |

---

## Reproducibility check (Day 12)

Member 2 follows these notes on a fresh IIS site, without help from Member 4. Anything that needed a verbal explanation is missing from this document — add it. The marking scheme wants *"hosting and deployment steps reproducible"*, and this is how you evidence it.

---

## Notes from the first publish (Day 3 — 2026-09-19, Member 4's machine)

Everything below was hit while getting `GET /api/ping` to answer from IIS, a phone and the Android emulator. Each item is either a surprise the checklist above did not warn about, or a checklist item whose failure mode was different from what we expected. These are the raw material for the report's deployment section.

### Machine and runtime

- **Only the .NET 10 runtime was installed; the project targets `net9.0`.** Locally `dotnet run` fails with "You must install or update .NET"; on IIS it would be a 502.5. The ASP.NET Core **9.x** Hosting Bundle fixed both — it installs the 9.x runtime and the ASP.NET Core Module together. The runtime alone is not enough for IIS; the bundle is not optional.
- **The template's `Microsoft.AspNetCore.OpenApi` package had to be removed.** It pins `Microsoft.OpenApi` 1.x while Swashbuckle 10 pulls 2.x; the clash throws `ReflectionTypeLoadException` inside MVC controller discovery at startup, before any request is served. The app could not boot at all until the package was gone. Symptom on IIS would have been a 500.30 with a log that names `Microsoft.OpenApi.Models.OpenApiDocument`.
- **`C:\inetpub` is admin-only.** `dotnet publish -o C:\inetpub\smartsolar-api` from a normal shell fails with MSB3191 "Access to the path is denied". Create the folder once from an admin shell and grant the developer account Full Control; publishing then stays non-elevated.
- **Gradle from a plain terminal fails with "Run this build using a Java 11 or newer JVM"** because an old JDK is first on `PATH`. Android Studio is unaffected. For command-line builds set `JAVA_HOME` to `C:\Program Files\Android\Android Studio\jbr`.

### IIS configuration

- App pool `SmartSolarApi`, **.NET CLR version = No Managed Code**, Integrated pipeline. Site `SmartSolarApi` bound to `http://*:8080`, physical path `C:\inetpub\smartsolar-api`. Pool identity granted `(OI)(CI)RX` on the folder with `icacls`.
- **Environment variables are set in `applicationHost.config`, not `web.config`.** IIS Manager → site → Configuration Editor → section `system.webServer/aspNetCore` → **From: `ApplicationHost.config <location path='SmartSolarApi'>`** → `environmentVariables`. `dotnet publish` regenerates `web.config`, so anything put there is wiped on the next publish. Keys use double underscores (`MongoSettings__ConnectionString`, `JwtSettings__Secret`, `Cors__AllowedOrigins__0`, …) plus `ASPNETCORE_ENVIRONMENT=Production`.
- First browse after creating the site returned **500.30** — expected, because the variables were not set yet. Once they were set and the pool recycled, `/api/ping` returned `mongo: true`.

### Network

- **The campus DNS resolver refuses SRV lookups**, so the `mongodb+srv://` connection string fails with `querySrv ECONNREFUSED` (mongosh) or a DNS error in the driver. Workaround: resolve the SRV and TXT records once via a public resolver (`nslookup -type=SRV _mongodb._tcp.<cluster> 8.8.8.8`) and use the equivalent non-SRV form — `mongodb://host1:27017,host2:27017,host3:27017/?tls=true&authSource=admin&replicaSet=<from TXT>`. That form is what the IIS site uses. On a network with a normal resolver the SRV form works; keep both strings in the team notes.
- **The firewall rule must be created with `profile=any`.** The campus Wi-Fi (`SLIIT-STD`) is classified *Public*; a rule added through the GUI defaults to Domain/Private and silently does nothing. `netsh advfirewall firewall add rule name="SmartSolar API 8080" dir=in action=allow protocol=TCP localport=8080 profile=any`.
- **The DHCP lease changed mid-session, twice** — between `172.22.243.99` and `172.28.25.79`. Every IP-bearing setting (`mobile/local.properties`, `network_security_config.xml`, the URL typed on the phone, `Cors:AllowedOrigins` once the web client is published) went stale without any error message; the emulator's request just hung in `SYN_SENT`. Before every integration test: `(Get-NetIPAddress -InterfaceAlias Wi-Fi -AddressFamily IPv4).IPAddress`. For the demo, use a phone hotspot or a DHCP reservation so the address is stable.
- Phone on the same Wi-Fi reached `http://<lan-ip>:8080/api/ping` once the rule and IP were right — no client isolation on this network. If a network *does* isolate clients, the symptom is identical to a missing firewall rule (timeout); the way to tell them apart is to move both devices to a hotspot.

### Android emulator

- Two different addresses reach the same IIS site from the emulator: `10.0.2.2:8080` (the emulator's alias for the host) and the host's LAN IP. **Use the LAN IP in `API_BASE_URL`** — it is the only one that also works on a real device and it exercises the firewall rule.
- Chrome inside the emulator loading `/api/ping` proves the network path but **not** the app's cleartext policy — Chrome has its own. The app needed both `<uses-permission android:name="android.permission.INTERNET"/>` and `android:networkSecurityConfig="@xml/network_security_config"` on `<application>`, with `res/xml/network_security_config.xml` permitting cleartext for `10.0.2.2` and the LAN IP only. Verified with a temporary `HttpURLConnection` call from `MainActivity` logging `ping -> 200`; the temporary code was removed before commit.
- `HttpURLConnection` has **no default connect timeout**. When the IP was stale the call hung forever with no exception — it looked like the app had simply not run the code. The networking layer (§6 of the plumbing guide) must set `setConnectTimeout`/`setReadTimeout`.

### Still to do on this machine

- [ ] Web client site on port 8081 with the URL Rewrite rule, then add `http://<lan-ip>:8081` to `Cors__AllowedOrigins__1` and recycle the API pool.
- [ ] Atlas network-access entry for the IIS machine's *public* IP if the demo runs from a different network.
- [ ] Reproducibility check on Day 12: Member 2 follows this document on a fresh site.
