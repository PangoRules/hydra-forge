## Validate: Board Filter Redesign (Plan 4C)

Replaces global card-type dropdown with column-visibility chips. Card-type filter stays per-column. Column selection makes `hideEmptyColumns` mutually exclusive.

### Setup
- [ ] `pnpm dev` running (web + API + Postgres + MinIO)
- [ ] Logged in as a user with access to a project that has 4+ columns and cards in at least 2 of them
- [ ] At least one column must be empty (for hide-empty scenarios)
- [ ] Browser DevTools at mobile width (~390px) ready for mobile checks

### Happy Path — Column Selection
1. Desktop: load board → all columns render → chip row shows every column name → chips not selected by default
2. Desktop: click "Backlog" chip → chip turns primary-blue → board hides all other columns → only Backlog accordion/column visible
3. Desktop: click a second chip (e.g. "Done") → both chips primary-blue → only those two columns visible
4. Desktop: click selected chip again → chip returns to outlined → that column reappears (others stay selected)
5. Desktop: deselect all chips → all columns reappear
6. Mobile: open Filter panel → chips render for every column → click chip → toggles selected state same as desktop

### Bug Fix — hideEmptyColumns vs Column Selection
7. Desktop: select a column chip → `Hide empty` checkbox becomes disabled (greyed, `not-allowed` cursor) → "(column selected)" hint appears
8. Desktop: select a chip + check that an unselected empty column stays visible (the regression — previously `hideEmptyColumns` would still strip it)
9. Mobile: open filter panel → select a chip → `Hide empty` checkbox disabled, hint visible
10. Deselect all chips → `Hide empty` checkbox re-enables; flipping it on collapses empty columns as before

### Edge Cases
11. Project with 1 column → chip row shows single chip → selecting it leaves board unchanged but `Hide empty` disables
12. Project with 0 columns → chip row empty (no crash)
13. Rapidly toggle a chip 5x → board reactivity stays consistent (no stale state, no console errors)
14. Select chip → refresh page → selection does NOT persist (client-side only — expected per plan)
15. Click chip while another panel's mutation is in-flight → no race; chip state updates normally

### Regressions
16. Search box still filters cards across visible columns
17. Assignee dropdown still filters
18. `Include archived` checkbox still toggles archived visibility
19. Per-column Type dropdown in `ColumnHeader` still filters within column (unchanged scope)
20. Bulk-select checkboxes + BulkActionBar still work on mobile
21. Card three-dot menu (archive, restore, move up/down/to-column) unchanged
22. WIP limit badge still renders when at limit

### Cleanup
- [ ] Deselect all chips before navigating away (next session starts with empty selection by default)