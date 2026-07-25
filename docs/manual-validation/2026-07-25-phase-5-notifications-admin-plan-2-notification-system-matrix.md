## Validate: Plan 2 — Notification System (Domain + Application + Infrastructure)

Plan scope: Domain entity factory + `MarkRead`, `INotificationRepository` port, `INotificationService` with actor-exclusion, `EfNotificationRepository`, DI registration. No UI, no SignalR — service layer only.

### Setup
- [ ] `dotnet build` succeeds (no new warnings beyond pre-existing `Microsoft.OpenApi` NU1903)
- [ ] `dotnet test` — 600 tests pass (59 Domain + 210 Application + 74 Infrastructure + 152 Server + 105 TUI)
- [ ] `dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server` returns "No changes have been made to the model since the last migration"
- [ ] `INotificationRepository` and `INotificationService` resolve from DI in `HydraForge.Server.Program.cs` (smoke: server starts without DI exception)

### Happy Path
1. `Notification.Create(userId, title, body, message, cardId, projectId, actionUrl)` returns a `Notification` with all 7 properties set, `Id != Guid.Empty`, `IsRead == false`, `CreatedAt` within last minute.
2. `notification.MarkRead()` flips `IsRead` to `true`.
3. `NotificationService.NotifyAsync(request with UserId != ActorId)` calls `INotificationRepository.AddAsync` once with a `Notification` whose `UserId` matches `request.UserId` and `Message` equals `request.Message`.
4. `EfNotificationRepository.AddAsync` persists — `db.Notifications` DbSet exists and the row is committed.

### Edge Cases
1. `Notification.Create(...)` with all optional params (`body`, `cardId`, `projectId`, `actionUrl`) null → those properties are null, no exception.
2. `NotificationService.NotifyAsync(request with UserId == ActorId)` returns early without calling `AddAsync` — actor-exclusion no-ops.
3. `NotificationService.NotifyAsync(request with Message == null)` passes `request.Title` as the `Message` to `Notification.Create`.
4. `EfNotificationRepository.ListByUserAsync(userId, skip, take, unreadOnly: true)` adds `!n.IsRead` filter; `null` skips the filter; results ordered by `CreatedAt` desc.
5. `EfNotificationRepository.MarkAsReadAsync(notificationId, userId)` where notification belongs to a different user → no-op (no row updated, no exception).
6. `EfNotificationRepository.MarkAllAsReadAsync(userId)` with zero unread → no `SaveChanges` call (early-guard prevents empty update).

### Regressions
1. `Notification` property setters are now `private set` — any pre-existing code that did `notification.IsRead = true` directly would fail to compile. Verify no other callers in `src/` mutate `Notification` properties directly (entity encapsulation rule per AGENTS.md).
2. `HydraForgeDbContext` already had `DbSet<Notification> Notifications` (parent branch) — confirmed not removed by this branch.
3. Other `Add*Services()` registrations in `Program.cs` (Auth, Project, Column, Card, Checklist, Comment, Attachment, Spec, Plan) still resolve — server boots, all 152 Server tests pass.
4. TUI and Web UI untouched — no SignalR/UI regression surface.

### Cleanup
- [ ] None required — no DB rows created, no config changed, no temp files.
