## Validate: TUI Blocked Card Indicator on Board

### Setup
- [ ] Start HydraForge API and TUI with authenticated user.
- [ ] Open a project board with at least three cards (one will be a blocker, two will be blocked).
- [ ] Create a `BlockedBy` relationship from the source card to the blocker card (via `d` Dependency Panel).
- [ ] Create one non-`BlockedBy` relationship (e.g. `Precedes` or `Relates`) for negative coverage.

### Happy Path
1. On board, press `r` to refresh → the blocked source card shows `🔴` prefix; the blocker card does not.
2. Navigate `h`/`l` to another column and back → indicator still present on the blocked card after re-render.
3. Two cards blocked by the same blocker (two `BlockedBy` rows sharing one target) → both blocked cards show `🔴`; blocker card does not.
4. A card blocked by another blocked card (chain A `BlockedBy` B, B `BlockedBy` C) → both A and B show `🔴`; C does not.
5. Delete the `BlockedBy` relationship → next `r` refresh clears the `🔴` on the formerly blocked card.

### Edge Cases
1. Card with no relationships → no `🔴` prefix; renders as before.
2. Card with only `Precedes` / `Relates` / `SpawnedFrom` relationships → no `🔴` prefix (only `BlockedBy` triggers indicator).
3. Self-referential `BlockedBy` (source = target) → source card has `🔴` (still semantically blocked; not a crash).
4. Many cards on the board (≥20) → all `🔴` indicators render correctly; load is not visibly slower than other boards.
5. API returns 5xx / network failure during relationship fetch → catch block runs; board renders without `🔴` on any card; no crash, no unhandled exception toast.
6. Server returns empty `Relationships` array → indicator absent; no exception.

### Regressions
1. From board, press `h`/`l` → column selection moves; cards still render with assignees/version.
2. From board, press `j`/`k` → card selection moves within column; `🔴` indicator on blocked cards does not affect selection.
3. From board, press `Enter` on a card → card detail view still opens.
4. From board, press `n` → create card prompt still opens.
5. From board, press `m` → move card prompt still works; if moved into a blocked card without confirm, 409 warning still surfaces.
6. From board, press `d` on a card → Dependency Panel still opens with `BlockedBy` selected by default.
7. From card detail, press `Tab` / `Shift+Tab` → section index advances through Metadata/Description/Checklist/Comments/Dependencies; `Dependencies` section still lists the blocker.
8. Press `Esc` from board → ProjectListScreen opens; SignalR disconnects as before.
9. After SignalR reconnect → board data refreshes; `🔴` indicators re-derive correctly.
10. Help overlay (`?`) on board still shows `[d] Add dependency`; no new keybinds required.

### Cleanup
- [ ] Delete test `BlockedBy` and non-`BlockedBy` relationships created during validation.
- [ ] Remove any temporary cards created as blockers / blocked cards.
- [ ] Restore board / column state if mutated.
