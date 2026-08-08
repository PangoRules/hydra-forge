# Phase 7 Chat — Plan 19b Card Popup Infra: Manual Validation

**Date:** 2026-08-08
**Feature:** Multi-popup shell (max 3 cards + dock, shared z-index stack, Escape LIFO, viewport constraints, localStorage persistence)

## Environment

- API server running with `ASPNETCORE_ENVIRONMENT=Development`
- Web UI dev server (`pnpm dev`) running, logged in as any project member
- Open to a project board view that has at least 3 cards (card-1, card-2, card-3 by ID)

## Setup

1. Create or open a project with at least 3 cards (note their UUIDs from URL or DevTools network tab).
2. Navigate to the board view.
3. **Accessing the popup store from browser console:** Open Nuxt DevTools → Pinia tab → `cardPopup` store. From there you can inspect `openCardIds`, `positions`, and call the `openCard(cardId)` action directly by clicking the "Actions" button. Alternatively, use the browser console with:
   ```javascript
   // If Nuxt DevTools exposes pinia globally:
   const popupStore = useCardPopupStore()
   // If not, use Vue Devtools' Pinia panel or Nuxt DevTools built-in Pinia tab
   ```
4. The dock FAB button is visible in the bottom-right corner.

---

## Test Cases

### Happy Path

---

#### TC-1: Dock opens with correct z-index

| Step | Action | Expected |
|------|--------|----------|
| 1 | Click dock FAB (bottom-right) | ChatDock opens |
| 2 | Inspect dock's `z-index` CSS property in DevTools (Elements panel) | `z-index: 50` |

**Pass/Fail:** _____

---

#### TC-2: Open card popup 1 — cascaded position + z-index

| Step | Action | Expected |
|------|--------|----------|
| 1 | Via Nuxt DevTools Pinia panel, call `openCard('<card-1-uuid>')` | Card popup appears at approximate position (48, 48) from top-left |
| 2 | Inspect popup's `z-index` | `z-index: 51` |
| 3 | Verify dock's z-index is unchanged | Dock still at `z-index: 50` |
| 4 | Observe the popup header | Shows truncated card ID with "…" suffix; close button (X) is visible |
| 5 | Observe the popup body | Placeholder text: "Card <uuid>… — body arrives in Plan 20" |
| 6 | Click the popup anywhere | Nothing changes (already topmost) |
| 7 | Drag the popup by its header to a new position | Popup follows cursor smoothly; no flicker or jump |

**Pass/Fail:** _____

---

#### TC-3: Open card popup 2 — cascade offset + bring-to-front

| Step | Action | Expected |
|------|--------|----------|
| 1 | Call `openCard('<card-2-uuid>')` | Card-2 popup appears offset ~24px down-right from card-1 |
| 2 | Inspect card-2's z-index | `z-index: 52` |
| 3 | Inspect card-1's z-index | `z-index: 51` (unchanged) |
| 4 | Click card-1's header area | Card-1 raises to front: z-index becomes 52, card-2 drops to 51 |
| 5 | Click card-2's header area | Card-2 returns to front: z-index 52, card-1 drops to 51 |

**Pass/Fail:** _____

---

#### TC-4: Open card popup 3 — cascade continues

| Step | Action | Expected |
|------|--------|----------|
| 1 | Call `openCard('<card-3-uuid>')` | Card-3 appears further offset (~72, 72 from origin — 96, 96 total from default 48,48) |
| 2 | Inspect z-indices | Card-3: 53, card-2: 52, card-1: 51, dock: 50 |
| 3 | Verify cascade positions in store | `cardPopup.positions`: card-1 near (48,48), card-2 near (72,72), card-3 near (96,96) |

**Pass/Fail:** _____

---

#### TC-5: Escape LIFO — card popups close in reverse order

| Step | Action | Expected |
|------|--------|----------|
| 1 | With card-1, card-2, card-3 open + dock at z 50/51/52/53 | Verify all 3 cards + dock visible |
| 2 | Press Escape | Card-3 closes (was topmost at z=53) |
| 3 | Press Escape again | Card-2 closes (now topmost at z=52) |
| 4 | Press Escape again | Card-1 closes (now topmost at z=51) |
| 5 | Press Escape again | Dock closes (now topmost at z=50) |
| 6 | Verify dock FAB reappears | FAB visible bottom-right |
| 7 | Verify store state | `openCardIds` empty, `activeCardId` null, `canOpen` true |

**Pass/Fail:** _____

---

#### TC-6: Escape with card opened after dock — card closes first

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open dock via FAB | Dock opens, z-index 50 |
| 2 | Call `openCard('<card-1-uuid>')` | Card popup opens, z-index 51 |
| 3 | Press Escape | Card popup closes (topmost). Dock stays open at z-index 50 |
| 4 | Press Escape | Dock closes (now topmost). FAB reappears |

**Pass/Fail:** _____

---

