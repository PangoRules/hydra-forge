## Validate: Plan 3 — NotificationHub + SignalR Push

Plan scope: `INotificationHubBus` port (Application), `INotificationHub` typed hub contract + `NotificationReceivedEvent` record, `NotificationHub` SignalR hub in Infrastructure, `SignalRNotificationHubBus` implementation, DI registration, hub mapping at `/hubs/notifications` + JWT query-string token extraction, `NotificationService` wired to broadcast after DB write, Application tests with `FakeNotificationHubBus`. Plan did NOT include automated SignalR integration tests — manual smoke is the contract.

### Setup
- [ ] `dotnet build` succeeds (no new warnings beyond pre-existing `Microsoft.OpenApi` NU1903)
- [ ] `dotnet test` — all tests pass (target ≥ 543: 59 Domain + 212 Application + 74 Infrastructure + 152 Server + 105 TUI; Application count grew by 2 with the new `FakeNotificationHubBus` cases)
- [ ] `app.MapHub<NotificationHub>("/hubs/notifications")` registered in `Program.cs` alongside `/hubs/board` and `/hubs/presence`
- [ ] `/hubs/notifications` added to `OnMessageReceived` JWT query-string token extraction list (allows `?access_token=...` for WebSocket auth)
- [ ] `INotificationHubBus` registered as scoped in `AddRealtimeServices` (so `NotificationService` resolves it)
- [ ] Server starts without DI exception (`NotificationService` now requires `INotificationHubBus` — any missing registration would surface as 500 from the first notification trigger)

### Happy Path (manual — SignalR push)
1. Start server (`dotnet run --project src/HydraForge.Server`) with `ASPNETCORE_ENVIRONMENT=Development`.
2. Open browser DevTools → Network → WS. Connect to `ws://localhost:5000/hubs/notifications?access_token=<userA JWT>`.
3. WS handshake completes; `OnConnectedAsync` adds connection to group `user-{userA-id}`. No errors in server log.
4. From a second client (curl + admin auth), trigger a notification to user A (e.g. assign user A to a card in a project they're a member of, or call any service path that invokes `INotificationService.NotifyAsync`).
5. Browser DevTools WS frame for user A shows incoming `OnNotificationReceived` event with payload `{ Id, Title, Body, CardId, ProjectId, ActionUrl, CreatedAt, IsRead }`. All 8 fields present.
6. The frame is the typed-hub method `OnNotificationReceived`, NOT the string `"NotificationReceived"` — the wire method name comes from the typed `INotificationHub.OnNotificationReceived` contract.

### Happy Path (Application — automated, covered by tests)
1. `NotificationService.NotifyAsync(request with UserId != ActorId)` calls `_notifRepo.AddAsync` THEN `_hubBus.SendNotificationAsync(request.UserId, notif, ct)`. Fake bus records `(UserId, Notification)` tuple; assertion verifies a single send with matching `UserId` and `Title`. (Test: `NotifyAsync_WhenDifferentUser_SendsToHubBus`)
2. `NotificationService.NotifyAsync(request with UserId == ActorId)` returns early before either call — both `repo.Added` and `hubBus.Sent` empty. (Tests: `NotifyAsync_WhenUserIdEqualsActorId_DoesNotAddNotification` + `..._DoesNotSendToHubBus`)
3. `NotificationService.NotifyAsync(request with Message == null)` passes `request.Title` as `Message` to `Notification.Create` (existing test still passes after `INotificationHubBus` injection).

### Edge Cases
1. User connects to `/hubs/notifications` WITHOUT a valid JWT (no `?access_token=...` and no auth header) → `[Authorize]` attribute rejects the connection; WS handshake returns 401. `OnConnectedAsync` never runs, user never joins `user-{userId}` group, never receives events.
2. User connects with expired/invalid JWT → handshake 401, same as above.
3. `NotificationService.NotifyAsync` invoked with `UserId != ActorId` and bus is registered as fake in test → fake records the call. With real `SignalRNotificationHubBus` and no connected clients for the target user → `Clients.Group($"user-{userId}")` resolves to empty set, no exception, DB write already committed. (Connection-loss resilience: notification persists, client sees it on next fetch even if offline.)
4. Two browser tabs of same user A connect simultaneously → both join group `user-{userA-id}`; a single notification broadcast reaches both connections.
5. `SignalRNotificationHubBus.SendNotificationAsync` called with `ct` that is already cancelled → `IHubContext` broadcast throws `OperationCanceledException`; `NotificationService` does not wrap in try/catch so the exception propagates out of `NotifyAsync`. Acceptable: hub failure should not silently swallow DB write that already succeeded (the row is persisted; client just won't get the real-time push).

### Regressions
1. `BoardHub` still mapped at `/hubs/board`, `PresenceHub` still mapped at `/hubs/presence` — `MapHub` calls in `Program.cs` unchanged.
2. `IBoardHub.OnBoardEvent` typed contract still wired (BoardHub + SignalRProjectBoardEventPublisher unchanged). Adding `INotificationHub` is additive — does not modify existing hub contracts.
3. `NotificationService` constructor signature changed (added `INotificationHubBus hubBus` parameter). Every test factory in `tests/HydraForge.Server.Tests/` that wires `NotificationService` must register a fake `INotificationHubBus` — verify no Server test now fails with DI resolution error (152 Server tests pass confirms this).
4. `Notification` entity unchanged — DB migration drift check should remain clean: `dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server` returns "No changes have been made to the model since the last migration".
5. JWT query-string token extraction now matches three path prefixes (`/hubs/board`, `/hubs/presence`, `/hubs/notifications`). Board and Presence WebSocket auth still works (existing 152 Server tests + manual board interaction).

### Plan Deviations (informational, not bugs)
1. `NotificationHub.cs` placed in `src/HydraForge.Infrastructure/Realtime/` instead of plan-specified `src/HydraForge.Server/Hubs/`. Matches existing `BoardHub` location and typed-hub convention. `PresenceHub` (untyped) stays in `src/HydraForge.Server/Hubs/`. Two hub conventions coexist — intentional.
2. Implementation uses typed `INotificationHub.OnNotificationReceived(...)` instead of plan's untyped `SendAsync("NotificationReceived", ...)`. Same wire format (typed hub method names map to camelCase event names by default — `OnNotificationReceived` → `onNotificationReceived`); added compile-time type safety and consistency with `IBoardHub.OnBoardEvent`. Client subscription would be `hubConnection.on("onNotificationReceived", handler)` — note the lowercase `n` in `on`.
3. `INotificationHub` and `NotificationReceivedEvent` created in `src/HydraForge.Application/Realtime/` (parallel to `IBoardHub` + `ProjectBoardEventEnvelope`). Plan did not list these but they are necessary for the typed-hub improvement above.

### Cleanup
- [ ] None required — no DB schema changes, no config changes, no test data persisted by validation steps.