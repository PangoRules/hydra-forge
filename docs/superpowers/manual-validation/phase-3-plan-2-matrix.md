# E2E Test Matrix — Phase 3 Plan 2 (Project List & Board)

## Pre-flight

```bash
# Start the stack (if not running)
docker compose up -d

# Start web dev server
cd src/web-ui && pnpm dev

# Login as testadmin, get a token for curl commands
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"testadmin","password":"TestAdmin123!"}' | \
  python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")

echo $TOKEN  # keep this handy
```

Open `http://localhost:3000` in a browser.

---

## 1. Auth & Project List

| # | Step | Expected | Pass? |
|---|---|---|---|
| 1.1 | Navigate to `http://localhost:3000` | Redirect to `/login` | ☐ |
| 1.2 | Login with `testadmin` / `TestAdmin123!` | Redirect to `/projects`, project list renders | ☐ |
| 1.3 | Project list shows existing smoke-test projects | Cards with project names + "No description" fallback | ☐ |
| 1.4 | Click "New Project" button | Create modal opens with Name + Description fields + Cancel/Create buttons | ☐ |
| 1.5 | Fill in `E2E Test Project` / `Test board` → click Create | Modal closes, new project appears at top of list | ☐ |
| 1.6 | Click the new project card | Navigate to `/projects/{id}/board`, Board header visible | ☐ |
| 1.7 | Logout (top-right button) → login again | Project list still shows the new project | ☐ |

---

## 2. Board — Desktop (viewport ≥ 768px)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 2.1 | Navigate to board of the `E2E Test Project` | Board header + refresh button visible | ☐ |
| 2.2 | Check Network tab | `GET /api/projects/{id}/Columns` 200 + `GET /api/projects/{id}/Cards` 200 | ☐ |
| 2.3 | Columns render | 6 default columns: Backlog, Spec-ing, Planned, In Dev, In Review, Done with card counts showing "0" | ☐ |
| 2.4 | Refresh button | Click refresh icon → columns re-fetch (check Network tab for new requests) | ☐ |
| 2.5 | No console errors | Only `<Suspense>` warning (expected), no icon warnings | ☐ |

---

## 3. Board — Mobile (viewport < 768px)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 3.1 | Open Chrome DevTools → Toggle device toolbar (Cmd+Shift+M) → set width to 375px | Mobile list view renders | ☐ |
| 3.2 | Column headers render in list format | Backlog, Spec-ing, etc. listed vertically | ☐ |
| 3.3 | Card counts show "0" | Each column shows badge with 0 | ☐ |
| 3.4 | Switch back to desktop (≥768px) | Kanban columns render horizontally again | ☐ |

---

## 4. Card CRUD (via curl — UI card creation not built yet)

```bash
# Store project ID (replace with your UUID if using a different project)
PROJECT_ID="<YOUR_PROJECT_ID_FROM_URL>"

# Get the first column ID (Backlog)
COL_ID=$(curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/projects/$PROJECT_ID/Columns | \
  python3 -c "import sys,json; cols=json.load(sys.stdin); print(cols[0]['id'])")
echo "Backlog column: $COL_ID"
```

| # | Step | Expected | Pass? |
|---|---|---|---|
| 4.1 | Create a "Task" card | `curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/Cards -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"columnId\":\"$COL_ID\",\"title\":\"Fix login bug\",\"description\":\"Users can't log in after password reset\",\"type\":0}"` → 201 with card JSON | ☐ |
| 4.2 | Refresh the board | Card "Fix login bug" appears in Backlog column with task icon (☐) and card number #1 | ☐ |
| 4.3 | Create a "Bug" card | `curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/Cards -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"columnId\":\"$COL_ID\",\"title\":\"Dashboard crash on load\",\"description\":\"Null pointer in renderDashboard()\",\"type\":1}"` → 201 | ☐ |
| 4.4 | Refresh the board | Bug card appears in Backlog with bug icon | ☐ |
| 4.5 | Create an "Epic" card | `curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/Cards -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"columnId\":\"$COL_ID\",\"title\":\"Q3 Performance Overhaul\",\"description\":\"Epic tracking all perf work\",\"type\":2}"` → 201 | ☐ |
| 4.6 | Refresh the board | Epic card appears with layers icon | ☐ |
| 4.7 | Card count updates | Backlog column badge now shows "3" | ☐ |
| 4.8 | Switch to mobile view | All 3 cards visible under Backlog header in list format | ☐ |

