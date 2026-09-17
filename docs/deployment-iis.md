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

---

## Reproducibility check (Day 12)

Member 2 follows these notes on a fresh IIS site, without help from Member 4. Anything that needed a verbal explanation is missing from this document — add it. The marking scheme wants *"hosting and deployment steps reproducible"*, and this is how you evidence it.
