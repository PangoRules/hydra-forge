# E2E Regression Matrix — Phase 5: Multi-User Notifications & Admin Dashboard

Consolidated from:
- `2026-07-25-phase-5-notifications-admin-plan-1-jwt-role-claim-fix-matrix.md`
- `2026-07-25-phase-5-notifications-admin-plan-2-notification-system-matrix.md`
- `2026-07-25-phase-5-notifications-admin-plan-3-notification-hub-matrix.md`
- `2026-07-25-phase-5-notifications-admin-plan-6-ntfy-integration-matrix.md`

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

### Setup
- [ ] `git pull` on `task/ntfy-integration`
- [ ] `docker compose up -d postgres minio` (host ports 5433, 9000, 9001)
- [ ] `PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef database update --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server` — `AddSystemSettingsFields` migration applies clean
- [ ] `docker compose --profile notifications up -d ntfy` — ntfy container starts
- [ ] `dotnet run --project src/HydraForge.Server` — server starts, no DI errors
- [ ] `curl http://localhost:8083/v1/health` → returns 200

### Happy Path
1. Migration applies on a DB that already has the `system_settings` row → `NtfyServerUrl`, `SearXngUrl`, `BrandName`, `BrandLogoUrl` columns added as nullable `text`, existing row preserved with nulls
2. Trigger any notification (e.g. assign a card to another user) → row appears in `notifications` table; SignalR `NotificationReceived` fires on the recipient's hub connection
3. `docker compose ps` shows `ntfy` service `healthy` after ~30s
4. `docker compose logs ntfy | tail -20` — no crash, auth.db + cache.db created in `/var/lib/ntfy`

### Edge Cases
1. `NotificationService` constructed with `ntfyClient: null` (test path + DI resolution without `INtfyClient` registered) → no exception, DB write + SignalR push still succeed (covered by `NtfyClientTests.NotificationService_WithNullNtfyClient_DoesNotThrow`)
2. `NtfyClient.PublishAsync` with `_serverUrl = null` → no HTTP call, returns immediately
3. `NtfyClient.PublishAsync` with unreachable server URL → exception caught, no rethrow (best-effort)
4. Concurrent notifications for many users → `hydraforge-{userId}` topic per user, no cross-talk
5. `UserId == ActorId` → service short-circuits, no DB write, no push, no SignalR event (existing behavior preserved)

### Regressions
1. Existing notification flow (DB + SignalR) still works end-to-end — no `useApi` repo or SignalR client change
2. `system_settings` seed row retains `Id = 00000000-0000-0000-0000-000000000001` — verify with `SELECT * FROM system_settings;`
3. TUI/Web UI notification bell + count still updates on new notifications (real-time path unchanged)
4. `dotnet build` clean, `dotnet test` all suites pass (74 Infra + 152 Server + 116 Tui + Application), EF drift check reports "No pending model changes"
5. `SystemSettingsSingletonId` static field moved from `HydraForgeDbContext` to `SystemSettings` entity — no callers referenced the old location (grep confirmed: only `DbContext.cs` and `SystemSettings.cs`)

### Docker
1. `docker compose --profile notifications config` — services include `ntfy` with `profiles: ["notifications"]`
2. `docker compose config --volumes` — `ntfy-data` volume declared
3. `ntfy` env: `NTFY_AUTH_DEFAULT_ACCESS: deny-all` set (no anonymous publish)
4. `NTFY_BASE_URL=http://localhost:8083` present in `.env.example`

### Cleanup
- [ ] `docker compose --profile notifications down -v` (or leave ntfy-data if you want to keep published messages)
- [ ] No test data to remove — use throwaway user/card
