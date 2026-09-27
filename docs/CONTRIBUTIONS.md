# Contributions Log

Append one row after each working session. Keep it in date order — do not edit
another member's rows.

| Date | Member | Branch | What was built |
| --- | --- | --- | --- |
| 2026-09-19 | Dahami | chore/shared-plumbing | Shared plumbing §1–5: config binding, Mongo singleton + models, BusinessRuleException + exception middleware, method-override middleware, JWT bearer + CORS, PingController |
| 2026-09-19 | Dahami | chore/seed-data | Seed script with real PasswordHasher hash (Test@1234); fixed ObjectId prefix length and nic unique index; seeded Atlas and verified all references |
| 2026-09-19 | Dahami | docs/deployment-notes | First IIS publish of the API (port 8080), env vars in applicationHost.config, firewall rule, phone + emulator ping verified; Android cleartext config; deployment notes written into deployment-iis.md |
| 2026-09-26 | Dahami | feature/identity-auth-api | Auth API: POST /auth/login (PBKDF2 verify, Pending/Deactivated block), POST /auth/register-prosumer (NIC old+new format, NIC/username/email uniqueness incl. duplicate-key race, role/status forced server-side), GET /auth/me; AuthService, claim helpers, safe profile DTO; new error codes registered |
| 2026-09-27 | Aman | feature/nodes-api-crud | Node CRUD endpoints: request/response DTOs with coordinate, capacity and schedule validation; StationRepository listing, insert, replace and status update; SlotRepository counts; NodeService with the GeoJSON [lng, lat] mapping, the prosumer active-only visibility rule and the battery-count guard; IReservationQueries declared for the shared active-reservation predicate with an interim implementation pending ReservationService; new node error codes registered in response-format.md |
