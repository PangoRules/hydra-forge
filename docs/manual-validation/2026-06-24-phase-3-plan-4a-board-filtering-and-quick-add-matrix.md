## Validate: Board Filtering & Quick-Add

**Plan:** `docs/plans/2026-06-24-phase-3-plan-4a-board-filtering-and-quick-add.md`
**Branch:** `task/phase-3-board-filtering`
**Server + Web UI both running** (pnpm dev + docker compose up)

### Setup
- [ ] Log in as a user with a project containing 3+ columns and 10+ cards across types (Task/Bug/Epic/Spec/Idea), some archived
- [ ] Navigate to `/projects/{id}/board`

### Global Filter Bar (Desktop — viewport ≥768px)
1. **Search**: type text → cards filter in real-time client-side, then server re-fetch on debounce
2. **Type dropdown**: select "Bug" → only Bug cards visible, column counts update
3. **Include Archived**: check → archived cards appear in columns (they start hidden)
4. **Hide Empty**: check → columns with 0 visible cards disappear from layout
5. **+ Add Card**: click → CardCreateModal opens with no column preselected
6. **Assignee filter**: select member → only their cards shown

### Per-Column Filters (Desktop)
7. **Column type dropdown**: select "Epic" in column → only Epics in that column shown
8. **Column archived checkbox**: check "Archived only" → only archived cards shown in that column
9. **Column inline search**: type "fix" → only cards in that column matching "fix" shown
10. **Column + Add**: click → CardCreateModal opens with that column preselected and locked

### Card Create Modal
11. **Global + Card → no preselection**: fill title, select column, create → card appears in selected column
12. **Column + Add → preselected**: fill title, create → card created in that column (column locked)
13. **Validation**: click Create with empty title → button disabled
14. **Cancel**: click Cancel → modal closes, no card created
15. **Epic parent**: select an Epic as parent, create card → card linked to epic

### Mobile (viewport <768px)
16. **Accordion**: columns start collapsed; tap column header → expands showing cards; tap again → collapses
17. **Global search**: type text → cards across all columns filter
18. **Filter panel**: tap "Filter" → slide-out with type/assignee/archived/hide-empty controls
19. **+ Add card**: tap "Add card" → CardCreateModal opens
20. **Three-dot menu**: tap ⋮ on a card → dropdown shows Archive/Restore

### Combinations & Edge Cases
21. **Global type + per-column type**: set global filter "Bug" AND per-column filter "Task" → intersection (0 cards likely in that column)
22. **Hide empty + type filter**: filter to a type with cards only in 1 column → only that column visible
23. **Include archived + per-column archived**: global include archives ON, per-column "Archived only" checked → only archived cards in that column
24. **Search then clear**: type search, wait 300ms (re-fetch), clear search → all cards return
25. **No results**: filter to type with no cards → empty columns, counts show 0

### Regressions
26. **Card move**: drag card between columns → still works
27. **Card click**: click card → CardModal opens with full details
28. **Card archive**: archive from three-dot menu → card gone, toast "Card archived"
29. **Column reorder**: drag column header → column order persists
30. **Project archived board**: open archived project → filters hidden, cards read-only

### Cleanup
- [ ] Close browser, no persistent state left
