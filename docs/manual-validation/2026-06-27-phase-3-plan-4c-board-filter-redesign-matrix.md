## Validate: Board Filter Redesign (Plan 4C)

Column visibility controlled via dropdown multi-select. Card-type filter stays per-column. Column selection makes `hideEmptyColumns` mutually exclusive.

### Setup
- [ ] `pnpm dev` running (web + API + Postgres + MinIO)
- [ ] Logged in as a user with access to a project that has 4+ columns and cards in at least 2 of them
- [ ] At least one column must be empty (for hide-empty scenarios)
- [ ] Browser DevTools at mobile width (~390px) ready for mobile checks

### Happy Path — Column Visibility Dropdown
1. Desktop: load board → all columns render → filter bar shows "All columns" button
2. Desktop: click "All columns" button → dropdown opens with checkboxes for every column
3. Desktop: uncheck "Backlog" → dropdown closes → board hides Backlog column → button updates to "1 column"
4. Desktop: reopen dropdown → uncheck "Done" → both Backlog and Done hidden → button shows "2 columns"
5. Desktop: reopen dropdown → recheck "Backlog" → Backlog reappears → button shows "1 column"
6. Desktop: reopen dropdown → check all → "All columns" button text returns
7. Mobile: open Filter panel → tap "All columns" → dropdown with checkboxes renders for every column → tapping toggles visibility same as desktop

### Bug Fix — hideEmptyColumns vs Column Selection
8. Desktop: uncheck a column → `Hide empty` checkbox becomes disabled (greyed, `not-allowed` cursor) → "(column selected)" hint appears
9. Desktop: uncheck a column + verify an unselected empty column stays visible (the regression — previously `hideEmptyColumns` would still strip it)
10. Mobile: open filter panel → toggle a column off → `Hide empty` checkbox disabled, hint visible
11. Recheck all columns → `Hide empty` checkbox re-enables; flipping it on collapses empty columns as before

### Edge Cases
12. Project with 1 column → dropdown shows single checkbox → unchecking it leaves board empty but `Hide empty` disables
13. Project with 0 columns → dropdown empty (no crash)
14. Rapidly toggle a checkbox 5x → board reactivity stays consistent (no stale state, no console errors)
15. Toggle column while another panel's mutation is in-flight → no race; board updates normally
16. Select column → refresh page → selection does NOT persist (client-side only — expected per plan)

### Regressions
17. Search box still filters cards across visible columns
18. Assignee dropdown still filters
19. `Include archived` checkbox still toggles archived visibility
20. Per-column Type dropdown still filters within column (unchanged scope)
21. Bulk-select checkboxes + BulkActionBar still work on mobile
22. Card three-dot menu (archive, restore, move up/down/to-column) unchanged
23. WIP limit badge still renders when at limit

### Cleanup
- [ ] Recheck all columns before navigating away (next session starts with all columns visible by default)
