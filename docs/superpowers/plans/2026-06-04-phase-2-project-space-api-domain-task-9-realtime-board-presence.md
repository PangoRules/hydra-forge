# Task 9: BoardHub mutation broadcasts and ephemeral PresenceHub
**Branch:** `task/realtime-board-presence`
**Parent branch:** `feat/phase-2-project-space-api-domain`
**Parent spec:** `2026-06-04-phase-2-project-space-api-domain-design.md`

**Goal:** Add SignalR board mutation invalidation events after successful commits and ephemeral presence join/leave/card-focus without DB writes.

**Files:** Modify/read `Program.cs`, `DomainErrorCodes.cs`, project/card services from prior tasks after merge. Create `src/HydraForge.Application/Realtime/*`, `src/HydraForge.Server/Hubs/BoardHub.cs`, `PresenceHub.cs`, `src/HydraForge.Infrastructure/Realtime/SignalRProjectBoardEventPublisher.cs` if Infrastructure owns publisher adapter, tests, smoke `src/HydraForge.Server/HttpTests/Realtime.http`.

## Steps

- [ ] Add Application models: `ProjectBoardEventEnvelope(eventId, projectId, entityType, entityId, action, version, occurredAt, payload)` and `IProjectBoardEventPublisher`.
- [ ] Write Application tests with fake publisher: mutation services publish only after repository commit succeeds; no publish on expected failure.
- [ ] Implement SignalR hubs in Server. `BoardHub.JoinProject(projectId)` checks membership/admin before `Groups.AddToGroupAsync(ProjectGroup(projectId))`. `PresenceHub` tracks in-memory concurrent dictionary by project and connection id; broadcasts join/leave/card-focus; no repository/DbContext writes.
- [ ] Register SignalR in `Program.cs`: `builder.Services.AddSignalR(); app.MapHub<BoardHub>("/hubs/board"); app.MapHub<PresenceHub>("/hubs/presence");` after auth.
- [ ] Implement publisher adapter that sends `BoardEvent` to project group. Ensure task services call publisher after transaction/SaveChanges. If service lacks unit-of-work hook, repository method returns committed result then service publishes.
- [ ] Write Server tests for unauthorized hub connect/join denial where practical; unit-test `PresenceService` directly for join/focus/disconnect cleanup and no audit/DB dependency.
- [ ] Create `src/HydraForge.Server/HttpTests/Realtime.http` documenting SignalR negotiate URLs and HTTP mutations that should emit board events. `.http` cannot open WebSocket reliably; include smoke GET/POST examples plus comments with expected hub methods.
- [ ] Run `dotnet test --filter Realtime`; `dotnet test`.
- [ ] Commit: `git add src tests && git commit -m "feat: add realtime board presence"`.

**Acceptance:** Board events emitted after commit only; presence has no DB writes/audit rows; `.http` documents smoke flow for endpoints/mutations that trigger events.
