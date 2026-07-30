## Validate: Audit Log Web UI Page (Plan 12)

### Setup
- [ ] `docker compose up -d postgres` — DB running
- [ ] `dotnet run --project src/HydraForge.Server` — server up on `:5000`
- [ ] Admin seed ran — admin user exists (default: admin / Admin123!)
- [ ] A non-admin test user exists (create via UI or seed)
- [ ] At least one project with a few cards exists so the audit log has rows (Card create/update/move produce entries)
- [ ] `cd src/web-ui && pnpm dev` — web UI up on `:3000`

### Happy Path
1. Login as admin → sidebar shows "Audit Log" nav item under admin group
2. Click "Audit Log" → navigates to `/admin/audit-log`
3. Page shows filter bar (Project ID, Actor ID, Entity Type, Action, From, To, Reset) + results table with columns Timestamp / Actor / Entity Type / Entity ID / Action / Scope + Details button
4. Page loads with `GET /api/admin/audit-log?skip=0&take=50` → table populated, "X total entries" shown
5. Page 1 of N displayed; "Previous" disabled, "Next" enabled (if totalCount > 50)
6. Click "Next" → `GET ...?skip=50&take=50` → page 2 shown, "Previous" enabled
7. Click "Previous" → back to page 1
8. Click "Details" on a row → row expands showing Old Value + New Value JSON side-by-side; button label switches to "Collapse"
9. Click "Collapse" → row collapses, button label back to "Details"

### Filters
10. Select Entity Type = "Card" → `GET ...&entityType=Card` → table filtered to card entries only
11. Select Entity Type = "All" → `entityType` param omitted from URL → unfiltered results
12. Select Action = "Moved" → `GET ...&action=Moved` → table filtered to move events
13. Type a valid Project ID GUID into the Project ID filter → `GET ...&projectId={guid}` → filtered to that project
14. Type a non-GUID string (e.g. "abc") into the Project ID filter → `projectId` param omitted, no crash (GUID validation guard)
15. Set "From" to yesterday's date → `GET ...&from={ISO timestamp}` → results from yesterday onward
16. Set "To" to a future date → `GET ...&to={ISO timestamp}` → results clamped to that date
17. Click "Reset filters" → all filter inputs cleared, `page` resets to 1 (after 300ms debounce), unfiltered results

### Edge Cases
18. Navigate to `/admin/audit-log` while unauthenticated → auth middleware redirects to `/login`
19. Navigate to `/admin/audit-log` as non-admin → `navigateTo('/projects')` fires (UI guard); API call would `403` (server guard)
20. Stop Postgres mid-load → `loadEntries` catch block fires → toast.error "Failed to load audit log"; `loading` cleared
21. Type in Project ID filter rapidly → debounced (300ms) → only one API call fires (deduplicated via requestSeq)
22. Change filter while previous request is in-flight → stale response discarded via `requestSeq` guard; latest filter value wins
23. Click "Last page" boundary → API returns fewer than `take` items → "Next" disabled when `page >= totalPages`
24. `totalCount === 0` → table shows empty body, "0 total entries" shown, both pagination buttons disabled

### Regressions
25. Admin user management (`/admin/users`) still works — page loads, list populates
26. System settings (`/admin/settings`) still loads
27. Non-admin user loads `/projects` → sees own projects (admin bypass unchanged)
28. Auth flow (login, logout, token refresh) unaffected
29. Phase 5 notification bell still shows unread count (real-time hub unchanged)

### Cleanup
- [ ] No test data needs cleanup (audit log is append-only and rolls over per retention policy)
