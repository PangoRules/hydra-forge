# Validate: SignalR Integration (BoardHub + PresenceHub)

## Setup
- [ ] `dotnet build` clean (zero warnings, zero errors). `SignalRConnectionManager` + `BoardScreen` diff compiles.
- [ ] `dotnet test` green across all suites (no regression from new file).
- [ ] Server running with at least one project the logged-in user is a member of.
- [ ] Two TUI sessions (or one TUI + Scalar/REST exercise) authenticated as the same user. A second browser tab signed in as another project member is even better for presence.
- [ ] Start fresh: `cd src/HydraForge.Tui && dotnet run` — log in, navigate to `ProjectListScreen` → select a project → `BoardScreen` renders.

## Connection Lifecycle
- [ ] On `BoardScreen.OnEnterAsync`: a `SignalRConnectionManager` is constructed, four event handlers are subscribed (`OnBoardEvent`, `OnCurrentUsers`, `OnUserJoined`, `OnUserLeft`), and `ConnectAsync(projectId)` runs.
- [ ] BoardHub connection successfully opens to `${ServerUrl}/hubs/board` with the JWT token from `ConfigStore`, subscribes to `OnBoardEvent`, and `InvokeAsync("JoinProject", projectId)` completes without `HubException`. Status indicator moves to `Connected`.
- [ ] PresenceHub connection opens to `${ServerUrl}/hubs/presence`, subscribes to `CurrentUsers`/`UserJoined`/`UserLeft`/`CardFocused`/`CardUnfocused`, and `InvokeAsync("JoinProject", projectId)` succeeds.
- [ ] After `JoinProject`, the server sends `CurrentUsers` to the caller (existing-users list, excluding self). `AppState.OnlineCount` reflects `users.Count` (so on a fresh board it may be 0).
- [ ] On `BoardScreen.OnExitAsync` (hit `Esc` to return to `ProjectListScreen`): `DisconnectAsync` runs, both `HubConnection.StopAsync()` + `DisposeAsync()` complete, no `NullReferenceException` from a re-entry.

## BoardHub — Real-Time Reload
- [ ] In another session (or via REST), `POST` a new card to the project. The TUI's `OnBoardEvent` fires with `EntityType="Card"`, `Action="Created"` → `HandleBoardEvent` calls `LoadBoardAsync` + `RenderAsync` → new card appears in the column without a manual refresh.
- [ ] `PATCH` (update title) on an existing card → TUI reloads and the new title shows.
- [ ] `MoveAsync` a card on the other session → TUI reloads and the card appears in the new column.
- [ ] `ArchiveAsync` a card on the other session → TUI reloads and the card disappears.
- [ ] `Delete` on a column in the other session → TUI column list shrinks.

## PresenceHub — Online Count
- [ ] When a second user joins the same project (start another TUI session or browser tab), the first session receives `UserJoined` → `AppState.OnlineCount` increments by 1.
- [ ] When the second session closes, `UserLeft` fires → `OnlineCount` decrements (clamped at 0 via `Math.Max(0, ...)`). Closing two sessions in a row must not send `OnlineCount` negative.
- [ ] `CurrentUsers` on initial join populates the count correctly without double-counting when `UserJoined` then fires for the same connection (server filters out caller-side from `CurrentUsers`).

## Reconnect
- [ ] Kill the server mid-board (or `docker compose stop server`). `_boardConnection.Reconnecting` fires → `AppState.Connection = Reconnecting`. Bring the server back. WithAutomaticReconnect retries the ladder (0, 1, 2, 5, 10, 30s). When it succeeds, `Reconnected` fires → status returns to `Connected` → `JoinProject` is re-invoked → TUI resumes receiving `OnBoardEvent`.
- [ ] **Same drop test for PresenceHub — flag if any incoming `UserJoined`/`UserLeft`/`CardFocused`/`CardUnfocused` events stop arriving after reconnect.** (Known gap: plan does not re-invoke `JoinProject` on the presence connection's `Reconnected` event — confirm whether this matters for your run; if it does, the fix is a mirrored handler on `_presenceConnection`.)
- [ ] Hard disconnect (network off, 30s+): `Closed` fires → `AppState.Connection = Disconnected`. No process crash.

## CardFocus / CardUnfocus (PresenceHub)
- [ ] Other session opens a card detail (server publishes `CardFocused`): the TUI's `OnCardFocused` handler fires — currently no UI consumes the event, but no exception is thrown and the event handler completes synchronously.
- [ ] Same for `CardUnfocused` when the other session closes the detail.

## Edge Cases
- [ ] **Connection refused** (server not running) → `ConnectAsync` throws (likely `HttpRequestException` or `StartAsync` rejection). `OnEnterAsync` propagates — the existing `LoadBoardAsync` catch does NOT cover this, so the user sees an unhandled exception. Confirm acceptable for now; the plan does not require a fallback.
- [ ] **Token expired mid-session** — first reconnect attempt will fail with 401. Connection falls to `Closed`. No infinite retry loop. (No token-refresh logic in this plan.)
- [ ] **`Guid.Empty` projectId** — `OnEnterAsync` falls back to `AppState.SelectedProjectId ?? Guid.Empty`. `JoinProject(Guid.Empty)` on the server throws `HubException("Access denied")` (no membership for empty project). Confirm this surfaces as an exception rather than a silent failure.
- [ ] **Rapid back-to-back events** (e.g. bulk card create) → multiple `LoadBoardAsync` calls overlap. Last-write-wins on `_columns`; if the screen tears, file separately. Plan does not address this.
- [ ] **`async void HandleBoardEvent` exception** — if `LoadBoardAsync` throws an unhandled exception (e.g. `TaskCanceledException` not caught by the existing `HttpRequestException`/`ApiException` handlers), the process crashes. The board handles `ApiException` + `HttpRequestException`; everything else is fatal.

## Regressions
- [ ] `BoardScreen` keyboard nav (`h/l/j/k/g/n/m/Enter/Delete/Esc/q`) still works — SignalR event handler does not block the input loop.
- [ ] `ProjectListScreen` (Plan 6) transitions to `BoardScreen` correctly — `_signalR` field is created fresh on each `OnEnterAsync`. `OnExitAsync` (via `Esc`) disconnects before the new screen takes over.
- [ ] `ConfigStore.Load()` continues to work in the SignalR path (uses no DI, just `new ConfigStore().Load()` — same pattern as elsewhere).
- [ ] `AppState.Connection` transitions (`Disconnected` → `Reconnecting` → `Connected`) are visible if any UI surface reads them.
- [ ] All tests still pass (`dotnet test`).

## Cleanup
- [ ] No test data needed; server-side projects are seed data. If you opened a second TUI session for presence tests, `q` (quit) to ensure `DisconnectAsync` runs cleanly.
