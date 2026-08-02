## Validate: Plan 21 — Hangfire + SystemSettings.AiNarrativeGenerationTimeUtc

### Setup
- [ ] Server running (`docker compose up` + `dotnet run --project src/HydraForge.Server`)
- [ ] Web dev server running (`cd src/web-ui && pnpm dev`)
- [ ] Logged in as admin (role = `Admin`)
- [ ] Browser has `auth_token` cookie set by login (verify in devtools)
- [ ] DB migrated — `system_settings` row has `ai_narrative_generation_time_utc = '00:00:00'` (verify: `psql -p 5433 -d hydraforge -c "SELECT \"AiNarrativeGenerationTimeUtc\" FROM system_settings;"`)

### Happy Path
1. Navigate to `/hangfire` → Hangfire dashboard loads, shows empty job list (no recurring jobs registered yet — that's expected; this plan only wires the infrastructure)
2. Navigate to `/admin/settings` → "AI Narrative Generation Time" time picker shows `00:00` (default)
3. Change time to `03:30`, click "Save Retention" → toast "Settings saved...", reload page → picker shows `03:30`
4. PUT `{"aiNarrativeGenerationTimeUtc": "01:15:00"}` to `/api/admin/settings` via curl → 200 OK, DB row updated to `01:15:00`, `GET /api/admin/settings` reflects `01:15:00`
5. Set time to `23:59`, save → DB row updated, persists across reload

### Edge Cases
1. PUT `/api/admin/settings` with body `{}` → 200 OK, all fields unchanged (including AiNarrativeGenerationTimeUtc). Regression check for the Cycle 7 bug where empty body cleared the schedule
2. PUT `/api/admin/settings` with `{"archivedItemRetentionDays": 500}` (only retention, no aiNarrativeTime field) → 200 OK, AiNarrativeGenerationTimeUtc preserved at prior value (not nulled)
3. PUT `/api/admin/settings` with `{"aiNarrativeGenerationTimeUtc": null}` → 200 OK, DB row's AiNarrativeGenerationTimeUtc becomes NULL (explicit clear works)
4. PUT `/api/admin/settings` with body `null` (literal JSON null) → 400 BadRequest with ProblemDetails "Request body is required."
5. PUT `/api/admin/settings` with malformed JSON (e.g. `{`) → 400 BadRequest with ProblemDetails "Invalid JSON in request body."
6. PUT `/api/admin/settings` with empty body → 400 BadRequest "Request body is required."
7. Non-admin user navigates to `/hangfire` → 401/403 redirect, dashboard does not load (AdminRequiredAuthFilter rejects)
8. Logged-out user navigates to `/hangfire` → redirected to login (cookie auth fails, no `auth_token` cookie)
9. While logged in as admin, navigate directly to `/hangfire` in browser → dashboard loads (cookie auth path in JWT bearer events fires, filter passes)
10. Time picker cleared (set to empty) and saved → DB row's AiNarrativeGenerationTimeUtc becomes NULL

### Regressions
1. All existing `/api/admin/*` endpoints still work (users, projects, audit-log, settings GET)
2. `GET /api/admin/settings` response includes `aiNarrativeGenerationTimeUtc` field
3. Existing settings fields (retention, ntfy, searxng, branding) still save correctly via the form
4. Non-admin cannot reach `/admin/settings` (redirected to `/projects`)
5. Server tests still pass: `dotnet test` shows 933 passed
6. EF migration model is clean: `dotnet ef migrations has-pending-model-changes` reports "No changes"
7. Hangfire dashboard does NOT load in Test environment (factory `WebApplicationFactory<Program>` skips `AddHangfire` + `UseHangfireDashboard` via `IsEnvironment("Test")` check — verify in `AdminControllerTests` that admin endpoints still respond)
8. Other admin pages (`/admin/providers`, `/admin/provider-models`, `/admin/routing`, `/admin/users`) still load
9. SignalR hubs, board view, project list still functional

### Cleanup
- [ ] Reset `AiNarrativeGenerationTimeUtc` to `00:00:00` if desired