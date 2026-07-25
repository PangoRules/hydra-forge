## Validate: BoardScreen (columns + cards + keyboard navigation + Esc back)

### Setup
- [ ] Build `dotnet build` clean (zero warnings, zero errors). NSwag codegen produces `HydraForgeApiClient` with `ProjectsGET2Async`, `CardsGETAsync`, `CardsPOSTAsync`, `MoveAsync`, `ArchiveAsync` (renamed from earlier `CardsXxxAsync` per D-50+ codegen).
- [ ] Server running with at least one project containing ≥3 columns, where at least one column has a `WIPLimit` set, one column has cards, and one column has no cards.
- [ ] TUI logged in (Plan 4) and on `ProjectListScreen` (Plan 6). Selecting a project with `Enter` transitions to `BoardScreen` and renders the layout.

### Happy Path — Layout
1. Board renders with three rows: Title panel (project name + "N columns  M cards"), Board row (one panel per column side-by-side), Status panel placeholder.
2. Each column panel shows a rounded header `<ColumnName> (<cardCount>)` — e.g. `Backlog (3)`, `In Progress (5/8)` when `WIPLimit` is set, `Done (2)` otherwise.
3. Each card has a coloured type badge (`T` cyan1, `I` red, `G` yellow, `D` green, `?` grey fallback), `#<CardNumber>`, truncated title (≤25 chars), and grey assignee initials suffix.
4. Blocked cards prefix with a 🔴 glyph.

### Happy Path — Navigation
5. Press `h`/`←` → selection moves to the previous column, `_selectedCard` resets to 0; new column header border is blue, previous column reverts to its configured colour.
6. Press `l`/`→` → next column; selected column border is blue, others use their configured colour.
7. Press `j`/`↓` → card selection moves down within the current column (stops at last card).
8. Press `k`/`↑` → card selection moves up (stops at 0).
9. Press `g` → jumps to first column, first card. Press `Shift+G` → jumps to last column.
10. Press `n` → prompts for title (required) and type (`Task`/`Issue`/`Goal`/`Idea`) → new card appears in the currently selected column after `LoadBoardAsync`.
11. Press `m` → prompts for target column → `MoveAsync` fires with `Version` from the cached card → board reloads with the card in the new column.
12. Press `Enter` → `_appState.SelectedCardId` is set and a green "Opening card #N..." message prints (Card detail screen is Task 9 — placeholder).

### Esc (this session's fix)
13. Press `Esc` from `BoardScreen` → a new `ProjectListScreen` is instantiated, `AppState.SelectedProjectId` is cleared, `OnEnterAsync` fires (reloads projects), `RenderAsync` runs, and the next keypress is handled by `ProjectListScreen` (typing `j`/`/`/`c` etc. works immediately — no dead key).
14. Press `Esc` and then `q` → confirm prompt → `Environment.Exit(0)`. (Confirms the entire screen stack terminates cleanly.)
15. Press `Esc` from `BoardScreen` twice in a row (back to project list, then quit) → no exception, no stale screen instance referenced.

### Edge Cases
16. Project with **zero columns** → board renders title + status only; column row is empty; `h`/`l`/`g`/`Shift+G` are no-ops; `n`/`m`/`Delete`/`Enter` all return early (`_columns.Count == 0` guard).
17. Column with **no cards** → renders header with `(0)` or `(0/<limit>)` and an empty body; `j` is a no-op while selected on that column.
18. Column with **no WIPLimit** → header shows `(N)` not `(N/0)`.
19. **Column name containing `[` or `]`** (e.g. `"[WIP] In Progress"`) → renders literally (no Markup parse error), wrapped in `Markup.Escape`.
20. Card **type** value is an enum member outside `Task`/`Issue`/`Goal`/`Idea` (e.g. a future `Bug`) → falls through to the grey `?` badge, no crash.
21. **Connection drop mid-board** (server killed) → `HttpRequestException` hits the `catch` in `LoadBoardAsync`, error is added to `ErrorCollector`, screen continues to render the last good state without throwing.

### Regressions
1. `ProjectListScreen` (Plan 6) still exits-to-board correctly via `Enter` — `_appState.SelectedProjectId` set, `BoardScreen` instantiated, `OnEnterAsync` + `RenderAsync` fire (this is the same pattern `Esc` now mirrors).
2. `LockScreen` (Plan 5) and `LoginScreen` (Plan 4) still use the same `IScreen` contract and `AppState.CurrentScreen = null` lifecycle.
3. `BoardScreen.OnEnterAsync` still re-fetches the project and cards; `OnExitAsync` remains `Task.CompletedTask` (no state cleanup needed yet).
4. `BoardRenderer` uses `BorderStyle = new Style(foreground: ...)` (newer Spectre.Console API) instead of the older `BorderColor` — visually equivalent under the current Spectre.Console version.
5. All 528 tests still pass (`dotnet test`).

### Cleanup
- [ ] No test data needed; project + columns are seed data. If new projects were created for type-badge enumeration tests, archive them via the API.
