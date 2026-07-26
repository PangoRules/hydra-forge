## Validate: ntfy push client integration (Plan 6)

Scope: `INtfyClient` port + `NtfyClient` implementation + `NtfyOptions` + `SystemSettings.NtfyServerUrl` field + `AddSystemSettingsFields` migration + docker-compose `ntfy` service + `NTFY_BASE_URL` env var. `NotificationService` now optionally takes `INtfyClient?` and calls `PublishAsync` after DB write. Server URL is intentionally `null` for now (Task 10 wires `CachedSettingsProvider`), so push is a no-op until that lands.

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
3. `ITuis`/Web UI notification bell + count still updates on new notifications (real-time path unchanged)
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
