## Validate: Card Type Redesign (Plan 4D)

Renames `Bug`→`Issue`, `Epic`→`Goal`, retires `Spec` (rows data-migrated to Goal). Removes the Epic-only parent restriction so any card type can parent any other. Updates UI labels across `CardCreateModal`, `CardMetadata`, `BoardCard`, `BoardMobileList`, and the type filter dropdowns.

### Setup
- [ ] `pnpm dev` running (web + API + Postgres + MinIO)
- [ ] Logged in as a user with access to a project
- [ ] Project has 2+ columns and 3+ existing cards of mixed types
- [ ] (Optional) seed a card via API with `Type=3` to verify data migration before reset
- [ ] Browser DevTools at mobile width (~390px) ready for mobile checks

### Happy Path — Type Rename
1. Open `+ Add Card` modal → Type dropdown lists exactly: `Task`, `Issue`, `Goal`, `Idea` (no `Bug`, `Epic`, `Spec`)
2. Create a card of each type → each shows correct label, color, and Lucide icon on the board (`square-check` / `bug` / `layers` / `lightbulb`)
3. Column header Type filter dropdown lists: `All`, `Task`, `Issue`, `Goal`, `Idea`
4. BoardCard and BoardMobileList show "Parent" (not "Epic") when `parentCardId` is set, with `i-lucide-layers` icon

### Happy Path — Open Parent Restriction
5. Create a `Task` card with parent = another `Task` → succeeds (200), card displays on board
6. Create an `Idea` card with parent = an `Issue` → succeeds (200)
7. Reopen `CardMetadata` for any card → "Parent" row exists in the metadata panel → dropdown shows every other card in the project, not filtered by type → changing it persists immediately
8. Set parent to a card from a different project via API → returns `400` `CARD_INVALID_PARENT` (was `CARD_INVALID_PARENT_EPIC`)
9. Set parent to self via API → returns `400` `CARD_PARENT_CYCLE`

### Data Migration — Spec Retirement
10. SQL check before validation: `SELECT COUNT(*) FROM cards WHERE "Type" = 3;` returns 0 (or row count matches pre-migration)
11. SQL check: `SELECT COUNT(*) FROM cards WHERE "Type" = 5;` ≥ previous `Type=3` count + previous `Type=5` count
12. Open a card that was previously `Spec` → metadata panel shows type `Goal` with layers icon (read from API; no client-side remapping)

### Edge Cases
13. Parent dropdown with 0 candidates (project has only one card) → shows only the `None` option
14. Parent dropdown when network fails → shows `None` only, no crash, no console error in production
15. Set parent → refresh page → parent persists
16. Set parent → set to `None` → saved; subsequent re-open shows `None` selected
17. Card with parent → archive card → metadata panel renders `Parent` row as read-only text (no dropdown)
18. Archived card with parent → restore → parent row becomes editable dropdown again
19. Card whose parent is archived → child card displays "Parent" badge; opening child metadata shows parent's title (or truncated id) read-only
20. Cycle: set A→B, then try B→A via API → returns `400 CARD_PARENT_CYCLE`

### Regressions
21. Existing cards (pre-rename) load with correct type label and icon (server enum names now `Issue`/`Goal`, integers unchanged)
22. Card CRUD still works: create, update title/description, move column, archive, restore
23. Card move with WIP-limit blocked-move warning still returns `409` with warning payload
24. Board filtering (column visibility, search, assignee, type filter) still works per Plan 4C
25. Quick-add, mobile reorder, bulk-action bar still work per Plan 4A
26. CardModal panels (Description, Metadata, Checklists, Comments, Attachments, Dependencies) still mount and persist
27. `CardContextSnapshot` board updates still fire on parent change
28. SignalR board events still broadcast card updates
29. Per-column WIP limit badge still renders
30. `cardTypeToApiString(0|1|2|3)` still maps to `Task|Issue|Goal|Idea`; unknown index defaults to `Task`

### Cleanup
- [ ] Remove any seeded test cards (or leave if the project is disposable)
