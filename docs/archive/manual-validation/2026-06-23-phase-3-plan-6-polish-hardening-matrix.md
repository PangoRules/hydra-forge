# E2E Matrix — Phase 3 Plan 6: Polish & Hardening

Plan: `docs/plans/2026-06-23-phase-3-plan-6-polish-hardening.md`
Branch: `task/phase-3-polish-hardening`

Covers the deltas shipped on this branch only — keyboard system, blocked indicator, archive warning, correlationId toast polish, SSR guard. Project-list flows already covered in `docs/manual-validation/2026-06-23-phase-3-web-ui-matrix.md`.

---

## Pre-flight

```bash
docker compose up -d
cd src/web-ui && pnpm dev
dotnet run --project src/HydraForge.Server
```

Open `http://localhost:3000`, login as `testadmin` / `TestAdmin123!`, open a project board with at least one column containing cards. Browser DevTools console open.

---

## 1. Keyboard Shortcuts (Task 21)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 1.1 | Land on board with cards in ≥1 column | Page loads with no `ReferenceError: window is not defined` errors in console | ☐ |
| 1.2 | Press `j` in board area (focus on body, not in an input) | Highlight on next card in current column | ☐ |
| 1.2a | Press Tab repeatedly until focus reaches the board area, observe the board | Only the existing column/card selection ring is visible — no separate/misaligned ring appears around the whole column row | ☐ |
| 1.3 | Press `k` | Highlight moves to previous card | ☐ |
| 1.4 | Press `h` / `l` | Highlight moves between columns left/right | ☐ |
| 1.5 | Press `Enter` on highlighted card | Card detail modal opens | ☐ |
| 1.6 | Press `n` | `CardCreateModal` opens for the current column | ☐ |
| 1.7 | Press `?` | `KeyboardShortcutOverlay` modal opens, grouped by scope (Board, Column, etc.) | ☐ |
| 1.8 | Press `?` again or `Esc` | Overlay closes | ☐ |
| 1.9 | Focus a comment/text input, type `j` `k` `l` `h` | No shortcuts fire while typing | ☐ |
| 1.10 | Press `Esc` while in a text input | Input loses focus or other Escape-bound handler runs | ☐ |
| 1.11 | Reload the page; observe no duplicate keydown firing | Selection advances by exactly one per press | ☐ |
| 1.12 | SSR check — `curl -s http://localhost:3000/projects/{id}/board` | HTML returns 200, no console/server error about `window.addEventListener` | ☐ |

---

## 2. Blocked Card Indicator (Task 23)

Plan explicitly drops the "move warning modal" sub-scope (`confirmBlockedMove` stays hardcoded `false` per CLAUDE.md + plan pre-exec note line 22). Only the visual indicator is in scope.

| # | Step | Expected | Pass? |
|---|---|---|---|
| 2.1 | On a board card, no lock icon visible | (Indicator not yet implemented for this card) | ☐ |
| 2.2 | (Skip — backend `card.isBlocked` field does not exist on `CardResponse` per pre-exec note line 24; indicator mapping out of scope on this branch) | — | — |

**Result: out of scope on this branch.** No manual steps for blocked indicator; the wiring it would require is deferred until backend exposes the field.

---

## 3. Archive With Dependents Warning (Task 24)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 3.1 | Open a card that has zero relationships, press `a` or click Archive button | Plain confirm dialog (existing flow); card archives; toast appears | ☐ |
| 3.2 | Create card B; on card A add `BlockedBy` / `Precedes` / `Relates` link to B; open A | A shows linked relationship badge | ☐ |
| 3.3 | From A, press `a` (or click Archive) | `ArchiveCardWarning` modal opens instead of plain confirm — header shows alert triangle icon + "Archive Card" | ☐ |
| 3.4 | Modal body lists dependents with relationship-type badge + card title | All linked cards shown with correct type badge | ☐ |
| 3.5 | Click "Cancel" | Modal closes, card not archived, no toast | ☐ |
| 3.6 | Re-open A's archive flow; click "Archive" | Modal closes, card archives, archive toast fires | ☐ |
| 3.7 | After archive, navigate to B | B unchanged, relationships still listed (archiving does not delete links per copy) | ☐ |

---

## 4. Correlation ID Copy Polish (Task 22)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 4.1 | Trigger any error toast (e.g. force a failed API call by stopping server, then click a button that calls API) | Error toast appears with "Copy Correlation ID" action button | ☐ |
| 4.2 | Click "Copy Correlation ID" with clipboard permission allowed | Success toast "Correlation ID copied" appears briefly; clipboard contains the ID | ☐ |
| 4.3 | Reload, deny clipboard permission, retrigger error, click "Copy Correlation ID" | Error toast appears: "Failed to copy — ID: {first 8 chars}."; clipboard-write exception handled, no unhandled rejection in console | ☐ |
| 4.4 | Normal API success path | No correlationId toast shown | ☐ |

---

## 5. SSR / Window Guard Regression (Commit bf9e029)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 5.1 | `pnpm typecheck` | Zero errors | ☐ |
| 5.2 | `pnpm lint` | Zero errors | ☐ |
| 5.3 | `pnpm test` | 148/148 passing across 34 files | ☐ |
| 5.4 | `pnpm build` | Build succeeds | ☐ |
| 5.5 | `curl -sI http://localhost:3000/projects/{id}/board` | 200 OK; server logs show no `window is not defined` from `useKeyboard` | ☐ |
| 5.6 | Load board in browser; check DevTools network tab for hydration warnings | No SSR/CSR mismatch warnings referencing keyboard shortcuts | ☐ |

---

## 6. ARIA & Focus (Task 25)

| # | Step | Expected | Pass? |
|---|---|---|---|
| 6.1 | Tab through board page from URL bar to first card | Focus ring visible on each interactive control | ☐ |
| 6.2 | Open card modal via click or `Enter` on highlighted card | Focus moves into the modal (not back to body) | ☐ |
| 6.3 | Tab through modal fields | Tab cycles through interactive elements within modal; `Esc` closes | ☐ |
| 6.4 | `KeyboardShortcutOverlay` open — check role/label | Modal announced by screen reader as a dialog with keyboard shortcuts content | ☐ |

---

## Cleanup

- [ ] Restore any test cards/relationships created in 3.2–3.7
- [ ] Verify clipboard permission state unchanged from baseline