#### TC-7: Drag brings card to front

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open card-1 and card-2 (in that order) | Card-2 topmost (z=52), card-1 behind (z=51) |
| 2 | Click and drag card-1's header | Card-1 raises to front: z-index becomes 52, card-2 drops to 51 |
| 3 | Verify store order | `openCardIds` ends with card-1 (last = topmost) |
| 4 | Drag card-2's header | Card-2 returns to front |

**Pass/Fail:** _____

---

#### TC-8: Hard reload restores open cards + positions

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open card-1 and card-2 | Both popups visible at their cascade positions |
| 2 | Drag card-1 to a distinctive position (e.g., top-right corner) | Card-1 moves |
| 3 | Hard-reload the browser (Cmd+Shift+R / Ctrl+F5) | Page reloads |
| 4 | After reload, observe | Both card popups re-open at their last positions (card-1 at top-right, card-2 at cascade position) |
| 5 | Inspect z-indices | card-1 and card-2 back at 51, 52; dock back at 50 (if dock was open on reload) |

**Pass/Fail:** _____

---

### Edge Cases

---

#### ESC-1: Max-3 rejection with error toast

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open card-1, card-2, card-3 (3 popups) | All three visible |
| 2 | Call `openCard('<card-4-uuid>')` | **No 4th popup appears** |
| 3 | Observe UI | Error toast displays: "Close a card popup first (max 3 open)" |
| 4 | Inspect store state | `openCardIds` still `['card-1', 'card-2', 'card-3']` (length 3) |
| 5 | Verify `canOpen` computed | `canOpen === false` (from Nuxt DevTools Pinia panel) |

**Pass/Fail:** _____

---

#### ESC-2: Open already-open card is no-op

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open card-1 | One popup visible |
| 2 | Call `openCard('<card-1-uuid>')` again | No duplicate popup. No toast. `openCardIds` still `['card-1']` |
| 3 | Verify card-1 is brought to front | z-index of card-1 stays at 51 (already topmost) |

**Pass/Fail:** _____

---

#### ESC-3: Escape with dropdown/listbox focused — popup does NOT close (gateway check)

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open card-1 popup | Popup visible |
| 2 | Inside the popup body (placeholder), focus a `USelect` or `USelectMenu` element | Dropdown/listbox has focus |
| 3 | Press Escape | **Popup does NOT close.** The select/dropdown handles Escape (closes the menu) |
| 4 | Click outside the select, back on a plain area inside popup | Focus moves |
| 5 | Press Escape | Popup now closes (no dialog/listbox/menu focused) |

**Pass/Fail:** _____

---

#### ESC-4: Escape when nothing open — no-op

| Step | Action | Expected |
|------|--------|----------|
| 1 | Close all popups and dock | No popups, no dock. FAB visible |
| 2 | Press Escape repeatedly | Nothing happens. No toasts, no errors, no console errors |
| 3 | Open something, then close it all again via Escape | Same — no-op after last close |

**Pass/Fail:** _____

---

#### ESC-5: Close non-active card — active card unchanged

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open card-1, then card-2 | card-2 active (topmost) |
| 2 | Via store, close card-1 (the non-topmost) | card-1 closes. card-2 stays active and remains topmost |
| 3 | Press Escape | card-2 closes (correct LIFO — only one left) |

**Pass/Fail:** _____

---

#### ESC-6: Close last card sets active to null

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open card-1 only | One popup, activeCardId = card-1 |
| 2 | Press Escape | Popup closes. `activeCardId` = null in store. `openCardIds` empty |
| 3 | Verify dock still operates independently | Open dock via FAB — dock works normally |

**Pass/Fail:** _____

---

#### ESC-7: Drag near viewport edges — constraint keeps popup on-screen

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open card-1 | Popup at default (48,48) |
| 2 | Drag popup far up past top edge of viewport | Popup stops at `top: 0` — does not go above viewport |
| 3 | Drag far left past left edge | Popup stops at `left: 0` |
| 4 | Drag far right past right edge | Popup stops so right edge of popup = viewport right edge (no horizontal scrollbar) |
| 5 | Drag far down past bottom edge | Popup stops so its bottom edge is at least 48px above viewport bottom (leaves room for dock FAB button) |

**Pass/Fail:** _____

---

#### ESC-8: localStorage quota exceeded / corrupt data — non-fatal fallback

| Step | Action | Expected |
|------|--------|----------|
| 1 | In browser console, simulate quota: `localStorage.setItem('hydraforge:cardPopup:state', 'too much data')` then fill localStorage to quota | Works if quota is hit; otherwise note: this test requires a localStorage-heavy page |
| 2 | Open card-1 and card-2 | Popups open normally |
| 3 | Reload page (hard reload) | If localStorage was corrupted or unavailable: popups default to empty state (no popups on reload). No crash, no console errors. **Non-fatal** — state is cleared, not throwing |
| 4 | Open card-1 again | Works normally. New state is saved to localStorage |

**Pass/Fail:** _____

> **Note:** localStorage quota testing is environment-dependent. The minimum expected behavior: corrupt JSON (set `'hydraforge:cardPopup:state'` to `'{broken'`) causes `loadState` to return `null`, resulting in clean start. Confirm no crash.

