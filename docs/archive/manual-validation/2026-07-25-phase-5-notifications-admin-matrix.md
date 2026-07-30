# E2E Regression Matrix — Phase 5: Multi-User Notifications & Admin Dashboard

Consolidated from all 13 per-plan matrices/plans on 2026-07-29 (phase wrap-up). Plans without a dedicated matrix file (5, 11) never had one written — see their stub sections below. Checkboxes below are carried over verbatim from each source file at the time it was written; they were not individually re-clicked-through during this consolidation pass (see closing verification note at the bottom for what *was* re-run).

---

## Plan 1: JWT Role Claim Fix

### Setup
- [ ] API server running with seeded admin user
- [ ] `dotnet build` exits 0
- [ ] `dotnet test --filter "FullyQualifiedName~JwtTokenIssuerTests"` passes 3/3

### Happy Path
1. POST `/api/auth/login` with seeded admin credentials → 200 with `accessToken`
2. Decode JWT → `role` claim present with value `"Admin"`, `is_admin` claim still `"true"`
3. Connect to `/hubs/board` with admin JWT, call `JoinProject(<any projectId>)` → succeeds (admin bypass)

### Edge Cases
1. Non-admin login → JWT has **no** `role` claim, `is_admin` equals `"false"`
2. Non-admin calls `JoinProject` on project they don't belong to → `HubException("Access denied")`
3. Pre-fix admin JWT → `IsInRole("Admin")` returns false (must re-login)

### Regressions
1. Non-admin with valid membership still joins `/hubs/board` and `/hubs/presence`
2. `/api/auth/login` response shape, expiry, `is_admin` flag unchanged
3. Full test suite passes — no DI or startup failures

---

## Plan 2: Notification System (Domain + Application + Infrastructure)

### Setup
- [x] `dotnet build` succeeds
- [x] `dotnet test` — 600 tests pass (59 Domain + 210 Application + 74 Infrastructure + 152 Server + 105 TUI)
- [x] No pending EF migrations
- [x] `INotificationRepository` and `INotificationService` resolve from DI

### Happy Path
1. `Notification.Create(...)` returns fully-populated entity (`Id != Guid.Empty`, `IsRead == false`, `CreatedAt` recent)
2. `notification.MarkRead()` flips `IsRead` to `true`
3. `NotifyAsync(UserId != ActorId)` calls `AddAsync` once with correct `UserId` and `Message`
4. `EfNotificationRepository` persists the row

### Edge Cases
1. `Notification.Create` with all optional params null → no exception, nulls stored
2. `NotifyAsync(UserId == ActorId)` → early return, no `AddAsync` call
3. `NotifyAsync(Message == null)` → `Title` used as `Message`
4. `ListByUserAsync(unreadOnly: true)` → `!n.IsRead` filter applied, ordered by `CreatedAt` desc
5. `MarkAsReadAsync` for wrong user → no-op, no exception
6. `MarkAllAsReadAsync` with zero unread → early guard, no `SaveChanges`

### Regressions
1. `Notification` property setters now `private set` — no direct mutation outside entity
2. `DbSet<Notification>` still present in `HydraForgeDbContext`
3. All other service registrations unaffected — server boots, all tests pass
4. TUI and Web UI untouched

---

## Plan 3: NotificationHub + SignalR Push

### Setup
- [x] `dotnet build` succeeds
- [x] `dotnet test` — all tests pass
- [x] `app.MapHub<NotificationHub>("/hubs/notifications")` registered in `Program.cs`
- [x] `/hubs/notifications` in JWT query-string token extraction paths
- [x] `INotificationHubBus` registered as scoped in `AddRealtimeServices`
- [x] Server starts without DI exception

### Happy Path (Application — automated)
1. `NotifyAsync(UserId != ActorId)` calls `AddAsync` THEN `SendNotificationAsync` — fake bus records `(UserId, Notification)` tuple
2. `NotifyAsync(UserId == ActorId)` returns early — repo and bus both untouched
3. `NotifyAsync(Message == null)` passes `Title` as `Message` to `Notification.Create`

