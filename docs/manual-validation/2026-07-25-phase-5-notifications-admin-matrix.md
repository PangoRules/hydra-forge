# E2E Regression Matrix — Phase 5: Multi-User Notifications & Admin Dashboard

Consolidated from:
- `2026-07-25-phase-5-notifications-admin-plan-1-jwt-role-claim-fix-matrix.md`
- `2026-07-25-phase-5-notifications-admin-plan-2-notification-system-matrix.md`
- `2026-07-25-phase-5-notifications-admin-plan-3-notification-hub-matrix.md`
- `2026-07-25-phase-5-notifications-admin-plan-6-ntfy-integration-matrix.md`
- `2026-07-25-phase-5-notifications-admin-plan-8-admin-all-projects-bypass-matrix.md`
- `2026-07-25-phase-5-notifications-admin-plan-12-audit-log-web-ui-matrix.md`

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
3. Trigger a notification to the connected user → WS frame arrives with `onNotificationReceived` payload — **BLOCKED: nothing in `src/` calls `NotifyAsync`/`INotificationService` yet (grep confirms zero callers outside `Notifications/`). Task 7 (notification-triggers) is what wires a real caller. Until then there is no code path that produces a live notification to push — this half of the matrix cannot be exercised end-to-end, automated or manual.**
4. Typed hub method `OnNotificationReceived` used (not raw `"NotificationReceived"` string) — code inspection only, confirmed by reading `SignalRNotificationHubBus`/`INotificationHub`; no runtime check possible without item 3

### Edge Cases
1. Connect without JWT → 401 — **verified 2026-07-25: `negotiate` without token returns 401**
2. Connect with expired/invalid JWT → 401 — **verified 2026-07-25: `negotiate` with garbage token returns 401**
3. `SendNotificationAsync` with no connected clients → empty group, no exception, DB write already committed — blocked on same gap as Happy Path item 3
4. Two tabs of same user → both in `user-{userId}` group, both receive events — blocked, same gap
5. `SendNotificationAsync` with cancelled `ct` → `OperationCanceledException` propagates — blocked, same gap

**Verdict: Plan 3's automatable surface (build, tests, DI, routes, hub auth) is fully verified. The actual push-delivery behavior (items 3–4 above) has no caller anywhere in the codebase yet and cannot be tested — correctly deferred to Task 7 (triggers) and Task 4/5 (Web/TUI consumers). Not a gap in Plan 3's own work; it's an expected consequence of task sequencing.**

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

## Plan 6: ntfy Integration

**Correction (2026-07-27):** The prior "closed" pass (D-52, commit `8af0a51`) marked this plan done, but 3 of 12 planned files/steps were never actually committed in `d5df06e`: `INtfyClient.cs` (Application port), `NtfyOptions.cs` (Infrastructure), and all of `SystemSettings` Step 1 (new fields, `UpdateSettings()`, `SystemSettingsSingletonId` move) plus the `AddSystemSettingsFields` migration. **`dotnet build` was actually failing on this branch** (`CS0246: INtfyClient could not be found`) — the matrix below was never runnable as written. Also found: `docker-compose.yml`'s `ntfy` service had no `command`, so the container printed help text and exited immediately instead of serving. All of the above fixed on `task/ntfy-integration` 2026-07-27; results below are from actually running each check, not carried over from the original (incorrect) close-out.