---

#### ESC-9: HMR re-mount — registerPopup idempotent (no duplicate stack entries)

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open dock and card-1 | Both visible. Stack = [dock, card-1] (2 entries) |
| 2 | Save a file that triggers HMR for ChatDock.vue (e.g., edit a comment and save) | ChatDock re-mounts. Dock should NOT appear twice in the stack |
| 3 | Inspect `usePopupZIndex().stack` in Nuxt DevTools or console | Stack still has 2 entries (dock + card-1). No duplicate `'chat-dock'` entry |
| 4 | Press Escape | Card-1 closes correctly. Press Escape again → dock closes correctly |

**Pass/Fail:** _____

---

#### ESC-10: Popup never shorter than 300px, grows with content

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open card-1 | Popup height is at least 300px |
| 2 | Using DevTools, inspect the popup's `min-height` CSS | `min-height: 300px` (or `minHeight: 300px` via style binding) |
| 3 | (Plan 20 content only — placeholder is small) Verify resizing window tall | Popup stays at min-height if content is small; grows with content otherwise |

**Pass/Fail:** _____

---

#### ESC-11: maxWidth/maxHeight clamps popup on small window

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open card-1 | Popup width is 520px (if viewport wide enough) |
| 2 | Resize browser window to narrow (e.g., 400px wide) | Popup width clamped to `calc(100vw - 1rem)` ≈ 384px |
| 3 | Resize browser window to short height (e.g., 400px tall) | Popup height clamped to `calc(100vh - 2rem)` ≈ 368px |
| 4 | Verify neither width nor height exceed viewport minus margins | No scrollbars appear from popup alone (content scrolls internally) |

**Pass/Fail:** _____

---

#### ESC-12: Click-to-front on mousedown within popup

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open card-1, card-2 (card-2 topmost) | card-2 z=52, card-1 z=51 |
| 2 | Click anywhere inside card-1's body (not header) | card-1 moves to front: z=52, card-2 drops to z=51 |
| 3 | Click card-2's close button (X) | card-2 closes. Card-1 stays at front. No flicker |
| 4 | Verify @click.stop on close button prevents `bringToFront` race | Card closes immediately; popup does not flash z-order update before disappearing |

**Pass/Fail:** _____

---

### Regressions

---

#### REG-1: ChatDock opens via FAB button as before

| Step | Action | Expected |
|------|--------|----------|
| 1 | With no popups open, click dock FAB | ChatDock opens normally, with its usual mode (draft), content, and state |
| 2 | Close dock (click X or press Escape with no cards open) | Dock closes, FAB reappears |

**Pass/Fail:** _____

---

#### REG-2: ChatDock modes (draft/session/history) unchanged

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open dock | Mode = `draft` (new chat textarea) |
| 2 | Type a message and start a session | Mode transitions to `session` with messages displayed |
| 3 | Click history icon | Mode transitions to `history` showing past session list |
| 4 | Navigate between modes | All mode transitions work exactly as before — no broken buttons or UI |

**Pass/Fail:** _____

---

#### REG-3: ChatDock dragging and full-height toggle unaffected

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open dock | Dock visible at its last position |
| 2 | Drag dock by its header | Dock moves smoothly, constrained within viewport |
| 3 | Click full-height toggle (if present) | Dock expands to full viewport height |
| 4 | Click toggle again | Dock returns to its prior size |
| 5 | Open card-1 alongside dock | Both visible. Dragging either works independently |

**Pass/Fail:** _____

---

#### REG-4: ChatDock localStorage persistence unchanged

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open dock, start a chat session, type a few messages | Messages appear |
| 2 | Hard-reload browser | Dock re-opens (if it was open) or FAB shows (if dock was closed) |
| 3 | Open dock again | Session and messages preserved as before this plan's changes |
| 4 | Verify no cross-contamination with cardPopup localStorage key | `hydraforge:cardPopup:state` and `hydraforge:chat:*` keys are separate. Deleting one does not affect the other |

**Pass/Fail:** _____

---

#### REG-5: Board page renders without errors

| Step | Action | Expected |
|------|--------|----------|
| 1 | Navigate to board view | Board loads fully: columns, cards, filter bar |
| 2 | Open browser DevTools Console tab | No JavaScript errors, no Vue warnings |
| 3 | Navigate away from board and back | Board renders correctly on re-entry |

**Pass/Fail:** _____

---

#### REG-6: No console errors on any popup/dock action

| Step | Action | Expected |
|------|--------|----------|
| 1 | Perform all Happy Path (TC-1 through TC-8) and Edge Case (ESC-1 through ESC-12) scenarios | After every action: **zero console errors, zero Vue warnings** |
| 2 | Check both Console and Network tabs | No 404s, no unhandled promise rejections, no deprecation warnings |

**Pass/Fail:** _____

---

### Cleanup

- [ ] Close all card popups and dock (via Escape or X buttons)
- [ ] Optionally clear `localStorage.removeItem('hydraforge:cardPopup:state')` to reset test state
- [ ] No permanent state changes — the board and cards are unchanged by this test