### Happy Path (SignalR push — manual)
1. Connect to `ws://localhost:5000/hubs/notifications?access_token=<JWT>` → WS handshake completes — **verified 2026-07-25: `negotiate` with valid JWT returns 200**
2. `OnConnectedAsync` adds connection to group `user-{userId}` — **not independently verifiable without a live WS client; negotiate success implies `[Authorize]` passed, group-join code is untouched since write**
3. Trigger a notification to the connected user → WS frame arrives with `onNotificationReceived` payload — was **BLOCKED** at write time (no caller of `NotifyAsync` existed yet). **Unblocked by Plan 7** (notification triggers, see below) — real callers now exist in `CardService`, `CommentService`, `ProjectService`. Not independently re-verified end-to-end after Plan 7 landed; Plan 7's own matrix covers the trigger-to-notification path.
4. Typed hub method `OnNotificationReceived` used (not raw `"NotificationReceived"` string) — code inspection only, confirmed by reading `SignalRNotificationHubBus`/`INotificationHub`

### Edge Cases
1. Connect without JWT → 401 — **verified 2026-07-25: `negotiate` without token returns 401**
2. Connect with expired/invalid JWT → 401 — **verified 2026-07-25: `negotiate` with garbage token returns 401**
3. `SendNotificationAsync` with no connected clients → empty group, no exception, DB write already committed
4. Two tabs of same user → both in `user-{userId}` group, both receive events
5. `SendNotificationAsync` with cancelled `ct` → `OperationCanceledException` propagates

**Verdict: Plan 3's automatable surface (build, tests, DI, routes, hub auth) is fully verified. Live push-delivery (items 3–4) was correctly deferred to Task 7 at the time this was written and is not itself re-exercised here — see Plan 7 below.**

### Regressions
1. `BoardHub` at `/hubs/board` and `PresenceHub` at `/hubs/presence` unchanged
2. `IBoardHub.OnBoardEvent` typed contract untouched — additive change only
3. `NotificationService` constructor now requires `INotificationHubBus` — 152 Server tests confirm DI resolution works
4. No pending EF migrations
5. JWT query-string extraction now covers three paths — Board/Presence auth still works

### Plan Deviations (informational)
1. `NotificationHub.cs` placed in `src/HydraForge.Infrastructure/Realtime/` (not `src/HydraForge.Server/Hubs/`). Matches `BoardHub` location and typed-hub convention.
2. Uses typed `INotificationHub.OnNotificationReceived(...)` instead of untyped `SendAsync("NotificationReceived", ...)`. Wire name: `onNotificationReceived` (camelCase default).
3. `INotificationHub` and `NotificationReceivedEvent` created in `src/HydraForge.Application/Realtime/`.

---

## Plan 4: Web UI Bell + Notification Panel

**Source:** `docs/manual-validation/2026-07-25-phase-5-notifications-admin-plan-4-web-ui-bell-matrix.md` (deleted after this merge).

### Result

**Partial pass, 2026-07-25.** Happy path 1-6 and 8 verified manually (testuser1, notifications seeded directly via SQL — no trigger wiring existed yet at the time, that gap was closed by Plan 7). Found + fixed along the way:
- Web container was serving a stale prebuilt image (`docker-compose.yml` `web`/`server` services `COPY . .` + build at image time, no volume mount — code changes need `docker compose up -d --build <service>`, not just a page refresh). Bell was invisible for this reason, not a code bug.
- Notification panel header + "Mark all read" made `sticky top-0` so they don't scroll away with a long list.

