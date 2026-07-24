## Validate: LockScreen + ConnectionManager (auto-retry)

### Setup
- [ ] Build `src/HydraForge.Tui/HydraForge.Tui.csproj` successfully (zero warnings, zero errors).
- [ ] Have a valid `TuiConfig` (use `ConfigStore` from Plan 2), but no route to the server — easiest is to start the TUI with `ServerUrl` set to a port that nothing is bound on (e.g. `http://127.0.0.1:1`).
- [ ] `HydraForgeApiClient.HealthAsync` generated method exists (Plan 3 / NSwag codegen present).

### Happy Path
1. Launch TUI against unreachable server → `LockScreen` renders a centered panel with "Server Unreachable", "Retrying...", and "Attempt N" lines, yellow border, "HydraForge" header.
2. Observe retry loop for ~15s → `Attempt` counter increments and "Retrying..." stays visible; `_appState.Connection` settles to `Reconnecting`.
3. While server is reachable (point `ServerUrl` to a running API), start TUI → `LockScreen` shows attempt 1, then dismisses (caller clears the lock screen) and `AppState.Connection == Connected`.
4. Call `ConnectionManager.CheckHealthAsync()` against a running API → returns `true`; no exception leaks.
5. Call `ConnectionManager.CheckHealthAsync()` against an unreachable host → returns `false`; no exception leaks.
6. Call `ConnectionManager.CreateLockScreen()` → returns a `LockScreen` instance wired to `CheckHealthAsync` (Q key on the screen triggers a confirm-then-quit path).
7. Call `ConnectionManager.WaitForConnectionAsync(timeoutMs: 5000)` against running API → returns `true` within 5s; `AppState.Connection == Connected`.
8. Call `ConnectionManager.WaitForConnectionAsync(timeoutMs: 2000)` against unreachable host → returns `false` after ~2s; `AppState.Connection == Disconnected`.

### Edge Cases
1. `LockScreen` retry loop: catch any exception inside `RetryLoopAsync` → swallowed, loop continues, attempt counter still increments.
2. `LockScreen` `RetryLoopAsync` delays: attempt 1 → 5s, 2 → 10s, 3 → 30s, 4 → 60s, 5+ → 60s (cap). Verify by inspecting timing or stubbing `Task.Delay`.
3. Calling `LockScreen.OnExitAsync()` while retry loop is running → `_retryCts` cancelled, loop exits without throwing.
4. Press `q` on `LockScreen` with `AnsiConsole.Confirm` answered "No" → loop keeps running, no `Environment.Exit`.
5. Press `q` on `LockScreen` with `AnsiConsole.Confirm` answered "Yes" → process exits via `Environment.Exit(0)`.
6. Press any non-`q` key on `LockScreen` → no-op, retry loop unaffected.
7. `WaitForConnectionAsync` with `timeoutMs: 0` against unreachable host → returns `false` immediately, no `Task.Delay` spin.

### Regressions
1. `LoginScreen` (Plan 4) still renders, accepts credentials, and transitions on successful login.
2. `ApiClientFactory.GetClient()` (Plan 3) still returns a cached `HydraForgeApiClient` after Plan 5 changes.
3. NSwag-generated `HydraForgeApiClient.HealthAsync` is still present in `src/HydraForge.Tui/Generated/`.
4. `AppState.Connection` property is still `ConnectionStatus` enum (`Connected` / `Reconnecting` / `Disconnected`).
5. `IScreen` interface contract unchanged: `OnEnterAsync`, `OnExitAsync`, `RenderAsync`, `HandleKeyAsync` (Plan 1).

### Cleanup
- [ ] Restore `TuiConfig.ServerUrl` to the real API URL.
