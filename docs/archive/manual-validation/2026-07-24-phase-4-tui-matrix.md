# Manual Validation Matrix — Phase 4: Project Space (TUI)

Design: `docs/archive/specs/2026-07-24-phase-4-tui-design.md`
Consolidates all per-plan matrices from `docs/manual-validation/phase-4-tui-*` (Tasks 1-13) plus the Task 14-17 wrap-up. Phase complete as of 2026-07-25.

**Gaps in original per-plan coverage** (documented rather than backfilled after the fact):
- **Task 4 (Login screen)** and **Task 9 (Card detail view)** never got a dedicated matrix file. Both are exercised as regression checks inside later matrices (Plan 5/6 regressions cover Login; Plans 11-13 regressions cover Card detail section nav). Treat those regression lines as their coverage.
- **Tasks 14-17** (this doc's final section) shipped in a documentation-and-gap-fill pass after Tasks 1-13 were already merged. Comments/Checklists turned out to be fully implemented by Task 9 already; Keyboard reference (`?`) had already been wired ahead of its plan as a bug fix; only the error panel + `LockScreen` help binding were newly written code. That pass was verified via `dotnet build` (0 errors) and `dotnet test tests/HydraForge.Tui.Tests` (105/105 passing), not a live interactive session — the matrix below is what to run the next time someone is at a real TUI session, not a record of steps already clicked through.

---

## Task 1: Scaffold (Spectre.Console + NSwag + Models)

### Setup
- [X] `dotnet tool restore` at repo root succeeds (installs `nswag.consolecore` 14.7.1)
- [X] `dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj` succeeds with 0 errors
- [X] `src/HydraForge.Tui/Generated/HydraForgeApiClient.cs` and `Contracts.cs` exist after build
- [X] API server NOT required for build (codegen fails silently, build still succeeds)
- [X] Docker Postgres+MinIO not required for this plan

### Happy Path
1. `dotnet run --project src/HydraForge.Tui` → Figlet "HydraForge" in blue renders, "TUI client starting..." line appears, any key exits cleanly with exit code 0
2. `dotnet test tests/HydraForge.Tui.Tests` → 6 ScreenStackTests pass
3. `dotnet test` (full solution) → 412 tests pass (188 Application + 6 TUI + 71 Infrastructure + 147 Server)
4. `grep -n "ProjectReference" src/HydraForge.Tui/HydraForge.Tui.csproj` → no Application reference
5. `grep -rn "Newtonsoft.Json" src/HydraForge.Tui/Generated/Contracts.cs` → properties annotated with `[Newtonsoft.Json.JsonProperty(...)]`
6. `grep -n 'int? Position { get; set; } = "0";' src/HydraForge.Tui/Generated/Contracts.cs` → no match (sed fix applied)
7. `grep -n "Stack<IScreen>" src/HydraForge.Tui/Models/ScreenStack.cs` → matches line 7

### Edge Cases
1. Build with API server not running on :5000 → NSwag codegen warns/exits non-zero, build still succeeds (`ContinueOnError=true`), `Generated/` files reflect last-good state
2. Build twice in a row on a clean checkout → second build succeeds, Generated files are idempotent
3. `dotnet ef migrations has-pending-model-changes ...` → "No changes have been made to the model since the last migration"
4. Inspect `Contracts.cs` for any `int? Position { get; set; } = "0";` defaults that escaped the sed patch → none

### Regressions
1. `dotnet build` of full solution → still builds
2. `dotnet test` of non-TUI projects → still 188 + 71 + 147 = 406 pass
3. `HydraForge.slnx` opens in IDE/editor → Tui project and Tests project both visible
4. No `using HydraForge.Application`/`Domain`/`Infrastructure`/`Server` in any non-Generated TUI source file (boundary check)
5. `git log --oneline feat/phase-4-tui..task/tui-scaffold` → only the 2 expected commits

### Cleanup
- [x] None — no DB, no file uploads, no test data created

---

## Task 2: ConfigStore

### Setup
- [ ] Build `src/HydraForge.Tui/HydraForge.Tui.csproj` successfully
- [ ] Run TUI process as current user on POSIX host

### Happy Path
1. `.hydraforge/config.json` does not exist → `ConfigStore.Load()` returns default `TuiConfig`
2. `ConfigStore.Save()` with populated server URL, JWT, expiry, refresh token → directory and JSON file created
3. Inspect saved JSON → camelCase, indented
4. Inspect saved file mode on POSIX → `0600`
5. `ConfigStore.Load()` after save → values round-trip unchanged
6. `ConfigStore.Clear()` → config file deleted

### Edge Cases
1. `ConfigStore.Clear()` when file absent → no exception, file remains absent
2. Invalid JSON in config file, then `Load()` → failure surfaced, not a silent fallback to unrelated credentials

### Regressions
1. Build TUI project → zero warnings, zero errors
2. TUI configuration/auth flow using `ConfigStore` → existing startup remains functional

### Cleanup
- [ ] Remove test config file / empty config directory if created

---

## Task 3: ApiClientFactory + AuthDelegatingHandler + ErrorCollector

### Setup
- [ ] Build succeeds
- [ ] HydraForge API running and reachable
- [ ] Valid `TuiConfig` with `JwtToken`/`ExpiresAt` persisted (Plan 2's ConfigStore)

### Happy Path
1. `ApiClientFactory.CreateClient()` → `HttpClient.BaseAddress` equals `config.ServerUrl.TrimEnd('/') + "/"`
2. First outgoing request carries `Authorization: Bearer <jwt>`
3. `AuthDelegatingHandler.SetToken(null)` → subsequent requests omit `Authorization`
4. `GetClient()` twice → same cached instance
5. `CreateUnauthenticatedClient()` → no `Authorization` header
6. Login + `RefreshTokenAsync()` → returns `true`, persisted `JwtToken`/`ExpiresAt` updated, handler's token updated
7. Authenticated call after refresh → uses new token
8. `IsTokenExpiringSoon()` with >60s left → `false`
9. `IsTokenExpiringSoon()` with <60s left → `true`
10. Add 51 errors to `ErrorCollector` → `Count == 50`, oldest evicted
11. `GetErrors()` → immutable list, insertion order
12. `Dismiss(0)` → first entry removed, `Count` decreases by 1

### Edge Cases
1. `RefreshTokenAsync()` with empty `JwtToken` → `false`, no API call, no throw
2. `RefreshTokenAsync()` against 401/5xx → `false`, no exception leaks, existing token untouched
3. `IsTokenExpiringSoon()` with `ExpiresAt == null` → `false`
4. `Dismiss(index)` out of range → no-op, no exception
5. `Dismiss(0)` on empty collector → no-op, no exception

### Regressions
1. Build → zero warnings, zero errors
2. NSwag codegen still emits `HydraForgeApiClient.RefreshAsync`
3. `ConfigStore` round-trip unaffected

### Cleanup
- [ ] Remove any test `TuiConfig`/persisted credentials written during validation

---

## Task 5: LockScreen + ConnectionManager (auto-retry)

### Setup
- [ ] Build succeeds
- [ ] Valid `TuiConfig` but no route to server (e.g. `ServerUrl=http://127.0.0.1:1`)
- [ ] `HydraForgeApiClient.HealthAsync` present

### Happy Path
1. Launch against unreachable server → `LockScreen` renders "Server Unreachable" / "Retrying..." / "Attempt N", yellow border
2. Observe ~15s → attempt counter increments, `AppState.Connection` settles to `Reconnecting`
3. Point `ServerUrl` to a running API → `LockScreen` dismisses, `AppState.Connection == Connected`
4. `ConnectionManager.CheckHealthAsync()` against running API → `true`, no exception
5. Same against unreachable host → `false`, no exception
6. `ConnectionManager.CreateLockScreen()` → wired to `CheckHealthAsync`; `Q` triggers confirm-then-quit
7. `WaitForConnectionAsync(5000)` against running API → `true` within 5s, `Connection == Connected`
8. `WaitForConnectionAsync(2000)` against unreachable host → `false` after ~2s, `Connection == Disconnected`

### Edge Cases
1. Exception inside `RetryLoopAsync` → swallowed, loop continues, counter still increments
2. Retry delays: attempt 1→5s, 2→10s, 3→30s, 4→60s, 5+→60s (cap)
3. `LockScreen.OnExitAsync()` mid-loop → `_retryCts` cancelled, no throw
4. `q` + Confirm "No" → loop keeps running
5. `q` + Confirm "Yes" → `Environment.Exit(0)`
6. Any non-`q` key → no-op
7. `WaitForConnectionAsync(timeoutMs: 0)` against unreachable host → `false` immediately, no `Task.Delay` spin

### Regressions
1. `LoginScreen` (Task 4) still renders, accepts credentials, transitions on success
2. `ApiClientFactory.GetClient()` still cached after Plan 5 changes
3. `HealthAsync` still present in generated client
4. `AppState.Connection` still `ConnectionStatus` enum
5. `IScreen` contract unchanged

### Cleanup
- [ ] Restore `TuiConfig.ServerUrl` to the real API URL

---

## Task 6: ProjectListScreen (list, create, search, archived filter, sort, role filter, pagination)

### Setup
- [ ] Build succeeds
- [ ] Valid JWT (via Task 4 login flow), `ServerUrl` pointing at a running API with existing projects
- [ ] Generated client typed against corrected enum schema (D-50) — `MemberRole`, `ColumnTemplate` real C# enums

### Happy Path
1. Launch with valid session → table of user's projects (`#`, Name, Members, Role, Created) + footer "N projects total"
2. `j`/`↓` repeatedly → highlight moves down, stops at last row
3. `k`/`↑` repeatedly → highlight moves up, stops at first row
4. `g` → first row. `Shift+G` → last row. `Home`/`End` same
5. `c` → prompts name/description/template → `POST /api/projects` fires, list reloads with new project
6. `/` → search string → server-side filtered reload, header shows `Filter: "..."`
7. `a` → toggles `includeArchived`, archived projects show `(archived)` badge + `A` marker; `a` again reverts
8. `Enter` on a row → `AppState.SelectedProjectId` set, transitions to board (placeholder note from original session no longer applies — board ships same phase)
9. `q` → confirm prompt → exit on Yes, stays on No

### Pagination, Sort, Role Filter
10. >20 projects → footer `Showing 1-20 of N projects (page 1/M)`; `n`/`p` page forward/back, no-op past bounds
11. Header `Sort: CreatedAt ↓   Role: All` by default; `s` cycles `CreatedAt → UpdatedAt → Name → CreatedAt`; `Shift+S` toggles direction
12. `r` cycles `Role: All → Owner → Member → All`
13. Any filter change resets to page 1 + top row selection
14. Reopening `/` pre-fills current filter value

### Error Visibility
15. Force a load failure → red "Errors (N)" panel renders with message/timestamp/correlationId; footer gains dismiss hint
16. `x` with errors present → clears panel
17. Failed create-project → error recorded and rendered, not swallowed

### Regressions
1. `LoginScreen`/`LockScreen`/`ConnectionManager` (Tasks 4-5) still behave per their own matrices
2. `ApiClientFactory.GetClient()` still cached and authenticated
3. `IScreen` contract unchanged
4. `HydraForge.Tui.csproj` has no `ProjectReference` to Domain/Application

### Cleanup
- [ ] Delete/archive any test projects created while validating

---

## Task 7: BoardScreen (columns + cards + keyboard navigation + Esc back)

### Setup
- [ ] Clean build. Generated client has `ProjectsGET2Async`, `CardsGETAsync`, `CardsPOSTAsync`, `MoveAsync`, `ArchiveAsync`
- [ ] Server has a project with ≥3 columns, one with a `WIPLimit`, one with cards, one empty
- [ ] Logged in, on `ProjectListScreen`, `Enter` transitions to `BoardScreen`

### Layout
- [ ] Three rows: Title (project name + counts), Board (per-column panels), Status (placeholder pre-Task-17)
- [ ] Title/Status fixed at 3 lines; Board row gets remaining height (regression guard: previously `SplitRows` with no `Size` split evenly and squeezed the board)
- [ ] Column header `<Name> (<count>)`, or `(N/Limit)` when WIP-limited
- [ ] Card shows colored type badge (T cyan1 / I red / G yellow / D green / ? grey), `#Number`, truncated title (≤25 chars), grey assignee initials
- [ ] Blocked cards prefix with 🔴

### Navigation
- [ ] `h`/`←` — prev column, card selection resets to 0, border colors swap
- [ ] `l`/`→` — next column
- [ ] `j`/`↓` — next card, stops at last
- [ ] `k`/`↑` — prev card, stops at 0
- [ ] `g` — first column/card. `Shift+G` — last column
- [ ] `n` — prompts title + type, new card appears in selected column after reload
- [ ] `m` — prompts target column, `MoveAsync` fires with cached `Version`, board reloads
- [ ] `Enter` — opens card detail (Task 9)

### Esc
- [ ] `Esc` → new `ProjectListScreen` instantiated, `SelectedProjectId` cleared, reloads, next keypress handled immediately (no dead key)
- [ ] `Esc` then `q` → confirm → exit
- [ ] `Esc` twice in a row → no exception, no stale screen reference

### Edge Cases
- [ ] Zero columns → title+status only, nav keys no-op, `n`/`m`/`Delete`/`Enter` early-return
- [ ] Column with no cards → `(0)` header, `j` no-op
- [ ] Column with no WIP limit → `(N)` not `(N/0)`
- [ ] Column name with `[`/`]` → renders literally via `Markup.Escape`
- [ ] Card type outside known enum → grey `?` badge, no crash
- [ ] Server killed mid-board → `HttpRequestException` caught, error added to `ErrorCollector`, last-good state stays rendered

### Regressions
- [ ] `ProjectListScreen` → board transition unaffected
- [ ] `LockScreen`/`LoginScreen` still use same `IScreen`/`AppState.CurrentScreen = null` lifecycle
- [ ] `BoardScreen.OnEnterAsync` re-fetches; `OnExitAsync` no-ops (pre-SignalR)
- [ ] `BoardRenderer` uses `BorderStyle` not deprecated `BorderColor`
- [ ] `dotnet test` green

### Cleanup
- [ ] Archive any projects created for type-badge enumeration tests

---

## Task 8: SignalR Integration (BoardHub + PresenceHub)

### Setup
- [ ] Clean build, `dotnet test` green
- [ ] Server running, logged-in user is project member
- [ ] Two sessions authenticated as same user (or +1 as another member for presence)

### Connection Lifecycle
- [ ] `BoardScreen.OnEnterAsync` constructs `SignalRConnectionManager`, subscribes `OnBoardEvent`/`OnCurrentUsers`/`OnUserJoined`/`OnUserLeft`, calls `ConnectAsync(projectId)`
- [ ] BoardHub opens to `/hubs/board` with JWT, `JoinProject` completes without `HubException`, status → `Connected`
- [ ] PresenceHub opens to `/hubs/presence`, subscribes presence events, `JoinProject` succeeds
- [ ] `CurrentUsers` after join populates `AppState.OnlineCount` (excludes self)
- [ ] `OnExitAsync` (via `Esc`) → `DisconnectAsync` stops+disposes both hubs, no `NullReferenceException` on re-entry

### BoardHub — Real-Time Reload
- [ ] Card created elsewhere → `OnBoardEvent` fires → reload → new card appears without manual refresh
- [ ] Card title updated elsewhere → reload shows new title
- [ ] Card moved elsewhere → reload shows new column
- [ ] Card archived elsewhere → reload removes it
- [ ] Column deleted elsewhere → column list shrinks

### PresenceHub — Online Count
- [ ] Second user joins → `OnlineCount` increments by 1
- [ ] Second session closes → `OnlineCount` decrements, clamped at 0 (no negative from two closes in a row)
- [ ] `CurrentUsers` on join doesn't double-count with a subsequent `UserJoined` for the same connection

### Reconnect
- [ ] Kill server mid-board → `Reconnecting` fires → `Connection = Reconnecting`; server back → `Reconnected` fires → `Connected` → `JoinProject` re-invoked → events resume
- [ ] **Known gap:** PresenceHub's `Reconnected` doesn't re-invoke `JoinProject` — confirm whether presence events resume after reconnect; if not, needs a mirrored handler
- [ ] Hard disconnect (30s+) → `Closed` fires → `Disconnected`, no crash

### CardFocus / CardUnfocus
- [ ] Other session opens card detail → `OnCardFocused` fires, no UI consumer yet, no exception
- [ ] Same for `CardUnfocused`

### Edge Cases
- [ ] Server not running → `ConnectAsync` throws, propagates out of `OnEnterAsync` (existing catch doesn't cover this — confirm acceptable)
- [ ] Token expired mid-session → reconnect fails 401 → `Closed`, no infinite retry
- [ ] `Guid.Empty` projectId → `JoinProject` throws `HubException("Access denied")`
- [ ] Rapid back-to-back events → overlapping `LoadBoardAsync` calls, last-write-wins
- [ ] Unhandled exception in `HandleBoardEvent` beyond `ApiException`/`HttpRequestException` → process crashes (known limitation)

### Regressions
- [ ] Board keyboard nav unaffected by SignalR handlers
- [ ] `ProjectListScreen` → `BoardScreen` transition still creates fresh `_signalR` each `OnEnterAsync`
- [ ] `ConfigStore.Load()` unaffected
- [ ] `dotnet test` green

### Cleanup
- [ ] `q` (quit) any extra sessions opened for presence tests to ensure `DisconnectAsync` runs

---

## Task 10: Card CRUD Keyboard Actions

### Setup
- [ ] API + TUI running, authenticated
- [ ] Board with ≥2 columns, ≥3 cards in one column
- [ ] One card has dependencies making unconfirmed movement return `409`

### Happy Path
1. `e` on selected card → title prompt with current title as default
2. Non-empty title → card title updates, board rerenders
3. `r` → reorder hint visible, board not cleared
4. `j`/`k` in reorder mode → selection moves within column
5. `Enter` → reorder persists, mode exits, board reloads
6. `r` then `Esc` → reorder mode exits without leaving board

### Edge Cases
1. `r` on empty board → no exception, mode doesn't start
2. `r` in empty column → same
3. `e` while reordering → prompt does not open
4. `r` while reordering → not re-entered, no duplicate hint
5. Dependency-blocked reorder → 409 warning stays visible, mode exits
6. Unknown card-type value during title edit → falls back to `Task`, no crash

### Regressions
1. `Enter` outside reorder mode → card detail opens
2. `Esc` outside reorder mode → project list opens
3. `n` → create still works
4. `m` → move prompt still works
5. Dependency-blocked normal move → warning still surfaces
6. `Delete` + confirm → archive still works

### Cleanup
- [ ] Restore edited title/order, remove test cards

---

## Task 11: Dependency Panel

### Setup
- [X] API + TUI running, authenticated
- [X] Board with ≥2 cards
- [X] Card detail view reachable (to verify `d` there too)

### Happy Path
1. `d` on a card (board) → Dependency Panel modal renders
2. Partial card number → live-updating results (≤5, source filtered out)
3. `Tab` → Search → Type → Confirm, blue border highlight
4. `Shift+Tab` → reverse
5. Type focused, `j`/`k` → `BlockedBy → Precedes → Relates → SpawnedFrom`, wraps
6. Type focused, `l`/`h` → no-op (reserved for parent columns)
7. Search focused with results, `j`/`k` → cursor moves/wraps
8. Search focused, `Enter` → relationship created, modal dismisses
9. Confirm focused, `Enter` → relationship created with highlighted type
10. Dismiss → board re-renders, no duplicate SignalR handlers
11. Card detail: `d` → panel opens with card as source
12. Pick target, confirm → relationship created; message in `ErrorCollector` (surfaced via Task 17 status bar)

### Edge Cases
1. Whitespace-only search → results clear, cursor resets
2. Zero-match search → Confirm+Enter no-ops
3. `Backspace` to empty → results clear
4. Confirm+Enter with empty results → red hint, modal stays open
5. `Esc` from any focus state → dismiss, no relationship created
6. Server 4xx → error to `ErrorCollector`, modal stays open
7. Server unreachable → connection error to `ErrorCollector`, modal stays open
8. `Ctrl+Q` while open → process exits
9. `d` on empty column → no panel, no exception
10. `d`, dismiss, `d` again → clean reopen, no duplicated SignalR subscriptions

### Regressions
1-11. Board nav (`h/l/j/k/Enter/n/m/r/Esc`), SignalR reconnect status, card detail `Tab`/`Shift+Tab` section nav, help overlay showing `[d] Add dependency` — all unaffected

### Cleanup
- [ ] Remove test relationships, restore board/card state

---

## Task 12: Blocked Card Indicator on Board

### Setup
- [ ] API + TUI running, authenticated
- [ ] Board with ≥3 cards (1 blocker, 2 blocked)
- [ ] One `BlockedBy` relationship, one non-`BlockedBy` (negative case)

### Happy Path
1. `r` refresh → blocked source shows 🔴, blocker does not
2. Navigate away/back (`h`/`l`) → indicator persists across re-render
3. Two cards blocked by same blocker → both show 🔴, blocker doesn't
4. Chain (A `BlockedBy` B, B `BlockedBy` C) → A and B show 🔴, C doesn't
5. Delete `BlockedBy` → next refresh clears 🔴

### Edge Cases
1. No relationships → no 🔴
2. Only `Precedes`/`Relates`/`SpawnedFrom` → no 🔴
3. Self-referential `BlockedBy` → 🔴 shown, no crash
4. ≥20 cards → all indicators correct, no visible slowdown
5. 5xx/network failure fetching relationships → no 🔴 anywhere, no crash, no unhandled toast
6. Empty `Relationships` array → no indicator, no exception

### Regressions
1-10. Board nav, card detail, move-with-409, dependency panel default type, help overlay — all unaffected; SignalR reconnect still re-derives indicators

### Cleanup
- [ ] Delete test relationships and temporary cards

---

## Task 13: Spec + Plan Viewer/Editor

### Setup
- [ ] API + TUI running, authenticated
- [ ] Card with one spec, one editable plan, one Done plan, non-empty descriptions
- [ ] `$EDITOR`/`$VISUAL` set to a working terminal editor

### Happy Path
1. `s` on card detail → Specs/Plans chooser
2. Select Specs → list with title/type/version/updated timestamp
3. Select + `Enter` → escaped markdown renders, truncated at 1,000 chars with `...`
4. `e`, edit, save → API persists, title/description untouched, version increments
5. `Esc` → card detail reloads
6. Select Plans → Pending/Active/Done badges
7. `e` on editable plan, save → persists, version increments
8. `c` in Specs mode → creates spec, appears in refreshed list
9. `c` in Plans mode → creates plan at next position

### Edge Cases
1. No specs/plans → empty-state + `c`/`Esc` hints
2. `e` on Done plan → editor doesn't launch, "Cannot edit a completed plan.", version unchanged
3. Save without changes → no request, "No changes.", version unchanged
4. No/failing `$EDITOR` → inline prompt fallback, saves still work
5. `[`/`]` in title/content → escaped, no crash/unintended styling
6. API down during load/create/update → error captured, TUI stays running

### Regressions
1. `Esc` from Specs/Plans → same card detail, not board/project list
2. `d` from card detail → dependency panel still works
3. Card title/description edit unaffected
4. `j`/`k`/arrows in spec/plan list stay within bounds

### Cleanup
- [ ] Restore edited content, remove test documents

---

## Tasks 14-17: Comments, Checklists, Keyboard Reference, Status Bar

Shipped together in a gap-fill pass (2026-07-25) after discovering Tasks 14 (comments) and 15 (checklists) were already fully implemented by Task 9's `CardDetailScreen`, and the `?` keyboard-reference overlay had already been built ahead of its plan as a bug fix (`Rendering/HelpOverlay.cs`, wired into Board/CardDetail/ProjectList). Only genuinely new code this pass: `LockScreen`'s `?` binding and the `ErrorPanelScreen` + `X` key on `BoardScreen`. Verified via `dotnet build` (0 errors) + `dotnet test tests/HydraForge.Tui.Tests` (105/105 passing) — **not yet run interactively**; treat all rows below as open.

### Setup
- [ ] API + TUI running, authenticated, on a card with ≥1 checklist item and existing comments
- [ ] Errors present in `ErrorCollector` (e.g. stop the API briefly to generate one)

### Task 14 — Comments
1. Card detail, comments section active, `a` → prompt for comment text, `POST`s to API, list reloads with new entry (author, timestamp, content)
2. Comment count/order matches server after reload

### Task 15 — Checklists
3. Card detail, checklist section active, `Space` → toggles first incomplete item (or first item), `PATCH`es API, `[x]`/`[ ]` + strikethrough update after reload

### Task 16 — Keyboard Reference
4. `?` on `ProjectListScreen`/`BoardScreen`/`CardDetailScreen` → overlay renders that screen's bindings, dismiss with `?` or `Esc`, parent screen re-renders correctly
5. `?` on `LockScreen` (new this pass) → overlay renders `q`/`?` bindings, dismiss returns to lock screen unaffected (retry loop keeps running underneath)
6. `LoginScreen` has no `?` binding by design — it's a sequential blocking-prompt flow with no key-dispatch loop to hang an overlay off; not a gap, a deliberate scope boundary

### Task 17 — Status Bar / Error Panel
7. Status bar (board bottom row) shows connection dot (green Connected / yellow Reconnecting / red Disconnected), online count, error count — confirm all three update live as state changes
8. `X` on `BoardScreen` (new this pass) → `ErrorPanelScreen` opens listing `ErrorCollector` entries (time, correlationId, message)
9. `j`/`k` in error panel → selection moves, wraps
10. `Del` on selected error → removed from list and from `ErrorCollector`
11. `?` in error panel → help overlay for its own bindings
12. `Esc` → returns to `BoardScreen`, status bar error count reflects any dismissals
13. Unread-notification count is **out of scope** — `AppState.UnreadNotifications` exists but is never populated; no notification API surface exists in the generated client yet. This is Phase 5 (ntfy integration) work, not a Phase 4 gap — confirmed via `docs/functional-spec.md` Phase 5 checklist ("bell icon + unread count in TUI status bar").

### Regressions
1. `E` still opens title/description/document edit (Board/CardDetail/SpecViewer) — confirm `X` doesn't collide with any existing binding
2. Board/CardDetail/ProjectList `?` overlays unaffected by the `LockScreen` addition
3. `dotnet test` full TUI suite green (105/105 at time of writing)

### Cleanup
- [ ] Dismiss any test errors added to `ErrorCollector` during validation