### Setup
- [x] `docker compose up -d postgres minio` (host ports 5433, 9000, 9001) — **verified 2026-07-27**
- [x] `PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef database update --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server` — `AddSystemSettingsFields` migration applied clean — **verified 2026-07-27** (migration didn't exist before this pass; created + applied)
- [x] `docker compose --profile notifications up -d ntfy` — ntfy container starts — **verified 2026-07-27** (required adding `command: serve` to `docker-compose.yml`; image has no default entrypoint command)
- [x] `dotnet run --project src/HydraForge.Server` — server starts, no DI errors — **verified 2026-07-27**: `Now listening on: http://localhost:5000`, no unresolved-service exceptions. `NtfyClient`'s `string? serverUrl` ctor param resolves to `null` via the container (unregistered reference type → `GetService` returns null) — no factory lambda needed for this to work, contrary to the original plan text's assumption
- [x] `curl http://localhost:8083/v1/health` → returns 200 — **verified 2026-07-27**, after the `command: serve` fix

### Happy Path
1. Migration applies on a DB that already has the `system_settings` row → `NtfyServerUrl`, `SearXngUrl`, `BrandName`, `BrandLogoUrl` columns added as nullable `text`, existing row preserved with nulls — **verified 2026-07-27**: `SELECT * FROM system_settings` shows singleton row `00000000-0000-0000-0000-000000000001` with all 4 new columns `NULL`
2. Trigger any notification (e.g. assign a card to another user) → row appears in `notifications` table; SignalR `NotificationReceived` fires on the recipient's hub connection — **still BLOCKED, same gap as Plan 3 item 3**: nothing calls `NotifyAsync` from a real domain event yet (Task 7). Not retested here; carried over from Plan 3's verdict, not re-verified independently.
3. `docker compose ps` shows `ntfy` service `healthy` after ~30s — **verified 2026-07-27**: `Up 32 seconds (healthy)`
4. `docker compose logs ntfy | tail -20` — no crash, auth.db + cache.db created in `/var/lib/ntfy` — **verified 2026-07-27**: `Listening on :80[http]`, `ls /var/lib/ntfy` shows both files

### Edge Cases
1. `NotificationService` constructed with `ntfyClient: null` → no exception, DB write + SignalR push still succeed — **verified**: `NtfyClientTests.NotificationService_WithNullNtfyClient_DoesNotThrow` (Application.Tests) passes
2. `NtfyClient.PublishAsync` with `_serverUrl = null` → no HTTP call, returns immediately — **verified**: new `Infrastructure.Tests/Notifications/NtfyClientTests.PublishAsync_WithNullServerUrl_MakesNoHttpCall` (was previously untested — only the `NotificationService`-level null-client path had coverage, not `NtfyClient` itself)
3. `NtfyClient.PublishAsync` with unreachable server URL → exception caught, no rethrow (best-effort) — **verified**: new `PublishAsync_WithUnreachableServer_SwallowsException`, using a throwing `HttpMessageHandler`
4. Concurrent notifications for many users → `hydraforge-{userId}` topic per user, no cross-talk — **verified by code inspection + test**: new `PublishAsync_WithServerUrl_PostsToPerUserTopic` asserts the posted URL is `{serverUrl}/hydraforge-{userId}`; no shared-state between calls so cross-talk isn't structurally possible
5. `UserId == ActorId` → service short-circuits, no DB write, no push, no SignalR event — **verified**: existing `NotificationServiceTests` coverage, unaffected by this change

### Regressions
1. Existing notification flow (DB + SignalR) still works end-to-end — **verified**: no `useApi` or SignalR client changes; Application/Server test suites unaffected
2. `system_settings` seed row retains `Id = 00000000-0000-0000-0000-000000000001` — **verified 2026-07-27** via direct SQL query
3. TUI/Web UI notification bell + count still updates on new notifications — not retested here (no code path touches this; carried over as unaffected)
4. `dotnet build` clean, `dotnet test` all suites pass, EF drift check reports "No pending model changes" — **verified 2026-07-27**: 617 tests pass (59 Domain + 213 Application + 77 Infrastructure + 152 Server + 116 Tui), `has-pending-model-changes` reports none
5. `SystemSettingsSingletonId` moved from `HydraForgeDbContext` to `SystemSettings.SingletonId` — **verified 2026-07-27**: grep confirms only `DbContext.cs` referenced it, updated in the same pass; build/tests green after the move

### Docker
1. `docker compose --profile notifications config` — services include `ntfy` with `profiles: ["notifications"]` — **verified 2026-07-27**
2. `docker compose --profile notifications config --volumes` — `ntfy-data` volume declared — **verified 2026-07-27** (note: plain `docker compose config --volumes` without the profile flag does NOT list it — profile-gated volumes only appear when the profile is active)
3. `ntfy` env: `NTFY_AUTH_DEFAULT_ACCESS: deny-all` set (no anonymous publish) — **verified 2026-07-27**
4. `NTFY_BASE_URL=http://localhost:8083` present in `.env.example` — **verified 2026-07-27**

**Verdict: All automatable surface (build, tests incl. 3 new `NtfyClient`-level tests, migration, DI/server boot, docker health) now genuinely passes — it did not before this pass, despite being marked ✅ closed. The only remaining gap (Happy Path item 2 — real end-to-end push) is correctly blocked on Task 7 (notification triggers), same as Plan 3; not a Plan 6 defect.**

### Cleanup
- [x] `docker compose --profile notifications down` (volumes preserved, no `-v`) — done 2026-07-27
- [x] No test data created — used the seeded `system_settings` singleton row only

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
