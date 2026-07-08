# E2E Regression Matrix — Phase 3 Web UI

Spec: `docs/specs/2026-06-23-phase-3-web-ui-design.md`

Accumulates per-task coverage. Sections added as each plan completes.

---

## Plan 2: Project List & Board

### Pre-flight

```bash
docker compose up -d
cd src/web-ui && pnpm dev
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"testadmin","password":"TestAdmin123!"}' | \
  python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
echo $TOKEN
```

Open `http://localhost:3000` in a browser.

### 1. Auth & Project List

| # | Step | Expected | Pass? |
|---|---|---|---|
| 1.1 | Navigate to `http://localhost:3000` | Redirect to `/login` | ☐ |
| 1.2 | Login with `testadmin` / `TestAdmin123!` | Redirect to `/projects`, project list renders | ☐ |
| 1.3 | Project list shows existing smoke-test projects | Cards with project names + "No description" fallback | ☐ |
| 1.4 | Click "New Project" button | Create modal opens with Name + Description fields + Cancel/Create buttons | ☐ |
| 1.5 | Fill in `E2E Test Project` / `Test board` → click Create | Modal closes, new project appears at top of list | ☐ |
| 1.6 | Click the new project card | Navigate to `/projects/{id}/board`, Board header visible | ☐ |
| 1.7 | Logout (top-right button) → login again | Project list still shows the new project | ☐ |

### 2. Board — Desktop (viewport ≥ 768px)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 2.1 | Navigate to board of the `E2E Test Project` | Board header + refresh button visible | ☐ |
| 2.2 | Check Network tab | `GET /api/projects/{id}/Columns` 200 + `GET /api/projects/{id}/Cards` 200 | ☐ |
| 2.3 | Columns render | 6 default columns: Backlog, Spec-ing, Planned, In Dev, In Review, Done with card counts showing "0" | ☐ |
| 2.4 | Refresh button | Click refresh icon → columns re-fetch (check Network tab for new requests) | ☐ |
| 2.5 | No console errors | Only `<Suspense>` warning (expected), no icon warnings | ☐ |

### 3. Board — Mobile (viewport < 768px)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 3.1 | Open Chrome DevTools → Toggle device toolbar (Cmd+Shift+M) → set width to 375px | Mobile list view renders | ☐ |
| 3.2 | Column headers render in list format | Backlog, Spec-ing, etc. listed vertically | ☐ |
| 3.3 | Card counts show "0" | Each column shows badge with 0 | ☐ |
| 3.4 | Switch back to desktop (≥768px) | Kanban columns render horizontally again | ☐ |

### 4. Card CRUD

```bash
PROJECT_ID="<YOUR_PROJECT_ID_FROM_URL>"
COL_ID=$(curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/projects/$PROJECT_ID/Columns | \
  python3 -c "import sys,json; cols=json.load(sys.stdin); print(cols[0]['id'])")
echo "Backlog column: $COL_ID"
```

