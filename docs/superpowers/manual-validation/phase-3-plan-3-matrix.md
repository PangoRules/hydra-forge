# E2E Test Matrix — Phase 3 Plan 3 (Card Modal Core)

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

Make sure you have at least one card with a description containing mixed formatting (bold, italic, headings, bullet list, numbered list, code block, blockquote). Create via curl if needed:

```bash
# Get a project and column ID first
PROJECT_ID="<YOUR_PROJECT_ID>"
COL_ID=$(curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/projects/$PROJECT_ID/Columns | \
  python3 -c "import sys,json; print(json.load(sys.stdin)[0]['id'])")

# Create a card with formatted description
curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/Cards \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"columnId\":\"$COL_ID\",\"title\":\"E2E Test Card\",\"description\":\"<h2>Users can't log in after password reset</h2><ul><li><p>test</p></li><li><p>test1</p></li><li><p>test2</p></li></ul><ol><li><p>test</p></li><li><p>test1</p></li><li><p>test2</p></li></ol><pre><code>Codigo lolsito</code></pre><blockquote><p>Quota perra mamalona</p></blockquote>\",\"type\":0}"
```

---

## 1. Card Modal Open & Close

| # | Step | Expected | Pass? |
|---|---|---|---|
| 1.1 | Click a card on the board | Modal opens with scale-in animation, card data loads (spinner shown briefly while loading) | ☐ |
| 1.2 | Header shows card title | Title truncated with ellipsis if too long | ☐ |
| 1.3 | Click backdrop (outside modal) | Modal closes with scale-out animation (~200ms), board view visible again | ☐ |
| 1.4 | Press `Escape` key | Modal closes with scale-out animation | ☐ |
| 1.5 | Click X button in header | Modal closes with scale-out animation | ☐ |
| 1.6 | Re-open same card immediately after closing | Modal opens normally (not stuck in closed state) | ☐ |
| 1.7 | Open card while API is slow/disconnected | Loading spinner displayed centered in modal body | ☐ |
| 1.8 | Open card that fails to load (e.g. deleted GUID) | Red error alert in modal body with error message | ☐ |

---

## 2. Desktop Layout (viewport ≥ 768px)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 2.1 | Open modal on desktop | Left column: CardDescription editor. Right column (border-left): CardMetadata sidebar. Both independently scrollable within max-h-[70vh]. | ☐ |
| 2.2 | Metadata panel shows card info | Badge for Type, Column ID (first 8 chars), Assignees (avatars or "None"), Due Date (or "None") | ☐ |
| 2.3 | Resize to mobile (<768px) | Layout switches to tabbed view (Details, Checklist, Comments, Related) with UTabs navigation bar | ☐ |
| 2.4 | Click Details tab | CardDescription + CardMetadata stacked vertically | ☐ |
| 2.5 | Click Checklist tab | Placeholder "Checklist coming soon" | ☐ |
| 2.6 | Click Comments tab | Placeholder "Comments coming soon" | ☐ |
| 2.7 | Click Related tab | Placeholder "Attachments, dependencies, specs, plans coming soon" | ☐ |

---

## 3. Save Button — Dirty State & Spinner

| # | Step | Expected | Pass? |
|---|---|---|---|
| 3.1 | Open card with existing description | Save button visible but **disabled** (grayed, no unsaved changes) | ☐ |
| 3.2 | Typing in description editor | Save button becomes **enabled** | ☐ |
| 3.3 | Click Save button | Button shows spinner, becomes disabled. On success: spinner stops, button disabled again (clean state) | ☐ |
| 3.4 | Type, wait ~2s without clicking Save | Auto-save fires. Button shows spinner during save, then returns to disabled. No double-save. | ☐ |
| 3.5 | Type a change, manually click Save before 2s debounce | Save fires immediately. Timer cleared. No double-save on debounce expiry. | ☐ |
| 3.6 | Press `Ctrl+Enter` (or `Cmd+Enter` on Mac) | Immediate save, same as clicking Save button | ☐ |
| 3.7 | After save, refresh page | Description persists (saved to database) | ☐ |
| 3.8 | Make a change, save, verify board card snippet | Board card snippet updates with new description (HTML stripped, plain text) | ☐ |
| 3.9 | Double-click Save rapidly | First save fires. Second click does nothing (`!dirty || saving` guards). | ☐ |

