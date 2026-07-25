# Validate: BoardScreen (columns + cards + keyboard navigation + Esc back)

## Setup
- [ ] `dotnet build` clean (zero warnings, zero errors). NSwag codegen produces `HydraForgeApiClient` with `ProjectsGET2Async`, `CardsGETAsync`, `CardsPOSTAsync`, `MoveAsync`, `ArchiveAsync` (renamed from earlier `CardsXxxAsync` per D-50+ codegen).
- [ ] Server running with at least one project containing ≥3 columns, where at least one column has a `WIPLimit` set, one column has cards, and one column has no cards.
- [ ] TUI logged in (Plan 4) and on `ProjectListScreen` (Plan 6). Selecting a project with `Enter` transitions to `BoardScreen` and renders the layout.

## Layout
- [ ] Board renders three rows: Title panel (project name + "N columns  M cards"), Board row (one panel per column side-by-side), Status panel placeholder.
- [ ] Title and Status rows are fixed at 3 lines tall (`Layout.Size(3)`) — the Board row gets the rest of the terminal height. **Regression check:** before this fix, all three rows split the terminal evenly (`SplitRows` with no `Size`), so a single-line Title/Status sat inside a huge mostly-blank panel and squeezed the Board row down to the same size. Confirm the Title/Status panels hug their text tightly instead of leaving a large empty box above/below the board.
- [ ] Each column panel shows a rounded header `<ColumnName> (<cardCount>)` — e.g. `Backlog (3)`, `In Progress (5/8)` when `WIPLimit` is set, `Done (2)` otherwise.
- [ ] Each card has a coloured type badge (`T` cyan1, `I` red, `G` yellow, `D` green, `?` grey fallback), `#<CardNumber>`, truncated title (≤25 chars), and grey assignee initials suffix.
- [ ] Blocked cards prefix with a 🔴 glyph.

## Navigation
- [ ] `h`/`←` — selection moves to the previous column, `_selectedCard` resets to 0; new column header border is blue, previous column reverts to its configured colour.
- [ ] `l`/`→` — next column; selected column border is blue, others use their configured colour.
- [ ] `j`/`↓` — card selection moves down within the current column (stops at last card).
- [ ] `k`/`↑` — card selection moves up (stops at 0).
- [ ] `g` — jumps to first column, first card. `Shift+G` — jumps to last column.
- [ ] `n` — prompts for title (required) and type (`Task`/`Issue`/`Goal`/`Idea`) → new card appears in the currently selected column after `LoadBoardAsync`.
- [ ] `m` — prompts for target column → `MoveAsync` fires with `Version` from the cached card → board reloads with the card in the new column.
- [ ] `Enter` — `_appState.SelectedCardId` is set and a green "Opening card #N..." message prints (Card detail screen is Task 9 — placeholder).

## Esc (this session's fix)
- [ ] `Esc` from `BoardScreen` → a new `ProjectListScreen` is instantiated, `AppState.SelectedProjectId` is cleared, `OnEnterAsync` fires (reloads projects), `RenderAsync` runs, and the next keypress is handled by `ProjectListScreen` (typing `j`/`/`/`c` etc. works immediately — no dead key).
- [ ] `Esc` then `q` → confirm prompt → `Environment.Exit(0)`. (Confirms the entire screen stack terminates cleanly.)
- [ ] `Esc` from `BoardScreen` twice in a row (back to project list, then quit) → no exception, no stale screen instance referenced.

## Edge Cases
- [ ] Project with **zero columns** → board renders title + status only; column row is empty; `h`/`l`/`g`/`Shift+G` are no-ops; `n`/`m`/`Delete`/`Enter` all return early (`_columns.Count == 0` guard).
- [ ] Column with **no cards** → renders header with `(0)` or `(0/<limit>)` and an empty body; `j` is a no-op while selected on that column.
- [ ] Column with **no WIPLimit** → header shows `(N)` not `(N/0)`.
- [ ] **Column name containing `[` or `]`** (e.g. `"[WIP] In Progress"`) → renders literally (no Markup parse error), wrapped in `Markup.Escape`.
- [ ] Card **type** value is an enum member outside `Task`/`Issue`/`Goal`/`Idea` (e.g. a future `Bug`) → falls through to the grey `?` badge, no crash.
- [ ] **Connection drop mid-board** (server killed) → `HttpRequestException` hits the `catch` in `LoadBoardAsync`, error is added to `ErrorCollector`, screen continues to render the last good state without throwing.

## Regressions
- [ ] `ProjectListScreen` (Plan 6) still exits-to-board correctly via `Enter` — `_appState.SelectedProjectId` set, `BoardScreen` instantiated, `OnEnterAsync` + `RenderAsync` fire (this is the same pattern `Esc` now mirrors).
- [ ] `LockScreen` (Plan 5) and `LoginScreen` (Plan 4) still use the same `IScreen` contract and `AppState.CurrentScreen = null` lifecycle.
- [ ] `BoardScreen.OnEnterAsync` still re-fetches the project and cards; `OnExitAsync` remains `Task.CompletedTask` (no state cleanup needed yet).
- [ ] `BoardRenderer` uses `BorderStyle = new Style(foreground: ...)` (newer Spectre.Console API) instead of the older `BorderColor` — visually equivalent under the current Spectre.Console version.
- [ ] All tests still pass (`dotnet test`).

## Cleanup
- [ ] No test data needed; project + columns are seed data. If new projects were created for type-badge enumeration tests, archive them via the API.
