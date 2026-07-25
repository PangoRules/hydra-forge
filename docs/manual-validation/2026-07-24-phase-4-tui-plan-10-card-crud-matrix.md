## Validate: TUI Card CRUD Keyboard Actions

### Setup
- [ ] Start HydraForge API and TUI with authenticated user.
- [ ] Open project board containing at least two columns and three cards in one column.
- [ ] Ensure one card has dependencies that make unconfirmed movement return `409 Conflict`.

### Happy Path
1. Select card and press `e` → title prompt opens with current title as default.
2. Enter non-empty title → card title updates and board rerenders.
3. Select card and press `r` → reorder hint remains visible; board is not immediately cleared.
4. Press `j` or `k` in reorder mode → selection moves within column.
5. Press `Enter` → card reorder request succeeds, reorder mode exits, board reloads with persisted order.
6. Press `r`, then `Esc` → reorder mode exits and board rerenders without leaving board.

### Edge Cases
1. Press `r` on empty board → no exception; reorder mode does not start.
2. Press `r` in empty column → no exception; reorder mode does not start.
3. While reorder mode is active, press `e` → edit prompt does not open.
4. While reorder mode is active, press `r` → reorder mode is not re-entered and no duplicate hint appears.
5. Trigger dependency-blocked reorder → warning remains visible after `409`; reorder mode exits.
6. Receive unknown card-type display value during title edit → mapper falls back to `Task`; TUI does not crash.

### Regressions
1. Press `Enter` outside reorder mode → card detail opens.
2. Press `Esc` outside reorder mode → project list opens.
3. Press `n` → card creation still succeeds and board reloads.
4. Press `m` → move prompt still opens; successful move persists.
5. Trigger dependency-blocked normal move → warning remains visible.
6. Press `Delete`, confirm archive → card archives and board reloads.

### Cleanup
- [ ] Restore edited card title and original card order.
- [ ] Remove any cards created during validation.
