## Validate: Web UI admin AI Feature Routing

### Setup
- [ ] Server running (`docker compose up` + `dotnet run --project src/HydraForge.Server`)
- [ ] Web dev server running (`cd src/web-ui && pnpm dev`)
- [ ] Admin account exists and logged in
- [ ] Feature routing configs seeded (verify via `GET /api/admin/routing`)
- [ ] 11 rows visible in the table

### Happy Path
1. Navigate to `/admin/routing` → DataTable shows 11 AiFeature rows with Feature name, Default Tier select, Max User Tier select
2. Change Default Tier for "Personal Chat" from Economy to Standard → toast "Routing updated", select reflects new value, page reload preserves change
3. Change Max User Tier for "Project Chat" from Premium to Locked → toast "Routing updated", select shows "Locked (default only)", reload shows `null`
4. Change Max User Tier for "Deep Research" from Locked to Premium → toast "Routing updated", reload preserves Premium

### Edge Cases
1. Rapid double-click on a tier select → each click saves independently, no stale data or duplicate toasts
2. Edit Default Tier on "Personal Chat" then immediately edit Default Tier on "Project Chat" (different row) before the first save settles → both rows show success toast and persist correctly (regression check for the shared-counter bug fixed in this pass — an earlier version dropped the first row's toast/state update whenever a second row was edited while the first save was in flight)
3. Network failure during save → toast error, select reverts to previous value
4. Non-admin user navigates to `/admin/routing` → redirected to `/projects`
5. All 11 rows present with correct human-readable feature names from AI_FEATURE_LABELS map

### Regressions
1. Existing admin pages (`/admin/providers`, `/admin/provider-models`, `/admin/audit-log`) still load correctly
2. `/admin` nav link still works
3. Board view, project list, chat still functional

### Cleanup
- [ ] Reset any changed routing configs if desired (no test data created)