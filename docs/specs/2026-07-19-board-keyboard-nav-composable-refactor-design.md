# Board Keyboard Navigation — Composable Refactor + Focus/Highlight Bug Fix

**Branch:** `task/phase-3-polish-hardening`

> **Date:** 2026-07-19
> **Status:** Approved
> **Phase:** 3 — Task 6 Polish & Hardening (follow-up to keyboard-nav commits `5f4835f`..`09f39f1`)

---

## 1. Problem

Keyboard navigation (hjkl, roving tabindex, card move/reorder, archive shortcut) was added to the board across five commits. Two problems surfaced:

1. **Visual bug**: after Tab-ing into the board, the column highlight looks misplaced relative to the columns. Root cause (confirmed by reading the code, not guessed): `BoardView.vue` puts `tabindex="0"` and its own `focus:ring-2 focus:ring-primary/30 focus:ring-inset` on the *entire column row* (`BoardView.vue:52-54`). This is a second, independent highlight mechanism sitting next to the real one — the `selectedColumnIndex`/`selectedCardIndex` state (owned in `board.vue:79-80`) that drives the `ring-2 ring-primary/40` on the selected `BoardColumn` (`BoardColumn.vue:100`) and `ring-2 ring-primary` on the selected `BoardCard` (`BoardCard.vue:141`). Nothing ties native DOM focus to the selection state — Tab paints a ring shaped like the whole row, which reads as misaligned next to the existing single-column ring.
2. **File growth / duplication risk**: keyboard-nav logic is ~270 of `board.vue`'s 615 lines, inline in a single `onMounted`. Selection-state sync is hand-written twice (`handleCardClick`, `handleColumnClick` in `board.vue:105-127`) — the same shape of duplication D-43 already flagged and fixed for filter state (`useBoardFilters.ts`). No composable exists yet for keyboard/focus, so the next section to get keyboard nav (Projects page, later chat) has nothing to plug into and would likely re-invent it inline again.

## 2. Goals

- Fix the Tab/hjkl visual desync.
- Extract board keyboard-nav state and handlers into composables under a dedicated folder, following the D-43 pattern (composable owns state directly, no local-ref + watcher duplication).
- Make the generic piece (single-axis roving selection) reusable as-is for a future flat-list nav (Projects page, chat), without guessing at that page's actual requirements (YAGNI — only the primitive is generalized, not a full Projects-page implementation).
- Hardening: replace the `if (isModalOpen()) return` guard duplicated in all 9 Board shortcut handlers with a single `enabled` predicate per registration in `useKeyboard.ts`.

## 3. Non-goals

- Re-architecting to true per-item ARIA roving-tabindex (tabindex moving between individual cards/columns as selection changes). Current pattern — single `tabindex="0"` tab-stop on the board container, internal hjkl nav, all descendants forced `tabindex="-1"` — stays as-is. That's a bigger a11y rework and belongs to the still-open Task 25 ARIA pass (`docs/plans/2026-06-23-phase-3-plan-6-polish-hardening.md`), not this fix.
- Building any Projects-page keyboard nav now. Only the generic primitive is added; the page-specific composable is deferred until that work actually starts (spec noted as "prone to change").
- Changing `CardModal.vue`'s own `keyboard.register('Card', 'a', ...)` — small, out of scope.

## 4. Design

### 4.1 Bug fix (`BoardView.vue`)

Remove the container's own visual focus ring — the persistent column/card selection ring already communicates where hjkl will act; a second ring adds a conflicting signal, not information. Keep `tabindex="0"` for reachability and add `aria-label="Board columns — use h j k l to navigate, ? for shortcuts"` for screen readers. Add an `@focus` handler as a hook for the composable to (re)validate selection state on focus-in (defensive; state is already clamped elsewhere, but gives a single place to extend later).

### 4.2 Composable folder

```
app/composables/keyboard/
  useKeyboard.ts          (moved, same public API + one addition)
  useRovingFocus.ts        (new — generic 1D primitive)
  useBoardKeyboardNav.ts   (new — board-specific 2D composable)
```

Nuxt auto-imports composables recursively, so no config change is needed for the subfolder.

### 4.3 `useKeyboard.ts` — dispatch hardening

Same public API (`register`/`unregister`/`getShortcuts`/`getAllShortcuts`). `register()` gains an optional `enabled?: () => boolean` predicate (default: always enabled). `handleKeyDown` filters out shortcuts whose `enabled()` returns false before the last-registered-wins match by key. This replaces the `if (isModalOpen()) return` line duplicated in each of the 9 Board handlers with one predicate passed at registration time: `enabled: () => !anyModalOpen.value`. No separate scope-stack registry — the predicate is the "active scope" mechanism, kept minimal since only two scopes (`Board`, `Card`) exist today.

### 4.4 `useRovingFocus.ts` — generic single-axis primitive

Given a reactive getter for a list of ids, returns:
- `selectedIndex: Ref<number>`
- `selectedId: ComputedRef<string | null>`
- `next()`, `prev()` — clamped or wraparound index movement (matches existing board behavior: wraparound, per `board.vue:209/221` for cards, `229/238` for columns)
- `select(index: number)`, `selectById(id: string)`
- `isSelected(index: number): boolean`

