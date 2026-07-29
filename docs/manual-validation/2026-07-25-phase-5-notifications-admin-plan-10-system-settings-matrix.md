## Validate: System Settings API + Cache + Web UI (Plan 10)

### Setup
- [ ] `docker compose up -d postgres` — DB running
- [ ] `dotnet run --project src/HydraForge.Server` — server up on `:5000`
- [ ] Admin seed ran — admin user exists (default: admin / Admin123!)
- [ ] A non-admin test user exists (create via UI or seed)
- [ ] `cd src/web-ui && pnpm dev` — web UI up on `:3000`

### Happy Path — API
1. `GET /api/admin/settings` with admin bearer token → `200` + JSON with all 7 fields (ArchivedItemRetentionDays, AuditLogRetentionDays, NotificationRetentionDays, NtfyServerUrl, SearXngUrl, BrandName, BrandLogoUrl)
2. `PUT /api/admin/settings` with `{ "archivedItemRetentionDays": 365 }` → `200` + `{ "message": "Settings updated..." }`
3. `GET /api/admin/settings` again → `200`, `archivedItemRetentionDays` is now `365`
4. `PUT /api/admin/settings` with `{ "ntfyServerUrl": "http://ntfy:8083", "brandName": "MyForge" }` → `200`
5. `GET /api/admin/settings` → `200`, `ntfyServerUrl` is `"http://ntfy:8083"`, `brandName` is `"MyForge"`

### Happy Path — Web UI
1. Login as admin → sidebar shows "System Settings" nav item with gear icon
2. Click "System Settings" → navigates to `/admin/settings`
3. Page shows 4 sections: Retention, Notifications, Search, Branding
4. Change `Archived Item Retention` to `365`, click "Save Retention" → toast "Settings saved. Changes apply within 5 minutes."
5. Set `ntfy Server URL` to `http://ntfy:8083`, click "Save Notifications" → success toast
6. Set `SearXNG URL` to `http://searxng:8080`, click "Save Search" → success toast
7. Set `Brand Name` to `MyForge`, `Brand Logo URL` to `https://example.com/logo.png`, click "Save Branding" → success toast
8. Refresh page → all values persist from API

### Edge Cases
1. `GET /api/admin/settings` without auth token → `401`
2. `GET /api/admin/settings` with non-admin token → `403`
3. `PUT /api/admin/settings` with non-admin token → `403`
4. `PUT /api/admin/settings` with empty body `{}` → `200`, no fields changed
5. `PUT /api/admin/settings` with `{ "ntfyServerUrl": null }` → `200`, `ntfyServerUrl` stays unchanged (null ignored by domain method)
6. Set `Archived Item Retention` to `-1` → saves (UI has no `min` guard; server accepts any int)
7. Navigate to `/admin/settings` as non-admin → redirects to `/projects`
8. Navigate to `/admin/settings` while unauthenticated → redirects to login

### Cache & Startup
1. Stop server, change Postgres `SystemSettings` row directly (e.g. set `brand_name = 'DirectDB'`), restart server → first `GET /api/admin/settings` returns the old cached value (5-min TTL), not the direct DB value
2. Wait 5+ minutes (or restart enough times) → cache expires, new value picked up
3. Start server in `Development` environment (`ASPNETCORE_ENVIRONMENT=Development`) → no `InvalidOperationException` from `ValidateScopes` (confirms cached provider scoped, not singleton)

### Ntfy URL Wiring
1. Set `NtfyServerUrl` via API to a valid ntfy server URL
2. Trigger a notification (e.g. someone assigns you a card) → ntfy server receives POST to `{url}/hydraforge-{userId}` (check ntfy logs)
3. Update `NtfyServerUrl` via API to a different URL → next notification goes to the new URL (no server restart needed)
4. Clear `NtfyServerUrl` to `null` → notifications silently skip (no HTTP call, no error thrown)

### Regressions
1. Login as admin → `200`, JWT returned
2. Admin user management (`/admin/users`) still works as before
3. Non-admin user loads `/projects` → sees own projects (admin bypass unchanged)
4. Notifications still deliver via SignalR to Web UI and TUI
5. Project CRUD for non-admin user → membership enforced (unaffected)

### Cleanup
- [ ] No test data needs cleanup (settings changes are persistent and reversible)
