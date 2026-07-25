## Validate: TUI Dependency Panel

### Setup
- [X] Start HydraForge API and TUI with authenticated user.
- [X] Open project board containing at least two cards (one source, one or more targets) in any column.
- [X] Open a card detail view (Enter on a card) to verify the `d` key triggers from there too.

### Happy Path
1. From board, press `d` on a card → Dependency Panel modal renders over the board.
2. Type a partial card number → result list updates live (up to 5 cards, source card filtered out).
3. Press `Tab` → focus moves from Search → Type → Confirm; selected section border highlights blue.
4. Press `Shift+Tab` → focus moves backwards through the same three sections.
5. With Type focused, press `j`/`k` (or ↓/↑) → highlight moves through `BlockedBy` → `Precedes` → `Relates` → `SpawnedFrom` and wraps.
6. With Type focused, press `l`/`h` (or →/←) → highlight does not move (left/right reserved for columns in parent).
7. With Search focused and ≥1 result, press `j`/`k` (or ↓/↑) → result cursor moves through the result list and wraps.
8. With Search focused and ≥1 result, press `Enter` → relationship is created and modal dismisses.
9. With Confirm focused, press `Enter` → relationship is created using highlighted type and modal dismisses.
10. After dismiss → board re-renders (parent restored, no duplicate SignalR handlers, no reconnection).
11. Card detail view: open a card, press `d` → Dependency Panel opens with `_cardId` as source.
12. Pick target, confirm → relationship created; success message persisted in ErrorCollector (visible via Task 17 status bar when shipped).

### Edge Cases
1. Type only whitespace → search results clear; cursor reset to 0.
2. Type text that matches zero cards → result list empty; pressing Enter on Confirm does nothing (no API call).
3. Press `Backspace` until search is empty → results clear; cursor reset.
4. Press `Enter` on Confirm with empty results → red hint shown ("No card selected..."); modal stays open.
5. Press `Esc` from any focus state → modal dismisses, parent re-renders, no relationship created.
6. Server returns 4xx (e.g. duplicate relationship, validation) → error added to ErrorCollector; modal stays open; user can retry or Esc.
7. Server unreachable (HttpRequestException) → connection error added to ErrorCollector; modal stays open.
8. Press `Ctrl+Q` while modal is open → process exits (global shortcut).
9. Press `d` on a column with zero cards → no panel opens, no exception.
10. From board, press `d`, dismiss, immediately press `d` again → new panel opens cleanly; SignalR handlers not duplicated (single event subscription per OnEnterAsync call).

### Regressions
1. From board, press `h`/`l` → column selection moves; cards still load and render.
2. From board, press `j`/`k` → card selection moves within column.
3. From board, press `Enter` on a card → card detail opens.
4. From board, press `n` → create card prompt opens.
5. From board, press `m` → move card prompt opens.
6. From board, press `r` → reorder mode enters; existing reorder-mode behavior preserved.
7. From board, press `Esc` → ProjectListScreen opens; SignalR disconnects and is nulled (true exit).
8. From board, after SignalR reconnect → `Connection` status returns to Connected, `OnlineCount` updates.
9. From card detail, press `Tab`/`Shift+Tab` → section index advances through Metadata/Description/Checklist/Comments/Dependencies.
10. From card detail, press `Esc` → board re-renders.
11. Help overlay (`?`) on board shows `[d] Add dependency` (no longer "coming soon").

### Cleanup
- [ ] Remove any test relationships created during validation.
- [ ] Restore board/card state if mutated.