Both fixes are in commit `2a67829` (PR #55, squash-merged).

**Not independently re-exercised after this pass**: happy path 7 (empty state), edge cases 1/3/4/5/6/7, all regressions. Nothing found during the original pass suggested these were at risk.

### Happy Path

1. See bell icon in navbar header — [x]
2. Unread badge shows correct count — [x]
3. Open notification panel — [x]
4. Notification displays correctly (title/body/timestamp, read/unread styling) — [x]
5. Mark single notification as read (navigates to `actionUrl`) — [x]
6. Mark all as read — [x]
7. Empty state ("No notifications yet") — [ ]
8. Bell count loads on page mount — [x]

### Edge Cases
1. 99+ overflow — [ ]
2. Click notification without `actionUrl` → no navigation — [ ]
3. Panel close on outside click — [ ]
4. Concurrent unread decrement (already-read notification) — [ ]
5. Not authenticated → bell not rendered — [ ]
6. API failure (unread count) → bell renders with no badge — [ ]
7. API failure (notifications list) → empty panel, no error toast — [ ]

### Regressions
1. Color mode button still works — [ ]
2. Logout button still works — [ ]
3. Header layout unchanged — [ ]
4. Session expiry modal still renders — [ ]

### Cleanup
- [x] Seeded rows for `testuser1` (`cc71c3af-3b20-4715-ac61-d536b063d841`) removed from `notifications` table.

---

## Plan 5: TUI Unread Count in Status Bar

**No dedicated manual-validation matrix was ever written for this plan** (shipped in commit `5519a40`, PR #56). The plan document's own "Verification" section specified: `dotnet build` clean, `dotnet test tests/HydraForge.Tui.Tests/` green, and a manual check — start TUI, status bar shows "0 unread", trigger a notification (via Plan 7), count increments, `U` opens the list. Automated portion is covered by the current full-suite run (116 TUI tests passing, see closing verification note below); the manual portion was not separately recorded and is not re-run here.

---

## Plan 6: ntfy Integration

**Correction (2026-07-27):** The prior "closed" pass (D-52, commit `8af0a51`) marked this plan done, but 3 of 12 planned files/steps were never actually committed in `d5df06e`: `INtfyClient.cs` (Application port), `NtfyOptions.cs` (Infrastructure), and all of `SystemSettings` Step 1 (new fields, `UpdateSettings()`, `SystemSettingsSingletonId` move) plus the `AddSystemSettingsFields` migration. **`dotnet build` was actually failing on this branch** (`CS0246: INtfyClient could not be found`) — the matrix below was never runnable as written. Also found: `docker-compose.yml`'s `ntfy` service had no `command`, so the container printed help text and exited immediately instead of serving. All of the above fixed on `task/ntfy-integration` 2026-07-27; results below are from actually running each check, not carried over from the original (incorrect) close-out.

### Setup
- [x] `docker compose up -d postgres minio` (host ports 5433, 9000, 9001) — **verified 2026-07-27**
- [x] `dotnet ef database update` — `AddSystemSettingsFields` migration applied clean — **verified 2026-07-27**
- [x] `docker compose --profile notifications up -d ntfy` — starts — **verified 2026-07-27** (required adding `command: serve` to `docker-compose.yml`)
- [x] `dotnet run --project src/HydraForge.Server` — server starts, no DI errors — **verified 2026-07-27**
- [x] `curl http://localhost:8083/v1/health` → 200 — **verified 2026-07-27**

### Happy Path
1. Migration applies on a DB with an existing `system_settings` row → 4 new nullable columns, existing row preserved — **verified 2026-07-27**
2. Trigger any notification → row in `notifications` table + SignalR push — **BLOCKED at write time** (no caller of `NotifyAsync` yet, Task 7 not landed). Not independently re-verified after Plan 7 landed.
3. `docker compose ps` shows `ntfy` `healthy` after ~30s — **verified 2026-07-27**
4. `docker compose logs ntfy` — no crash, DBs created — **verified 2026-07-27**

### Edge Cases
1. `NotificationService` with `ntfyClient: null` → no exception — **verified** (test)
2. `NtfyClient.PublishAsync` with `_serverUrl = null` → no HTTP call — **verified** (test)
3. `NtfyClient.PublishAsync` with unreachable server → exception swallowed — **verified** (test)
4. Concurrent notifications, many users → per-user topic, no cross-talk — **verified** (test + inspection)
5. `UserId == ActorId` → short-circuits — **verified** (existing test)

### Regressions
1. Existing notification flow (DB + SignalR) still works — **verified**
2. `system_settings` seed row `Id` unchanged — **verified 2026-07-27**
3. TUI/Web UI bell + count still updates — not retested, no code path touches this
4. `dotnet build`/`dotnet test`/EF drift clean — **verified 2026-07-27**: 617 tests pass
5. `SystemSettingsSingletonId` moved to `SystemSettings.SingletonId` — **verified 2026-07-27**

### Docker
1. `docker compose --profile notifications config` includes `ntfy` — **verified 2026-07-27**
2. `ntfy-data` volume declared (profile-gated, only visible with `--profile notifications`) — **verified 2026-07-27**
3. `NTFY_AUTH_DEFAULT_ACCESS: deny-all` set — **verified 2026-07-27**
4. `NTFY_BASE_URL=http://localhost:8083` in `.env.example` — **verified 2026-07-27**

**Verdict: All automatable surface now genuinely passes. The only remaining gap (Happy Path item 2, real end-to-end push) was correctly blocked on Task 7 at write time, same as Plan 3.**

### Cleanup
- [x] `docker compose --profile notifications down` (volumes preserved) — done 2026-07-27
- [x] No test data created

---

## Plan 7: Notification Trigger Points

7 triggers across 4 services. Automated tests cover `NotifyRequest` recording + actor-skip; the happy-path list below documents intended manual coverage of real end-to-end wiring — the click-through itself was not fully executed and checked off (see items below), but code-review of triggers 4–7 caught and fixed one real bug.

### Code review findings (triggers 4–7)

- **Trigger 4 (@mention)** — backend fully implemented (`CommentService.cs`): regex-parses `@username`, resolves to project members only. No autocomplete UI in `CardComments.vue` (plain textarea) — type the literal `@UserB` text.
- **Trigger 5 (dependency resolved) — was broken, fixed.** Notify call was wired into `CardService.MoveAsync`, but only `Archive()` sets `ArchivedAt` — Move never does, so the "last active blocker" check never fired. Fixed by moving the call into `CardService.ArchiveAsync`. Added `ArchiveAsync_ArchivingLastActiveBlocker_NotifiesBlockedCardAssignees` and `ArchiveAsync_OtherActiveBlockerRemains_DoesNotNotify` regression tests (zero coverage existed before, which is why the wiring bug went unnoticed). See also `CLAUDE.md` "Notification trigger patterns" and `docs/DECISIONS.md`.
- **Triggers 6 & 7 (project archived / updated)** — confirmed working as specced, including single-field edits still notifying.

### Setup
- [x] Docker Compose up (postgres + minio)
- [x] Server running
- [x] Two users exist (User A, User B), both project members
- [x] Test project has card #1 assigned to User B
- [ ] User B is watcher on card #1
- [ ] Card #1 has a "blocked by" relationship from card #2
- [x] User A is the actor for all actions

### Happy Path — 7 Triggers
1. Card moved (watcher/assignee notified) — not independently re-verified in this pass
2. Card assigned — not independently re-verified in this pass
3. Comment added (watcher notified) — not independently re-verified in this pass
4. @mention in comment — confirmed via code review (see above)
5. Dependency resolved (archive blocker) — confirmed via code review + new regression tests (see above); the wiring bug this uncovered is now fixed
6. Project archived — confirmed via code review
7. Project updated — confirmed via code review, including single-field edits

### Edge Cases
1. Actor self-exclusion — covered by existing `NotificationService` unit tests (actor==recipient skip)
2. Card move without watchers/assignees → no crash — not independently re-verified
3. Comment without @mention → no mention notification, watcher notification still fires — not independently re-verified
4. Card move without resolved blockers → no "unblocked" notification — not independently re-verified
5. Empty project update (name-only) still notifies — confirmed via code review

### Regressions
1. Card CRUD still works — covered by full Application/Server test suite (green, see closing note)
2. Project CRUD still works — covered by full test suite
3. Existing `NotificationService` behavior unchanged — covered by full test suite
4. Existing SignalR notification types still work — not independently re-verified live

---

## Plan 8: Admin All-Projects Bypass

### Setup
- [x] `dotnet build` exits 0
- [x] `dotnet test --filter "FullyQualifiedName~MembershipGuardTests"` passes 3/3
- [x] `dotnet test --filter "FullyQualifiedName~ClaimsPrincipalExtensionsTests"` passes 3/3
- [x] `dotnet test` — all tests pass (12 new per-service admin-bypass tests)

### Happy Path — Admin non-member on project they're not a member of

1. Login as admin, access **cards** (create, read, update, move, delete) on a project where admin is not a member → succeeds
2. Login as admin, access **columns** (create, read, update, delete, reorder) → succeeds
3. Login as admin, access **specs** (create, read, update, delete) → succeeds
4. Login as admin, access **plans** (create, read, update, delete, set status) → succeeds
5. Login as admin, access **checklist** (create, read, update, delete items) → succeeds
6. Login as admin, access **attachments** (create, read, delete) → succeeds
7. Login as admin, access **comments** (create, read, update, delete) → succeeds
8. Login as admin, access **card relationships** (create, delete) → succeeds
9. Login as admin, access **project members** (add, remove, change role) → succeeds (admin cannot self-remove from unowned project — guarded by `RemoveMemberAsync` NRE-guard)
10. Login as admin → **project list** shows ALL projects (including non-member ones), `MyRole` is null for non-member projects
11. Login as admin → access **ProjectSnapshot** endpoint for a non-member project → 200 OK

### Denied — Non-admin non-member

1. Login as non-admin, non-member of project → all 10 resource types above return 403 / `Result.Failure`
2. Login as non-admin non-member → project list shows only member projects

### Edge Cases

1. Admin is also a member of a project → normal member role behavior preserved, `MyRole` shows actual role
2. Admin removes another member from a project they're not a member of → succeeds (admin bypass)
3. Admin removes self from a project they own → NRE-guard prevents crash, admin removal should still work if admin IS a member
4. Nullable `MyRole` — TUI renders `"—"` for null role, Web UI renders appropriately
5. `ClaimsPrincipalExtensions.IsProjectMemberOrAdmin` — admin returns true without DB membership check (no round-trip)

### Regressions

1. Non-admin member workflows unchanged — all existing `CardService`, `ChecklistService`, etc. tests pass
2. `ProjectService.GetAllAsync` non-admin path unchanged (same `ListByUserIdAsync` call)
3. `BoardHub`/`PresenceHub` need no code change — already gate on `IsInRole` (Task 1 JWT fix)
4. `dotnet test` — all suites pass

---

## Plan 9: Admin Controller + User Management

**Source:** `docs/manual-validation/2026-07-25-phase-5-notifications-admin-plan-9-admin-controller-user-mgmt-matrix.md` (deleted after this merge). Application-level behavior (validation errors, self-disable/self-demotion guards, pagination) is covered by the automated Application/Server test suites; the items below were the intended manual click-through and were not individually re-verified in this consolidation pass.

### Happy Path — API
1. `GET /api/admin/users` with admin bearer token → `200` + paginated `{ items, totalCount }`
2. `POST /api/admin/users` with valid body → `201` + created user DTO
3. `PATCH /api/admin/users/{id}/disable` → `204`, user becomes disabled
4. `PATCH /api/admin/users/{id}/enable` → `204`, user re-enabled
5. `POST /api/admin/users/{id}/reset-password` → `204`
6. `PATCH /api/admin/users/{id}/role` → `204`, admin flag toggles
7. `GET /api/admin/projects` → `200` + paginated projects list (includes non-member projects)
8. `GET /api/admin/projects/{id}` → `200` + project detail for any project

### Happy Path — Web UI
1. Navigate to `/admin` as admin → redirects to `/admin/users`
2. `/admin/users` shows paginated user table with username, email, status badges
3. Disable / Enable an active user → badge updates, toast shows
4. Make Admin / Remove Admin → admin badge toggles
5. Create User modal → user appears in table, toast shows
6. Search by username → table filters, total count updates
7. Reset password modal → `204`
8. `/admin/projects` → shows all projects including non-member ones; "Open Board" navigates

### Edge Cases
1. Duplicate username → `400` + `USERNAME_TAKEN`
2. Blank username → `400` + validation error
3. Short password (< 6 chars) → `400` + validation error
4. Nonexistent GUID → `404`
5. Self-disable → `400` + `SELF_DISABLE`
6. Disable nonexistent user → `404`
7. Self-demotion via role change → `400` + `ADMIN_SELF_DEMOTION`
8. No auth token → `401`
9. Non-admin token → `403`
10. Non-admin navigating `/admin` or `/admin/users` → redirects to `/projects`
11. Search with no matches → empty table, `0 total users`

### Regressions
1. Login as admin → `200`, JWT returned
2. Login as disabled user → `401`
3. Login as non-admin → `200`, normal session
4. Non-admin `/projects` unaffected by admin bypass
5. Project CRUD for non-admin unaffected
6. SignalR board presence unaffected

---

## Plan 10: System Settings API + Cache + Web UI

**Source:** `docs/manual-validation/2026-07-25-phase-5-notifications-admin-plan-10-system-settings-matrix.md` (deleted after this merge). Same status as Plan 9 — automated coverage (cache TTL behavior, DI scoping) is green in the full suite; manual click-through items below were not individually re-verified in this consolidation pass.

### Happy Path — API
1. `GET /api/admin/settings` → `200` + all 7 fields
2. `PUT /api/admin/settings` partial update → `200` + confirmation message
3. Re-`GET` reflects the update
4. Multi-field `PUT` (ntfy URL + brand name) → both persist

### Happy Path — Web UI
1. Sidebar "System Settings" (gear icon) → `/admin/settings`
2. Page shows 4 sections: Retention, Notifications, Search, Branding
3. Each section's Save button → success toast, "Changes apply within 5 minutes" copy for retention
4. Refresh page → values persist from API

### Edge Cases
1. No auth token → `401`
2. Non-admin token (GET/PUT) → `403`
3. Empty `PUT` body `{}` → `200`, no fields changed
4. `null` field in `PUT` → ignored, existing value unchanged (domain method null-guards)
5. Negative retention value → server accepts (UI has no min guard)
6. Non-admin navigating `/admin/settings` → redirects to `/projects`
7. Unauthenticated → redirects to login

### Cache & Startup
1. Direct DB row change + restart → first `GET` still returns cached value (5-min TTL) — this is the intended behavior, not a bug
2. Cache expires after TTL → new value picked up
3. `Development` env startup → no `ValidateScopes` exception (confirms `CachedSettingsProvider` is scoped, not singleton)

### Ntfy URL Wiring
1. Set `NtfyServerUrl` → notification triggers a POST to `{url}/hydraforge-{userId}` (ntfy logs)
2. Change URL → next notification goes to new URL, no restart needed
3. Clear to `null` → notifications silently skip publish, no error

### Regressions
1. Login as admin → `200`
2. `/admin/users` still works
3. Non-admin `/projects` unaffected
4. Notifications still deliver via SignalR
5. Project CRUD for non-admin unaffected (membership enforced)

---

## Plan 11: Audit Log Reader + Controller

**No dedicated manual-validation matrix was ever written for this plan** (shipped in commit `7c9dcb3`, PR #62 — audit log reader service + `GET /api/admin/audit-log` controller). Its consumer, the Web UI page, is covered by Plan 12 below (the reader has no independent UI surface — Plan 12's Setup/Happy-Path items exercise it end-to-end). Application-layer reader logic (filtering, pagination) has automated test coverage in the current green suite.

---

## Plan 12: Audit Log Web UI Page

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

---

## Plan 13: Admin Dashboard Home Page

### Setup
- [ ] `docker compose up -d postgres` — DB running
- [ ] `dotnet run --project src/HydraForge.Server` — server up on `:5000`
- [ ] `cd src/web-ui && pnpm dev` — web UI up on `:3000`
- [ ] Admin seed ran — admin user exists (default: admin / Admin123!)
- [ ] At least one non-admin test user exists
- [ ] At least one project with one archived + one active project (so breakdown has both > 0)
- [ ] At least one disabled user (so Users breakdown has disabled > 0)
- [ ] At least 10 audit log entries exist (so "Recent Audit Log" fills the preview)

### Happy Path — Sidebar Nav
1. Log in as admin → sidebar shows "Admin" group with items in order: Dashboard, Users, System Settings, Audit Log
2. Hover "Dashboard" in sidebar → tooltip / hover state renders
3. Click "Dashboard" in sidebar → navigates to `/admin`
4. Click "Users" in sidebar → navigates to `/admin/users`
5. Log out, log in as non-admin → sidebar has NO "Admin" group at all

### Happy Path — Dashboard Page (admin lands here)
1. Navigate to `/admin` as admin → page renders (NOT redirected to `/admin/users`)
2. Page shows heading "Admin Dashboard"
3. Three summary cards visible: Users, Projects, Health
4. Users card shows total count matching `GET /api/admin/users` totalCount, with "X active · Y disabled" subtitle matching server-side breakdown
5. Projects card shows total count matching `GET /api/admin/projects` totalCount, with "X active · Y archived" subtitle matching server-side breakdown
6. Health card shows green dot + "Online" label (static, no fetch)
7. Four quick-action buttons render: Manage Users, All Projects, System Settings, Audit Log
8. Click "Manage Users" → navigates to `/admin/users`
9. Click "All Projects" → navigates to `/projects`
10. Click "System Settings" → navigates to `/admin/settings`
11. Click "Audit Log" → navigates to `/admin/audit-log`
12. "Recent Audit Log" section heading renders
13. DataTable shows up to 10 audit entries with columns: Timestamp, Actor, Entity Type, Action
14. Timestamp column renders formatted local date string (not raw ISO)
15. "View Full Audit Log" button renders (because recentAudit.length > 0) and navigates to `/admin/audit-log`

### Edge Cases
1. Navigate to `/admin` while logged out → middleware redirects to `/login`
2. Navigate to `/admin` as non-admin → redirects to `/chats` (does NOT render dashboard)
3. Disable API server, then load `/admin` as admin → error toast with correlation ID shown, summary cards stay at `0`
4. Empty install (no audit entries) → "Recent Audit Log" DataTable shows "No results found." empty state, "View Full Audit Log" button is HIDDEN
5. Install with exactly 10 audit entries → "View Full Audit Log" button renders
6. Install with 500+ users → Users card totalCount shows real total, but active/disabled breakdown only counts the first 500 fetched (DASHBOARD_BREAKDOWN_PAGE_SIZE constant) — verify this matches plan's "revisit with a dedicated counts endpoint" caveat
7. Install with 500+ projects → same caveat as #6 for the Projects card breakdown

### Regressions
1. Existing admin pages still work: `/admin/users`, `/admin/settings`, `/admin/audit-log` render unchanged
2. Existing non-admin pages still work: `/projects`, `/chats`, login still functional
3. Sidebar "Workspace" group (Chats, Projects) and disabled "AI Tools"/"Creative"/"Personal" groups still render
4. Login / logout / JWT refresh still work
5. SignalR board presence unaffected
6. Admin nav group item order matches plan: Dashboard → Users → System Settings → Audit Log (Reports still disabled placeholder)
7. `/api/admin/audit-log?take=10` query works (server accepts the `take` parameter, returns up to 10 items + totalCount)

### Cleanup
- [ ] No test data needs cleanup (dashboard is read-only)

---

## Closing verification — Phase 5 wrap-up (2026-07-29)

Re-run at the point of archiving this matrix, as the objective floor beneath every partial/unchecked item above:

- `dotnet build` — clean, 0 errors (1 pre-existing unrelated warning in `EfProjectRepository.cs`)
- `dotnet test` — **683/683 pass** (69 Domain + 248 Application + 85 Infrastructure + 165 Server + 116 Tui)
- `dotnet ef migrations has-pending-model-changes` — "No changes have been made to the model since the last migration."

All 13 phase-5 plans are merged to `main` (or this branch) and have shipped in production commits. The unchecked manual items above represent UI click-through detail that was not independently re-verified during this consolidation pass, not known defects — nothing in code review or the automated suite surfaced a regression in any of them.