---

## 4. MarkdownEditor — Formatting Toolbar

| # | Step | Expected | Pass? |
|---|---|---|---|
| 4.1 | Select text, click Bold button | Selected text becomes **bold**. Button highlights with primary color when active. Tooltip shows "Bold" on hover. | ☐ |
| 4.2 | Select text, click Italic button | Selected text becomes *italic*. Button highlights. Tooltip shows "Italic". | ☐ |
| 4.3 | Click Heading 1/2/3 button on a line | Line converts to heading (H1=1.75rem bold, H2=1.4rem semibold, H3=1.15rem semibold). Button highlights. Tooltip shows "Heading 1". | ☐ |
| 4.4 | Click Bullet List button | Line becomes bullet list item. Subsequent Enter adds new bullet. Tooltip shows "Bullet List". | ☐ |
| 4.5 | Click Ordered List button | Line becomes numbered list item. Numbers auto-increment. Tooltip shows "Ordered List". | ☐ |
| 4.6 | Click Code Block button | Content becomes code block (gray background, monospace). Tooltip shows "Code Block". | ☐ |
| 4.7 | Click Blockquote button | Content becomes blockquote (left border, indented). Tooltip shows "Blockquote". | ☐ |
| 4.8 | Toggle bold/italic/heading when already active | Toggles off, button returns to neutral color | ☐ |
| 4.9 | Source toggle button | Right side of toolbar, shows "Source" label, tooltip "Toggle Markdown Source" | ☐ |

---

## 5. MarkdownEditor — Source Mode Round-Trip