| # | Step | Expected | Pass? |
|---|---|---|---|
| 4.1 | Create a "Task" card | `curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/Cards -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"columnId\":\"$COL_ID\",\"title\":\"Fix login bug\",\"type\":0}"` → 201 | ☐ |
| 4.2 | Refresh the board | Card "Fix login bug" appears in Backlog with task icon (#1) | ☐ |
| 4.3 | Create a "Bug" card | type=1 → 201 | ☐ |
| 4.4 | Create an "Epic" card | type=2 → 201 | ☐ |
| 4.5 | Card count updates | Backlog badge shows "3" | ☐ |
| 4.6 | Switch to mobile view | All 3 cards visible under Backlog header | ☐ |

### 5. Card Move Between Columns

| # | Step | Expected | Pass? |
|---|---|---|---|
| 5.1 | Move Bug card to In Dev via `PUT /api/projects/$PROJECT_ID/Cards/$CARD_ID/move` | 200 | ☐ |
| 5.2 | Refresh the board | Bug card in In Dev column | ☐ |
| 5.3 | Backlog shows "2", In Dev shows "1" | Counts correct | ☐ |

### 6. Error States

| # | Step | Expected | Pass? |
|---|---|---|---|
| 6.1 | Stop server: `docker compose stop server` → refresh board | "Failed to load board" error + Retry button | ☐ |
| 6.2 | Start server → Retry | Board loads | ☐ |
| 6.3 | Logout → navigate to `/projects/{id}/board` | Redirect to `/login` | ☐ |

### 7. Column Reorder (Persistence)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 7.1 | Reorder via `PUT /api/projects/$PROJECT_ID/Columns/reorder` | 204 | ☐ |
| 7.2 | Refresh the board | Column order persists | ☐ |

---

## Plan 3: Card Modal Core

### Pre-flight

Same stack startup as Plan 2. Ensure a card with rich formatted description exists (create via curl if needed):

```bash
PROJECT_ID="<YOUR_PROJECT_ID>"
COL_ID=$(curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/projects/$PROJECT_ID/Columns | \
  python3 -c "import sys,json; print(json.load(sys.stdin)[0]['id'])")
curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/Cards \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"columnId\":\"$COL_ID\",\"title\":\"E2E Test Card\",\"description\":\"<h2>Heading</h2><ul><li><p>item</p></li></ul>\",\"type\":0}"
```

### 1. Card Modal Open & Close

| # | Step | Expected | Pass? |
|---|---|---|---|
| 1.1 | Click a card on the board | Modal opens with scale-in animation | ☐ |
| 1.2 | Click backdrop | Modal closes with scale-out animation | ☐ |
| 1.3 | Press `Escape` | Modal closes | ☐ |
| 1.4 | Click X button | Modal closes | ☐ |
| 1.5 | Re-open immediately after closing | Modal opens normally | ☐ |

### 2. Desktop Layout (viewport ≥ 768px)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 2.1 | Open modal on desktop | Left: CardDescription editor. Right: CardMetadata sidebar (border-left) | ☐ |
| 2.2 | Metadata shows | Type badge, Column ID (first 8 chars), Assignees, Due Date | ☐ |
| 2.3 | Resize to mobile (<768px) | Tabbed view: Details / Checklist / Comments / Related | ☐ |
| 2.4 | Checklist tab | Placeholder "Checklist coming soon" | ☐ |
| 2.5 | Comments tab | Placeholder "Comments coming soon" | ☐ |
| 2.6 | Related tab | Placeholder "Attachments, dependencies, specs, plans coming soon" | ☐ |

### 3. Save Button — Dirty State & Spinner

| # | Step | Expected | Pass? |
|---|---|---|---|
| 3.1 | Open card | Save button disabled (no unsaved changes) | ☐ |
| 3.2 | Type in editor | Save button enabled | ☐ |
| 3.3 | Click Save | Spinner shown, then disabled (clean state) | ☐ |
| 3.4 | Type, wait ~2s | Auto-save fires. No double-save. | ☐ |
| 3.5 | `Ctrl+Enter` | Immediate save | ☐ |
| 3.6 | After save, refresh | Description persists | ☐ |

### 4. MarkdownEditor — Formatting Toolbar

| # | Step | Expected | Pass? |
|---|---|---|---|
| 4.1 | Bold | Selected text bold. Button highlights. Tooltip "Bold". | ☐ |
| 4.2 | Italic | Italic. Button highlights. Tooltip "Italic". | ☐ |
| 4.3 | Heading buttons | Line converts to H1/H2/H3 | ☐ |
| 4.4 | Bullet List | Bullet list. Enter adds new bullet. | ☐ |
| 4.5 | Ordered List | Numbered list. Auto-increments. | ☐ |
| 4.6 | Code Block | Monospace gray background | ☐ |
| 4.7 | Blockquote | Left border, indented | ☐ |
| 4.8 | Toggle active formatting | Toggles off | ☐ |
| 4.9 | Source toggle | Shows raw markdown textarea | ☐ |

### 5. Source Mode Round-Trip

| # | Step | Expected | Pass? |
|---|---|---|---|
| 5.1 | Click Source | Switches to raw markdown textarea | ☐ |
| 5.2 | Code blocks use fenced ` ``` ` | Not 4-space indented | ☐ |
| 5.3 | Lists are compact | No extra blank lines between items | ☐ |
| 5.4 | Switch back without editing | Content identical | ☐ |
| 5.5 | Source → preview without editing | Save button stays disabled | ☐ |
| 5.6 | Edit in source → switch back | Save button enabled | ☐ |
| 5.7 | Multiple round-trips | No spacing degradation | ☐ |

### 6. Archive / Restore

| # | Step | Expected | Pass? |
|---|---|---|---|
| 6.1 | Click Archive | Modal closes, card removed from board | ☐ |
| 6.2 | Archive while network down | Error toast "Failed to archive card". Modal stays open. | ☐ |
| 6.3 | Open archived card | Shows Restore button | ☐ |
| 6.4 | Click Restore | Card reappears on board, modal stays open | ☐ |
| 6.5 | Restore while network down | Error toast "Failed to restore card" | ☐ |

### 7. Concurrency & Version Tracking

| # | Step | Expected | Pass? |
|---|---|---|---|
| 7.1 | Open card in two tabs | Both show same content | ☐ |
| 7.2 | Tab A: edit & save | Gets version N+1 | ☐ |
| 7.3 | Tab B: edit & save (stale version) | 409 CARD_CONCURRENCY_MISMATCH | ☐ |
| 7.4 | Close + reopen in Tab B | Loads version N+1 | ☐ |

### 8. Edge Cases

| # | Step | Expected | Pass? |
|---|---|---|---|
| 8.1 | Card with no description | Placeholder "Add a description...". Save disabled. | ☐ |
| 8.2 | Card with very long title | Title truncated with ellipsis | ☐ |
| 8.3 | `<script>alert('xss')</script>` in source mode | Tiptap strips/escapes. No alert popup. | ☐ |
| 8.4 | Close modal before 2s auto-save fires | Auto-save does NOT fire after close | ☐ |
| 8.5 | Reopen after unsaved close | Content from database (not in-memory state) | ☐ |

### 9. Regressions (Plan 2 baseline)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 9.1 | Project list loads | Unaffected | ☐ |
| 9.2 | Create new project | Works | ☐ |
| 9.3 | Board on mobile | BoardMobileList renders | ☐ |
| 9.4 | Board on desktop | Kanban renders | ☐ |
| 9.5 | Login with invalid credentials | Error toast, stays on login | ☐ |

---

## Plan 3A: Card Modal Hardening


### Setup
- [X] API server running locally (`ASPNETCORE_ENVIRONMENT=Development`)
- [X] Web dev server running (`pnpm dev` at `src/web-ui`)
- [X] Logged in, project with cards created

### Task 1 — Error Toasts on useApi() Failure

#### Card Modal Archive Error
1. Open a card modal → click Archive → confirm → observe toast "Card archived" (happy path)
2. Stop API server → open card modal → click Archive → confirm → observe red error toast "Failed to archive card" (previously: silent nothing)
3. Restart API server → verify happy path still works
Validated this works as expected.

#### Card Modal Restore Error
1. Open archived card modal → click Restore → observe toast "Card restored" (happy path)
2. Stop API server → open archived card modal → click Restore → observe red error toast "Failed to restore card" (previously: silent nothing)
3. Restart API server → verify happy path still works
Validated this works as expected.

#### Board Card Archive Error
1. From board view → three-dot menu on card → Archive → confirm → observe toast "Card archived" + card disappears (happy path)
2. Stop API server → three-dot menu → Archive → confirm → observe red error toast "Failed to archive card" (previously: silent nothing)
3. Restart API server → verify happy path still works
Validated this works as expected.

#### Board Card Restore Error
1. Enable "Include archived" filter → three-dot menu on archived card → Restore → observe toast "Card restored" + card reappears (happy path)
2. Stop API server → three-dot menu on archived card → Restore → observe red error toast "Failed to restore card" (previously: silent nothing)
3. Restart API server → verify happy path still works
Validates this works as expected.

#### Card Create Error
1. Click "Add card" → fill form → click Create → observe toast "Card created" + card appears on board (happy path)
2. Stop API server → click "Add card" → fill form → click Create → observe red error toast with message (previously: modal just closed silently)
3. Restart API server → verify happy path still works
Validated this works as epxected.

### Task 2 — Version Ownership (requires Plan 4 Task 16 for full test)

1. Open card modal → type in description → click Save → description saves without error
2. Verify no console errors when saving description multiple times in same modal session
3. Close and re-open modal → description matches saved value

### Task 3 — Shared Utilities & Type Filters

1. Card create modal Type dropdown shows options: Task, Bug, Epic, Spec, Idea
Validated
2. Board filter bar Type dropdown shows: All, Task, Bug, Epic, Spec, Idea
Validated
3. Column header Type dropdown (mobile + desktop) shows: All, Task, Bug, Epic, Spec, Idea
Validated
4. Select "Bug" filter → only Bug cards visible
Validated with Task type
5. Select "All" → all cards visible again
Validated
6. Due date shown on cards formatted as "Mar 15" (short month + day)
Working
7. Past due date card shows red text "text-red-500"
Working

### Task 4 — Accessibility

1. Open card modal → edit description → click Save → screen reader should hear "Saving description…" (visible only to assistive tech via `.sr-only`)
2. Stop API server → edit description → click Save → observe error text "Failed to save" rendered with `role="alert"` (inspect DOM for `[role="alert"]`)

### Regressions

1. Card modal opens, loads card data, displays description
2. Board view shows all cards with correct type icons/labels
3. Board filter bar filtering works across all type, assignee, archive, search options
4. Board mobile list type filter works
5. Build: `cd src/web-ui && pnpm build` succeeds

---

## Plan 4: Card Modal Panels


### Setup
- [x] API server running locally (`ASPNETCORE_ENVIRONMENT=Development`)
- [x] Web dev server running (`pnpm dev` at `src/web-ui`)
- [x] Logged in, project with cards and at least 2 members
- [x] At least one card with parent epic available (for metadata editor parent pick)
- [x] MinIO running (attachment upload needs it)

### Task 12 — Card Checklist

1. Open any card modal → checklist panel visible in desktop right sidebar
Valid it's working
2. Type "Test item 1" in add field → click Add → item appears in list, input clears
Typed asdf appeared both in right panel and in the checklist menu item after selecting it. (not sure how to describe that this worked as expected)
3. Add 3+ items → verify progress bar updates (0% → X/N as you complete)
Yep I have 8 items where 3 are completed, it shows 3/8 and the green progress is updated
4. Click checkbox on item 1 → item shows strikethrough, progress % increases
Correct clicking item 1 strikethrugh happens and progress increases. There is a weird thing where it doesn't look smooth but it does happens smooth, not sure if am explaining, could be more smoother, that is what I was trying to say wihtout being dispectful
5. Stop API server → click checkbox on item 2 → checkbox reverts, red error toast "Failed to update item"
Yep I got Failed to update item this passes
6. Restart API → toggle item 2 → checkbox persists (happy path restored)
Yep passes
7. Click up arrow on item 3 → item 3 swaps with item 2 in UI
this works as expectd
8. Stop API → click up arrow on item 3 → list snaps back to server order, red error toast "Failed to reorder item"
Yep it doesn't snap back to server order I get a toast saying failed to reorder item and an error being displayed stating that there was an error when attempting to fetch the resource.
9. Click trash icon on an item → item disappears from list
Works!
10. Stop API → click trash → item reappears at original position, red error toast "Failed to delete item"
Works!
11. Mobile viewport → open card modal → switch to "Related" tab → checklist renders there
Related has attachments and dependencies, Checklist is rendered as a clickable tab so we're good

### Task 13 — Card Comments

1. Open any card modal → comments panel visible
Works! mobile and desktop
2. Type comment text → click Post → comment appears in list with author + timestamp
Works!
3. Empty input → Post button disabled
Works!
4. Stop API → type comment → click Post → input not cleared, red error toast
Works
5. Page refresh → comments persist (loaded from API, not just local state)
Works
6. Mobile → comments render in "Related" tab
Works

### Task 14 — Card Attachments

1. Open any card modal → attachments panel visible
Works
2. Click upload → select small image (< 5MB) → file uploads, appears in list with filename + size
It allowed me to upload a pdf and a small image.
3. Click downloaded file icon → file downloads with correct content
Works!
4. Click delete on an attachment → confirmation → attachment disappears from list
Works!
5. Stop API → click upload → red error toast, file not added
Works!
6. Try uploading > 10MB file → expect validation rejection (size limit)
Works!
7. MinIO stopped → upload → graceful failure with error toast, no crash
Works!
8. Mobile → attachments render in "Related" tab
Works!

### Task 15 — Card Dependencies

1. Open card with no dependencies → "No dependencies" empty state shown
Works!
2. Create dependency via API (link another card as blocks/blocked-by/relates-to) → reopen modal → badge appears with relationship type label
3. Each linked card shows title + clickable to navigate (if routing wired)
4. Mobile → dependencies render in "Related" tab
All this works as expected

### Task 16 — Card Metadata Editor

1. Open any card modal → metadata sidebar shows: Type, Column, Assignees, Due date, Parent epic
Working
2. Change Type from Task → Bug → save → modal refreshes, type shown as Bug, no 409 error
Working
3. Stop API → change type → save → red error toast, type reverts

4. Change Column dropdown → save → card column updates
5. Add an assignee (project member) → appears in assignees list
6. Remove an assignee → disappears
7. Set due date in future → due date shows on card on board
8. Set due date in past → board card shows red text
9. Pick a parent epic (different card) → parent appears in metadata
10. Save metadata → open another modal in same session → no stale version: edit description first then metadata → both save without 409 CARD_CONCURRENCY_MISMATCH
11. Open archived card modal → metadata fields disabled / not editable
12. Mobile → metadata renders in "Related" tab

### Project Members — Self-Remove Confirmation (cycle 3 fix)

1. Log in as a non-owner member of a project with another active member present
2. Open project members list (either Project edit modal or board page MemberManagementPanel)
3. Click remove (trash icon) on own row → confirm dialog appears (was broken before: removed immediately without confirmation)
4. Click Cancel in confirm dialog → member still in list, no API call fired
5. Click remove on own row again → confirm dialog → click Confirm → member removed, toast shown
6. Log in as project Owner → click remove on own row → no confirm dialog, member removed immediately (owners bypass the warning)
7. Click remove on another non-owner member → no confirm dialog, removed immediately

### Project List — Archive / Restore + Filter Toggle

1. Project list page (`/projects`) → three-dot menu on a project → Archive → confirm → project disappears from default list
2. Enable "Include archived" filter toggle → archived project reappears with "Archived" badge
3. From three-dot menu on archived project → Restore → confirm → project returns to active state
4. Stop API → click Archive on a project → red error toast, project still visible
5. Stop API → click Restore on archived project → red error toast, project stays archived

### Regressions

1. Card modal opens, loads card data, description editor + auto-save still work
2. Card description saves without `409 CARD_CONCURRENCY_MISMATCH` when metadata also edited
3. Board view renders all cards with type icons + due-date formatting
4. Board filter bar: Type, Assignee, Archive, Search all still work
5. Card create modal still works (uses shared `cardTypeToApiString`/`formatDueDate` utilities)
6. Session expiry warning system still functions (refresh button)
7. Build: `cd src/web-ui && pnpm build` succeeds
8. Typecheck: `cd src/web-ui && pnpm typecheck` passes
9. Lint: `cd src/web-ui && pnpm lint` passes

### Cleanup

- [ ] Restore any cards archived during validation
- [ ] Delete any test comments/attachments uploaded
- [ ] Undo any test dependency links

---

## Plan 4A: Board Filtering & Quick-Add


**Plan:** `docs/plans/2026-06-24-phase-3-plan-4a-board-filtering-and-quick-add.md`
**Branch:** `task/phase-3-board-filtering`
**Server + Web UI both running** (pnpm dev + docker compose up)

#### Setup
- [ ] Log in as a user with a project containing 3+ columns and 10+ cards across types (Task/Bug/Epic/Spec/Idea), some archived
- [ ] Navigate to `/projects/{id}/board`

#### Global Filter Bar (Desktop — viewport ≥768px)
1. **Search**: type text → cards filter in real-time client-side, then server re-fetch on debounce
Works
2. **Type dropdown**: select "Bug" → only Bug cards visible, column counts update
Works
3. **Include Archived**: check → archived cards appear in columns (they start hidden)
Works
4. **Hide Empty**: check → columns with 0 visible cards disappear from layout
Works
5. **+ Add Card**: click → CardCreateModal opens with no column preselected
Works
6. **Assignee filter**: select member → only their cards shown
Works

#### Per-Column Filters (Desktop)
7. **Column type dropdown**: select "Epic" in column → only Epics in that column shown
Works
8. **Column archived checkbox**: check "Archived only" → only archived cards shown in that column
Works
9. **Column inline search**: type "fix" → only cards in that column matching "fix" shown
Works
10. **Column + Add**: click → CardCreateModal opens with that column preselected and locked
Works

#### Card Create Modal
11. **Global + Card → no preselection**: fill title, select column, create → card appears in selected column
Works
12. **Column + Add → preselected**: fill title, create → card created in that column (column locked)
Works
13. **Validation**: click Create with empty title → button disabled
Works
14. **Cancel**: click Cancel → modal closes, no card created
Works
15. **Epic parent**: select an Epic as parent, create card → card linked to epic

#### Mobile (viewport <768px)
16. **Accordion**: columns start collapsed; tap column header → expands showing cards; tap again → collapses
Works
17. **Global search**: type text → cards across all columns filter
Works
18. **Filter panel**: tap "Filter" → slide-out with type/assignee/archived/hide-empty controls
Works
19. **+ Add card**: tap "Add card" → CardCreateModal opens
Works
20. **Three-dot menu**: tap ⋮ on a card → dropdown shows Archive/Restore
Works

#### Combinations & Edge Cases
21. **Global type + per-column type**: set global filter "Bug" AND per-column filter "Task" → intersection (0 cards likely in that column)
22. **Hide empty + type filter**: filter to a type with cards only in 1 column → only that column visible
23. **Include archived + per-column archived**: global include archives ON, per-column "Archived only" checked → only archived cards in that column
24. **Search then clear**: type search, wait 300ms (re-fetch), clear search → all cards return
25. **No results**: filter to type with no cards → empty columns, counts show 0

#### Regressions
26. **Card move**: drag card between columns → still works
27. **Card click**: click card → CardModal opens with full details
28. **Card archive**: archive from three-dot menu → card gone, toast "Card archived"
29. **Column reorder**: drag column header → column order persists
30. **Project archived board**: open archived project → filters hidden, cards read-only

#### Cleanup
- [ ] Close browser, no persistent state left

---

## Plan 4C: Board Filter Redesign


Column visibility controlled via dropdown multi-select. Card-type filter stays per-column. Column selection makes `hideEmptyColumns` mutually exclusive.

#### Setup
- [ ] `pnpm dev` running (web + API + Postgres + MinIO)
- [ ] Logged in as a user with access to a project that has 4+ columns and cards in at least 2 of them
- [ ] At least one column must be empty (for hide-empty scenarios)
- [ ] Browser DevTools at mobile width (~390px) ready for mobile checks

#### Happy Path — Column Visibility Dropdown
1. Desktop: load board → all columns render → filter bar shows "All columns" button
Wroks
2. Desktop: click "All columns" button → dropdown opens with checkboxes for every column
Works
3. Desktop: uncheck "Backlog" → dropdown closes → board hides Backlog column → button updates to "1 column"
Works
4. Desktop: reopen dropdown → uncheck "Done" → both Backlog and Done hidden → button shows "2 columns"
Works
5. Desktop: reopen dropdown → recheck "Backlog" → Backlog reappears → button shows "1 column"
Works
6. Desktop: reopen dropdown → check all → "All columns" button text returns
Works
7. Mobile: open Filter panel → tap "All columns" → dropdown with checkboxes renders for every column → tapping toggles visibility same as desktop
Works

#### Bug Fix — hideEmptyColumns vs Column Selection
8. Desktop: uncheck a column → `Hide empty` checkbox becomes disabled (greyed, `not-allowed` cursor) → "(column selected)" hint appears
Works
9. Desktop: uncheck a column + verify an unselected empty column stays visible (the regression — previously `hideEmptyColumns` would still strip it)
Works
10. Mobile: open filter panel → toggle a column off → `Hide empty` checkbox disabled, hint visible
Works
11. Recheck all columns → `Hide empty` checkbox re-enables; flipping it on collapses empty columns as before
Works

#### Edge Cases
12. Project with 1 column → dropdown shows single checkbox → unchecking it leaves board empty but `Hide empty` disables
13. Project with 0 columns → dropdown empty (no crash)
14. Rapidly toggle a checkbox 5x → board reactivity stays consistent (no stale state, no console errors)
15. Toggle column while another panel's mutation is in-flight → no race; board updates normally
16. Select column → refresh page → selection does NOT persist (client-side only — expected per plan)

#### Regressions
17. Search box still filters cards across visible columns
18. Assignee dropdown still filters
19. `Include archived` checkbox still toggles archived visibility
20. Per-column Type dropdown still filters within column (unchanged scope)
21. Bulk-select checkboxes + BulkActionBar still work on mobile
22. Card three-dot menu (archive, restore, move up/down/to-column) unchanged
23. WIP limit badge still renders when at limit

#### Cleanup
- [ ] Recheck all columns before navigating away (next session starts with all columns visible by default)

---

## Plan 4D: Card Type Redesign


Renames `Bug`→`Issue`, `Epic`→`Goal`, retires `Spec` (rows data-migrated to Goal). Removes the Epic-only parent restriction so any card type can parent any other. Updates UI labels across `CardCreateModal`, `CardMetadata`, `BoardCard`, `BoardMobileList`, and the type filter dropdowns.

#### Setup
- [X] `pnpm dev` running (web + API + Postgres + MinIO)
- [X] Logged in as a user with access to a project
- [x] Project has 2+ columns and 3+ existing cards of mixed types
- [X] (Optional) seed a card via API with `Type=3` to verify data migration before reset
- [X] Browser DevTools at mobile width (~390px) ready for mobile checks

#### Happy Path — Type Rename
1. Open `+ Add Card` modal → Type dropdown lists exactly: `Task`, `Issue`, `Goal`, `Idea` (no `Bug`, `Epic`, `Spec`)
Works!
2. Create a card of each type → each shows correct label, color, and Lucide icon on the board (`square-check` / `bug` / `layers` / `lightbulb`)
Works!
3. Column header Type filter dropdown lists: `All`, `Task`, `Issue`, `Goal`, `Idea`
Works!
4. BoardCard and BoardMobileList show "Parent" (not "Epic") when `parentCardId` is set, with `i-lucide-layers` icon
Works!


#### Happy Path — Open Parent Restriction
5. Create a `Task` card with parent = another `Task` → succeeds (200), card displays on board
6. Create an `Idea` card with parent = an `Issue` → succeeds (200)
7. Reopen `CardMetadata` for any card → "Parent" row exists in the metadata panel → dropdown shows every other card in the project, not filtered by type → changing it persists immediately
8. Set parent to a card from a different project via API → returns `400` `CARD_INVALID_PARENT` (was `CARD_INVALID_PARENT_EPIC`)
9. Set parent to self via API → returns `400` `CARD_PARENT_CYCLE`

#### Data Migration — Spec Retirement
10. SQL check before validation: `SELECT COUNT(*) FROM cards WHERE "Type" = 3;` returns 0 (or row count matches pre-migration)
11. SQL check: `SELECT COUNT(*) FROM cards WHERE "Type" = 5;` ≥ previous `Type=3` count + previous `Type=5` count
12. Open a card that was previously `Spec` → metadata panel shows type `Goal` with layers icon (read from API; no client-side remapping)

#### Edge Cases
13. Parent dropdown with 0 candidates (project has only one card) → shows only the `None` option
14. Parent dropdown when network fails → shows `None` only, no crash, no console error in production
15. Set parent → refresh page → parent persists
16. Set parent → set to `None` → saved; subsequent re-open shows `None` selected
17. Card with parent → archive card → metadata panel renders `Parent` row as read-only text (no dropdown)
18. Archived card with parent → restore → parent row becomes editable dropdown again
19. Card whose parent is archived → child card displays "Parent" badge; opening child metadata shows parent's title (or truncated id) read-only
20. Cycle: set A→B, then try B→A via API → returns `400 CARD_PARENT_CYCLE`

#### Regressions
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

#### Cleanup
- [ ] Remove any seeded test cards (or leave if the project is disposable)

---

## Plan 4B: E2E Testing Foundation


### Validate: Playwright e2e suite runs green locally and in CI

#### Setup — Local Dev Mode
- [ ] Postgres + MinIO up: `docker compose up -d postgres minio`
- [ ] `cd src/web-ui && pnpm install` already run (playwright on PATH)
- [ ] One-time: `cd src/web-ui && pnpm exec playwright install --with-deps chromium`
- [ ] Playwright's `webServer` auto-starts API + Nuxt; no need to run them separately

#### Setup — Full Docker Stack
- [ ] `docker compose up` (starts postgres + minio + server + web)
- [ ] Access at `http://localhost:3000`
- [ ] API at `http://localhost:5000`
- [ ] For E2E: kill Docker web service or let Playwright start its own servers on host

#### Happy Path
1. `pnpm exec playwright test --project=setup` → `e2e/.auth/testadmin.json` produced, contains non-empty `cookies` array with `auth_token`
2. `pnpm test:e2e` (all 5 specs) → all pass, no flakes, no manual waits left
3. Open `src/web-ui/playwright-report/` on a passing run → report renders, trace files present
4. Push branch and open PR → `e2e` job appears in Checks tab and passes

#### Edge Cases
1. No API or Nuxt running beforehand → Playwright webServer starts both automatically, waits for health, runs tests, then cleans up
2. API or Nuxt already running (`reuseExistingServer: true`) → Playwright skips startup, reuses running instances
3. CI e2e job → separate e2e job in workflow (postgres service, migrations, Playwright); webServer is undefined in CI
4. Re-run `pnpm test:e2e` twice in a row without DB reset → second run also passes (random-suffix seeding avoids collisions)
5. Concurrency spec run alone → opens two browser contexts, both auth'd, second save rejected with visible error in `[role="alert"]`

#### Regressions
1. `pnpm typecheck && pnpm lint && pnpm test` (unit-test suite) → still green; Playwright specs live in `e2e/`, outside Vitest's `app/` glob
2. `pnpm build` → still green; no production code changes
3. Pre-existing e2e-blocking surface (`TestUserSeeder`, `auth_token` cookie, `ApiRoutes` constants) → untouched
4. `docker compose up` builds clean, web service reaches API via Docker network (`http://server:8080`)
5. `docker compose up -d postgres minio` (local dev) still works — no breaking changes to existing services

#### Cleanup
- [ ] No seeded projects/cards left behind in dev DB that need manual cleanup (suffixed titles make them identifiable)
- [ ] `e2e/.auth/testadmin.json` is gitignored, no commit needed
- [ ] `playwright-report/` and `test-results/` are gitignored

---

## Plan 5: Specs, Plans & Real-time


### Setup
- [X] API server running locally (`ASPNETCORE_ENVIRONMENT=Development`)
- [X] Web dev server running (`pnpm dev` at `src/web-ui`)
- [X] Logged in, project with at least one Goal card and one Idea card (and Task/Issue for negative case)

### Task — Type-Conditional Docs Tab (CardModal)
Goal: Verify the Docs tab shows only on Goal/Idea and Plan only on Goal.

1. Open a Task card modal → tabs: Details, Checklist, Comments, Related. No "Docs" tab.
No related, why is this mentioned, other than tat it works.
2. Open an Issue card modal → tabs same as Task. No "Docs" tab.
Like wise no docs tab. Works
3. Open an Idea card modal → "Docs" tab present; clicking it shows Spec editor only (no Plan section, no `USeparator`).
Yep i see Spec editor this works I believe
4. Open a Goal card modal → "Docs" tab present; clicking it shows Spec editor, `USeparator`, then Plan editor.
Yep I can see spec and plans! this works
5. With Goal modal open and Docs tab active → switch card type to Task via metadata panel → active tab auto-resets to Details, Docs tab disappears.
Works as expected


### Task — CardSpec Inline Editor (ownership card)
6. On Goal card → Docs tab → Spec editor visible with title/description/content fields and Save button.
Works, though description was thrown off as it don't make sense.
7. Type a spec title + description + content → click Save → green toast "Spec saved"; refresh page → spec still present.
Works
8. Reopen the card → click "History" button → version list appears on right side with v1 entry.
Works
9. Save again → if History open, versions list refreshes and v2 appears.
Works
10. Click "Hide history" → history panel closes; "History" button label returns.
Works
11. Click "Restore" on a previous version → green toast "Version restored"; current title/content reverts; if history panel open, list refreshes.
Works

### Task — CardPlan Inline Editor (ownership card, Goal only)
12. On Goal card → Docs tab → Plan editor visible below Spec with Save button.
Works
13. Type a plan title + description + content → click Save → green toast "Plan saved"; refresh → plan still present.
Works
14. Repeat history/save/restore cycle (steps 8-11) for Plan → version list works, restore works.
Works

### Task — Spec/Plan API Wrapper Unwrap (regression for prior finding)
15. Confirm History renders actual version entries (not empty) when versions exist on backend.
   - Backstop check via devtools Network: `GET /api/projects/{pid}/specs/{sid}/versions` returns `{ "versions": [...] }`; UI shows them.
   Works!

### Task — SignalR BoardHub Real-time Sync
16. Browser A: logged-in, viewing board → devtools Network shows `WS /hubs/board` upgrade succeeded.
Works
17. Browser B (different user, same project): move a card from Column 1 to Column 2.
Works
18. Browser A: card appears in Column 2 without manual refresh (board store refetched).
Works
19. Open devtools on Browser A → Console → temporarily lose network for 5s → "Reconnecting…" state implied by realtime state.
Works
20. Restore network → connection re-establishes; card-move events resume.
Works

### Task — SignalR PresenceHub Online Users
21. Browser A joins project → presence store updates (visible if app surfaces it).
22. Browser B joins same project → both browsers see each other (UserJoined event).
23. Browser B navigates away or closes → Browser A receives UserLeft; online list updates.

### Task — Card Focus Tracking
24. Browser A opens a card → `FocusCard` invoked (devtools shows `SignalR invoke`).
25. Browser B receives `CardFocused` event for that card → presence store `focusedCards` map updated.
26. Browser A closes the card → no BlurCard sent (acceptable per current hub contract; focus state is best-effort fire-and-forget).

### Task — Realtime Cleanup on Board Unmount (regression for prior finding)
27. Navigate away from board page → SignalR connections for `/hubs/board` and `/hubs/presence` close cleanly (devtools Network shows ws closed).
28. Presence store online users map and focused cards cleared on disconnect (covered by automated test).

### Task — Realtime State Reset on Hub onclose (regression for prior finding)
29. Simulate server restart or token expiry causing SignalR onclose → `isReconnecting.value = false`, `isConnected.value = false`. (Hard to surface without UI; covered by automated test.)

### Regressions
30. Existing tabs (Details, Checklist, Comments, Related) still work on Task/Issue cards.
31. Editing Spec/Plan doesn't break CardDescription / CardMetadata save (concurrency).
32. Board filtering, move-card, archive-card flows unaffected.

### Cleanup
- [ ] None required (test users and cards seeded by dev fixture)

---

## Plan 5A: Doc Model Schema


API behavior (DocType, PlanStatus, SpawnedFrom, SetStatus lifecycle) is covered by
`ProjectServiceTests`/`PlanServiceTests`/`SpecServiceTests`/`PlansControllerTests`/`SpecsControllerTests`
(xUnit) and `.http` smoke files — not re-validated manually here. This matrix only
covers what those don't: does the actual UI expose and wire the feature correctly.

#### Setup
- [ ] API server running (`dotnet run --project src/HydraForge.Server`)
- [ ] Web dev server running (`pnpm dev`)
- [ ] Authenticated user, member of a project
- [ ] A Goal card exists in the project
- [ ] An Idea card exists in the project
- [ ] An Issue card exists in the project
- [ ] A Task card exists in the project

#### UI — Docs tab visibility per card type

Actual gating logic (`CardModal.vue`): Spec shown for Goal/Idea/Issue, Plans shown for
Goal/Issue/Task — so every type gets a Docs tab, but the sections inside differ.

1. [X] **Goal card → Docs tab** → Spec section labeled "Specification" + Plans section with "Add Plan"
2. [X] **Idea card → Docs tab** → Spec section labeled "Concept" only, no Plans section
3. [x] **Issue card → Docs tab** → Spec section labeled "Report" + Plans section with "Add Plan"
4. [x] **Task card → Docs tab** → Plans section only (no Spec section), "Add Plan" present

#### UI — Spec section (Goal / Idea / Issue)

5. [x] **Create spec (empty state)** → title input + editor + "Create" button; fill both, click Create → spec saves, toast "Spec saved", button label switches to "Save"
6. [x] **Save disabled when clean** → immediately after create/load, Save is disabled (nothing edited yet)
7. [x] **Edit spec** → change title or content → Save enables → click Save → toast "Spec saved"
8. [x] **Version history** → click "History" → versions panel opens showing past versions with author + timestamp
9. [x] **Restore version** → click Restore on an older version → title/content revert, toast "Version restored", history refreshes
10. [X] **Readonly (archived card)** → no Create/Save button, no title/content editing

#### UI — Plans section (Goal / Issue / Task)

11. [x] **Add Plan** → click "Add Plan" → inline form (title + editor + Create/Cancel) appears
12. [x] **Create disabled until titled** → Create button disabled while title is empty
13. [x] **Create plan** → fill title + content, click Create → plan appears in list with "Pending" badge, toast "Plan created"
14. [x] **Expand/collapse** → click a plan's header row → chevron rotates, editor (and history panel if open) shows/hides
15. [x] **Status dropdown** → click the status badge → dropdown lists the other two statuses only (e.g. from Pending: Active, Done)
16. [x] **Set Active** → pick Active from dropdown → badge updates to "Active" immediately, no page reload
17. [x] **Set Done** → pick Done from dropdown → badge "Done", editor becomes read-only, Save button disappears, plan row dims (opacity)
18. [x] **Done → Pending/Active directly** → from a Done plan, status dropdown still works and allows jumping straight back to Pending or Active (no separate "Reactivate" button — single dropdown handles all transitions)
19. [x] **Save gated by dirty state** → Save button disabled until title or content actually changes; disabled entirely while status is Done
20. [x] **Multiple plans** → create 2+ plans on one card → all shown stacked, ordered by position, each independently expandable/editable
21. [x] **Version history per plan** → click History on one plan → panel shows versions for that plan only (not other plans)
22. [x] **Restore blocked on Done** → open history on a Done plan → Restore button disabled
23. [x] **Restore on non-Done plan** → Restore an older version → content reverts, toast "Version restored"
24. [x] **Readonly (archived card)** → no "Add Plan" button, status dropdown disabled, no Save buttons

#### Regressions (UI)

25. [x] **Tab switch preserves data** → edit a Spec/Plan without saving, switch to another tab and back → unsaved edits still present (no unexpected refetch wipes them)
26. [x] **Closing and reopening the card** → re-fetches fresh Spec/Plan state from the server (saved changes persist, no stale cache)
27. [x] **Idea card never shows a Plans section** → confirms no regression of the Idea-has-no-plans rule (D-44) after any of the above interactions

---

## Plan 5A UX Polish: Collapsible Plans, Status Dropdown, Fullscreen Editor


#### Setup
- [ ] API server running (`dotnet run --project src/HydraForge.Server`)
- [ ] Web dev server running (`pnpm dev`)
- [ ] Authenticated user with valid JWT token
- [ ] A project exists where user is a member
- [ ] A Goal card exists with at least 2 plans (one Pending, one Active, one Done)
- [ ] A Goal card exists with a Spec

#### Collapsible Plan Sections

1. [ ] **Default collapsed** → open card modal → Plans section shows headers only, no editor / Save / History buttons visible
2. [ ] **Click header to expand** → click plan header row → editor + Save + History appear below header
3. [ ] **Chevron rotates** → chevron rotates 180° when expanded, returns to 0° when collapsed
4. [ ] **Click header to collapse** → click expanded header → body hides, only header remains
5. [ ] **Multiple plans can be expanded independently** → expand Plan A → expand Plan B → both bodies visible
6. [ ] **Click Save/History button does NOT collapse** → expand plan → click Save → plan stays expanded (no toggle)
7. [ ] **Click title input does NOT collapse** → expand plan → click title field to focus → plan stays expanded
8. [ ] **Click status dropdown does NOT collapse** → expand plan → click status badge to open menu → plan stays expanded
9. [ ] **Read-only mode** → open card as archived/non-member → header still clickable but body shows read-only editor; Save/Activate/Complete/Reactivate hidden
10. [ ] **Done plan opacity** → Done plan section renders with `opacity-75` class

#### Interactive Status Dropdown

11. [ ] **Dropdown opens on badge click** → click status badge → menu shows the 2 other statuses (excludes current)
12. [ ] **Pending → Active via dropdown** → plan status Pending → dropdown → select "Active" → API call fires, badge changes to "Active"
13. [ ] **Active → Done via dropdown** → plan status Active → dropdown → select "Done" → API call fires, badge changes to "Done", editor becomes read-only
14. [ ] **Done → Active via dropdown** → plan status Done → dropdown → select "Active" → API call fires, badge changes to "Active", editor becomes editable
15. [ ] **Pending → Done via dropdown** → plan status Pending → dropdown → select "Done" → BOTH activate and complete fire sequentially, final status is "Done"
16. [ ] **Revert to Pending via reopen API** → plan status Active → dropdown → select "Pending" → API call fires, badge changes to "Pending", editor stays editable
17. [ ] **Dropdown disabled in readonly mode** → open archived card → status badge not clickable, no menu opens
18. [ ] **Search input hidden** → open dropdown → no search text input visible (just the 2 status options)
19. [ ] **Failed reopen** → simulate 500 error → error toast "Failed to reopen plan", plan status remains unchanged

#### Fullscreen Toggle on MarkdownEditor

20. [ ] **Fullscreen button visible** → expand a plan → editor toolbar shows maximize icon at right
21. [ ] **Click fullscreen** → click maximize icon → editor fills viewport (fixed inset-0, z-50, bg-background), icon switches to minimize
22. [ ] **Editor usable in fullscreen** → can type in editor area; toolbar buttons (bold, italic, etc.) work
23. [ ] **Source toggle works in fullscreen** → toggle to source mode in fullscreen → source textarea fills available height, minimize button remains in source bar? (button only in WYSIWYG toolbar — verify source mode toggle works)
24. [ ] **Escape exits fullscreen** → press Escape → editor returns to inline mode, icon switches back to maximize
25. [ ] **Minimize button exits** → click minimize icon → editor returns to inline mode
26. [ ] **Fullscreen works in CardSpec** → open spec, click fullscreen → spec editor fills viewport, Escape exits
27. [ ] **Multiple editors, only one fullscreen at a time** → expand plan A fullscreen → expand plan B → each editor has its own fullscreen state independently
28. [ ] **Fullscreen has backdrop overlay** → enter fullscreen → background content (other plans, card panels) is obscured by semi-transparent backdrop
29. [ ] **Backdrop click exits fullscreen** → enter fullscreen → click on dark backdrop area (outside editor panel) → editor returns to inline mode

#### Regressions

30. [ ] **Save plan still works** → expand plan → edit title/content → click Save → toast success, version increments
31. [ ] **History panel still works** → click History → versions panel renders, Restore still works
32. [ ] **Plan create form still works** → click Add Plan → inline form appears, Create button works
33. [ ] **Spec section unaffected** → Spec section above plans still renders, edits still save
34. [ ] **No console errors** → open card modal → no Vue warnings in dev console

#### Editor Scroll

35. [ ] **Editor has max-height with scroll** → expand plan → type enough content to exceed ~400px → editor shows scrollbar, does NOT grow infinitely
36. [ ] **Fullscreen editor has full height** → enter fullscreen → editor fills available space with scroll on content overflow