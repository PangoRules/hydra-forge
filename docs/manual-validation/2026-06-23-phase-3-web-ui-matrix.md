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