No DOM/tabindex/ring opinion baked in — purely index math + selection state. This is the piece a future flat-list nav (Projects page, chat message list) plugs into directly without modification.

### 4.5 `useBoardKeyboardNav.ts` — board-specific 2D composable

Composes two `useRovingFocus` instances: one over `board.visibleColumns` (columns axis), one over the cards of whichever column is currently selected (cards axis, re-keyed whenever the column selection changes). Owns:

- All `keyboard.register('Board', ...)` calls currently inline in `board.vue`'s `onMounted` (hjkl, `?`, `n`, `Enter`, `a`, Ctrl+Shift+Arrow move/reorder) — moved verbatim in behavior, guarded via the new `enabled` predicate instead of inline `isModalOpen()` checks.
- `moveSelectedCard`/`reorderSelectedCard` — calls `useCardMove(projectId)` itself rather than requiring the caller to wire it through.
- `syncToCard(card)` / `syncToColumn(columnId)` — the logic currently in `board.vue`'s `handleCardClick`/`handleColumnClick` (walk `board.visibleColumns`/`cardsByColumn` to find index, write back into selection state). Single owner of selection state now, instead of two independent write paths.
- Registration lifecycle: calls `onMounted`/`onBeforeUnmount` internally (same pattern already used by `useRealtime.connect/disconnect`), so `board.vue` doesn't manage `keyboard.unregister('Board')` itself.

Signature:
```ts
useBoardKeyboardNav(options: {
  projectId: string
  anyModalOpen: Ref<boolean> | ComputedRef<boolean>
  projectArchived: Ref<boolean>
  onOpenCard: (card: CardResponse) => void
  onCreateCard: (columnId?: string) => void
  onArchiveCard: (card: CardResponse) => void
}): {
  selectedColumnIndex: Ref<number>
  selectedCardIndex: Ref<number>
  selectedCardId: ComputedRef<string | null>
  syncToCard: (card: CardResponse) => void
  syncToColumn: (columnId: string) => void
}
```

## 5. Call-site migration

- **`board.vue`**: delete `selectedColumnIndex`/`selectedCardIndex` refs, `selectedCardIdForKeyboard` computed, the `handleCardClick` sync loop, `handleColumnClick`, and the ~180-line `keyboard.register('Board', ...)` block in `onMounted`. Replace with one `const nav = useBoardKeyboardNav({ projectId, anyModalOpen, projectArchived, onOpenCard: openCardModal, onCreateCard: handleAddCard, onArchiveCard: requestArchive })` call. Template passes `nav.selectedColumnIndex` / `nav.selectedCardId` into `BoardView` exactly as today. Existing card-click/column-click handlers become thin wrappers calling `nav.syncToCard(card)` / `nav.syncToColumn(columnId)` (plus whatever non-nav side effects they already had, e.g. opening the card modal).
- **`BoardView.vue`**, **`BoardColumn.vue`**, **`BoardCard.vue`**, **`ColumnHeader.vue`**: no prop-shape changes — `selected`/`selectedCardId` keep flowing exactly as today. Only `BoardView.vue`'s template changes (§4.1).
- **`CardModal.vue`**: unchanged.

## 6. Testing

- `useRovingFocus.test.ts` — pure index-logic unit tests: next/prev wraparound at both ends, `select`/`selectById`, `isSelected`.
- `useBoardKeyboardNav.test.ts` — composable test with a mocked board store: hjkl moves indices correctly (including column-switch resetting card index to 0, matching current `board.vue:230/239` behavior), `Enter` opens the right card via `onOpenCard`, `a` calls `onArchiveCard` with the right card, Ctrl+Shift+Arrow calls move/reorder with correct target index, and the `enabled` predicate blocks all handlers when `anyModalOpen` is true (replacing today's manual per-handler checks).
- Existing `board.vue`/`BoardView.vue` component tests get slimmer — no longer need to poke at inline keyboard logic that no longer lives there.
- Manual: add a new row to `docs/manual-validation/2026-06-23-phase-3-plan-6-polish-hardening-matrix.md` §1 (Keyboard Shortcuts) — "Tab into board, then press hjkl — no stray/misaligned focus ring, only the column/card selection ring shows."

## 7. Documentation updates

- New `D-47` in `docs/DECISIONS.md`: the roving-focus composable split (generic primitive + board-specific wrapper) and the two-independent-rings root cause as rationale for why focus and selection state must share one owner going forward.
- `docs/plans/2026-06-23-phase-3-plan-6-polish-hardening.md` Task 21 pre-execution note updated to reflect the composable refactor (superseding the original inline-in-`board.vue` sketch in that task's Step 3).
- `CLAUDE.md` Nuxt UI v4 patterns section: short addition pointing at `useRovingFocus`/`useBoardKeyboardNav` (`app/composables/keyboard/`) as the established pattern for any future keyboard-nav surface, alongside the existing D-43 `useBoardFilters` mention.