---

## 5. Card Move Between Columns

```bash
# Get In Dev column ID (index 3, position 3)
IN_DEV_ID=$(curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/projects/$PROJECT_ID/Columns | \
  python3 -c "import sys,json; cols=json.load(sys.stdin); print(cols[3]['id'])")
echo "In Dev column: $IN_DEV_ID"

# Get the Bug card ID (type=1, the second card created)
CARD_ID=$(curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/projects/$PROJECT_ID/Cards?type=1" | \
  python3 -c "import sys,json; data=json.load(sys.stdin); print(data['cards'][0]['id'])")
VERSION=$(curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/projects/$PROJECT_ID/Cards?type=1" | \
  python3 -c "import sys,json; data=json.load(sys.stdin); print(data['cards'][0]['version'])")
echo "Bug card: $CARD_ID (v$VERSION)"
```

| # | Step | Expected | Pass? |
|---|---|---|---|
| 5.1 | Move Bug card to In Dev | `curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/Cards/$CARD_ID/move -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"targetColumnId\":\"$IN_DEV_ID\",\"targetPosition\":0,\"confirmBlockedMove\":false,\"version\":$VERSION}"` → 200 | ☐ |
| 5.2 | Refresh the board | Bug card moved from Backlog to In Dev column | ☐ |
| 5.3 | Backlog now shows "2", In Dev shows "1" | Card counts update correctly | ☐ |
| 5.4 | Move Task card to In Dev (position 0, before the Bug) | New version needed. Get new version first. Move succeeds. | ☐ |
| 5.5 | Refresh | Task card appears ABOVE Bug card in In Dev | ☐ |

---

## 6. Error States

| # | Step | Expected | Pass? |
|---|---|---|---|
| 6.1 | Stop the server: `docker compose stop server` → refresh board page | "Failed to load board" error message + Retry button | ☐ |
| 6.2 | Click Retry | Same error (server still down) | ☐ |
| 6.3 | Start server: `docker compose start server`, wait 5s, click Retry | Board loads successfully | ☐ |
| 6.4 | Logout → navigate to `/projects/{id}/board` | Redirected to `/login` (auth middleware) | ☐ |

---

## 7. Column Reorder (Persistence)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 7.1 | Reorder columns via API | `curl -s -X PUT http://localhost:5000/api/projects/$PROJECT_ID/Columns/reorder -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"columnIds\":[\"$IN_DEV_ID\",\"$COL_ID\"]}"` → 204 | ☐ |
| 7.2 | Refresh the board | Column order persists (In Dev first, then Backlog) * ⚠️ This reorder only sends 2 IDs — system keeps rest in place? Test carefully | ☐ |

---

## 8. Browser Tab / Responsive

| # | Step | Expected | Pass? |
|---|---|---|---|
| 8.1 | Open board in 2 tabs | Both show same columns | ☐ |
| 8.2 | Resize tab 1 to mobile width (<768px) | Mobile list view renders | ☐ |
| 8.3 | Tab 2 at desktop (≥768px) | Desktop kanban still renders independently | ☐ |

---

## Summary

| Section | Pass/Fail |
|---|---|
| 1. Auth & Project List | ☐ |
| 2. Board Desktop | ☐ |
| 3. Board Mobile | ☐ |
| 4. Card CRUD | ☐ |
| 5. Card Move | ☐ |
| 6. Error States | ☐ |
| 7. Column Reorder | ☐ |
| 8. Browser Tabs | ☐ |