| # | Step | Expected | Pass? |
|---|---|---|---|
| 5.1 | Click Source button | Switches to raw markdown `<textarea>`. Toolbar replaced with "Markdown" label + "Preview" button. | ☐ |
| 5.2 | Verify code blocks use fenced ` ``` ``` syntax | Not indented 4-spaces. Fenced with backticks. | ☐ |
| 5.3 | Verify lists are compact (no extra blank lines between items) | `* item1` directly followed by newline + `* item2`, not separated by blank lines | ☐ |
| 5.4 | Click Preview button (back to WYSIWYG) without editing source | Content looks identical to before entering source mode. No extra blank lines added. | ☐ |
| 5.5 | Switch source → preview without editing | No auto-save triggered. Save button stays disabled. | ☐ |
| 5.6 | In source mode, edit the markdown (add text, change formatting), switch back | Content reflects edits. Save button becomes enabled (dirty). | ☐ |
| 5.7 | In source mode, type `line1\nline2` (single newline between lines), switch back | Both lines preserved with `<br>` between them, not collapsed into one line. | ☐ |
| 5.8 | Switch source → preview → source → preview 3+ times without editing | Content stable. No spacing degradation after multiple round-trips. | ☐ |
| 5.9 | In source mode, verify `<br>` output is bare `\n` not `  \n` (no trailing spaces) | Source textarea shows clean `\n`, not lines ending with two spaces | ☐ |

---

## 6. Archive / Restore

| # | Step | Expected | Pass? |
|---|---|---|---|
| 6.1 | Click Archive button in modal header | API call succeeds, modal closes, card removed from board | ☐ |
| 6.2 | Click Archive while network is down | Error toast "Failed to archive card". Modal stays open. | ☐ |
| 6.3 | Open archived card (via API or show archived toggle) | Shows Restore button instead of Archive | ☐ |
| 6.4 | Click Restore on archived card | API call succeeds, card reappears on board, modal stays open (restore doesn't close) | ☐ |
| 6.5 | Click Restore while network is down | Error toast "Failed to restore card". Modal stays open. | ☐ |

---

## 7. Concurrency & Version Tracking

| # | Step | Expected | Pass? |
|---|---|---|---|
| 7.1 | Open card in two browser tabs | Both show same content and version | ☐ |
| 7.2 | Tab A: edit & save description | Tab A saves with version=N, gets version=N+1 back. | ☐ |
| 7.3 | Tab B: edit & save (without refresh) | Tab B still has stale version=N → API returns 409 CARD_CONCURRENCY_MISMATCH | ☐ |
| 7.4 | Close modal, re-open (fresh fetch) | Tab B reloads with updated version=N+1 content | ☐ |

---

## 8. Auth — Token Hydration on Refresh

| # | Step | Expected | Pass? |
|---|---|---|---|
| 8.1 | Log in, refresh the page (F5) | Store hydrates from cookie. Logout button visible in top-right header. | ☐ |
| 8.2 | Click Logout | Token cleared, store cleared, redirected to `/login` | ☐ |
| 8.3 | After logout, press browser back | Redirected to `/login` (not stuck on protected page) | ☐ |

---

## 9. Error States

| # | Step | Expected | Pass? |
|---|---|---|---|
| 9.1 | Stop server: `docker compose stop server` → edit card description, try to save | Save button shows spinner. After timeout, save error message appears below editor: "Failed to save" (or specific error). | ☐ |
| 9.2 | Start server: `docker compose start server` → save again | Save succeeds. Button returns to disabled state. Error message clears. | ☐ |
| 9.3 | Logout while modal is open | After redirect, protected routes inaccessible | ☐ |

---

## 10. Regressions

| # | Step | Expected | Pass? |
|---|---|---|---|
| 10.1 | Navigate to `/projects` | Project list loads correctly | ☐ |
| 10.2 | Click New Project button | ProjectCreateModal opens via AppModal (same wrapper) | ☐ |
| 10.3 | Create a new project | Project appears in list, navigating to board works | ☐ |
| 10.4 | Open board on mobile (<768px) | BoardMobileList renders cards vertically with HTML-stripped snippets | ☐ |
| 10.5 | Open board on desktop (≥768px) | Kanban columns render horizontally | ☐ |
| 10.6 | Login with invalid credentials | Error toast, stays on login page | ☐ |
| 10.7 | Navigate to `/projects` without token | Redirected to `/login` | ☐ |
| 10.8 | Refresh project list | Projects re-fetched, no console errors | ☐ |

---

## 11. Edge Cases

| # | Step | Expected | Pass? |
|---|---|---|---|
| 11.1 | Card with no description | Editor shows placeholder "Add a description...". Save button disabled. | ☐ |
| 11.2 | Card with very long title | Title truncated with ellipsis in modal header | ☐ |
| 11.3 | Card with null/empty assignees list | Metadata shows "None" for assignees | ☐ |
| 11.4 | Type `<script>alert('xss')</script>` in source mode, switch back | HTML rendered safely. Tiptap strips/extracts script tags. No alert popup. | ☐ |
| 11.5 | Open card, edit description, close modal before 2s auto-save | Modal closes. Auto-save does NOT fire after close (component unmounts). | ☐ |
| 11.6 | Reopen same card after unsaved close | Content restored from database (not from unsaved in-memory state) | ☐ |

---

## Summary

| Section | Pass/Fail |
|---|---|
| 1. Card Modal Open & Close | ☐ |
| 2. Desktop Layout | ☐ |
| 3. Save Button (Dirty State & Spinner) | ☐ |
| 4. MarkdownEditor Formatting Toolbar | ☐ |
| 5. Source Mode Round-Trip | ☐ |
| 6. Archive / Restore | ☐ |
| 7. Concurrency & Version Tracking | ☐ |
| 8. Auth Token Hydration | ☐ |
| 9. Error States | ☐ |
| 10. Regressions | ☐ |
| 11. Edge Cases | ☐ |
