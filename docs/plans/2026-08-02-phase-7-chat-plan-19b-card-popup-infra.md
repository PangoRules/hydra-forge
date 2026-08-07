# Plan 19b: Web UI — Card Popup Infrastructure (multi-popup shell + shared z-index + Escape LIFO)
**Branch:** `task/card-popup-refactor`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 19b

**Goal:** A reusable multi-popup shell so cards can open as independent draggable popups (max 3 simultaneously) alongside the existing `ChatDock`, sharing one z-index stack and one Escape-LIFO close order. This plan ships the **shell only** — no card content, no chat features. Plan 20 adopts the shell (migrates `CardModal.vue` content into `CardPopup.vue` instances) and builds the chat features on top.

> **Why a separate infra plan.** The existing card-detail surface is a single `CardModal.vue` (one open card at a time, `UTabs` body, `z-50` modal). The new direction (decisions Q1/Q3) is multiple simultaneously-open card popups + the chat dock, all sharing z-order and Escape behavior. Building that shell and the z-index/escape plumbing in the same plan as the chat features would mix infra adoption with feature work and make both hard to review. This plan delivers the reusable pieces; Plan 20 is the first consumer.

## Decisions (from session)

- **Q1 — Escape:** LIFO. One Escape closes the topmost popup (card popup or the chat dock). Repeat for each.
- **Q3 — Max cards:** 3 simultaneously. A 4th `openCard` call is rejected with an error toast; no popup opens.
- The chat dock (`ChatDock.vue`) already exists and is draggable with `useDraggable`; this plan extracts its z-index/escape logic into a shared composable and re-points the dock at it. The dock's own behavior (modes, session lifecycle, full-height) is unchanged.

## Files

- Create: `src/web-ui/app/composables/usePopupZIndex.ts` — shared z-index stack + Escape-LIFO close-order composable (module-level reactive state, used by both `CardPopup.vue` and `ChatDock.vue`).
- Create: `src/web-ui/app/stores/cardPopup.ts` — `openCardIds[]`, `activeCardId`, per-card `position {x,y}` and `zIndex`; `openCard`/`closeCard`/`closeTopmost`/`setActive`/`bringToFront` actions; max-3 enforcement.
- Create: `src/web-ui/app/components/card/CardPopup.vue` — draggable wrapper (reuses the `useDraggable` pattern already proven in `ChatDock.vue`), renders default-slot content, z-index from the shared composable, click-to-front, Escape handled by the shared composable.
- Modify: `src/web-ui/app/components/chat/ChatDock.vue` — replace the hardcoded `z-50` class with the shared composable's z-index; register the dock with the shared escape stack so Escape-LIFO includes it.
- Modify: `src/web-ui/app/layouts/default.vue` — mount a `<CardPopupLayer />` (renders all open `CardPopup` instances from the store) alongside the existing `<ChatDock />`.
- Create: `src/web-ui/app/components/card/CardPopupLayer.vue` — iterates `cardPopup.openCardIds`, renders one `<CardPopup :cardId="…">` per open card with a slot for the card body (Plan 20 fills the slot with the migrated `CardModal` body).
- Create: `src/web-ui/app/components/card/__tests__/cardPopup.test.ts` — store: max-3 rejection, LIFO close, bringToFront reorders, position persistence.
- Create: `src/web-ui/app/composables/__tests__/usePopupZIndex.test.ts` — register/unregister/bringToFront/closeTopmost; z-index monotonic with stack order; dock + card popup share one stack.
- Modify: `src/web-ui/app/stores/__tests__/chatDock.test.ts` — assert the dock now sources z-index from the shared composable (no regression to a static `z-50`).

## Steps

- [ ] `usePopupZIndex.ts`: module-level `ref` holding an ordered array of `{ id: string, kind: 'card' | 'dock', close: () => void }` (topmost = last). API: `registerPopup(id, kind, closeFn)`, `unregisterPopup(id)`, `bringToFront(id)` (move to end, bump z), `closeTopmost()` (call topmost `closeFn`, then unregister), `zIndexFor(id)` (computed: base `50` + index). A single global `keydown` Escape listener (registered once, on first `registerPopup`, removed when the stack empties) calls `closeTopmost()`. Export a `usePopupZIndex()` that returns the reactive API. No Nuxt auto-import magic — import explicitly where used.
- [ ] `stores/cardPopup.ts`: `openCardIds: ref<string[]>`, `activeCardId: ref<string | null>`, `positions: ref<Record<string, { x: number, y: number }>>`. `openCard(cardId)`: if already open → `setActive(cardId)` + `bringToFront` (no-op duplicate); else if `openCardIds.length >= 3` → `useAppToast().error('Close a card popup first (max 3 open)')` and return (no push); else push + set active + register with `usePopupZIndex` (kind `'card'`, close = `() => closeCard(cardId)`) + seed position (cascade offset from existing popups so they don't perfectly overlap). `closeCard(cardId)`: remove from ids + positions + unregister from z-index; if it was active, set active to the new topmost. `closeTopmost()`: delegate to `usePopupZIndex.closeTopmost()` (which calls the right `closeFn`). `setActive`/`bringToFront(cardId)`: set active + `usePopupZIndex.bringToFront(cardId)`. Persist `openCardIds` + positions to `localStorage` (`hydraforge:cardPopup:state`) so a hard reload restores the open set (same pattern as `chatDock`'s `LS_ACTIVE_SESSION_KEY`).
- [ ] `CardPopup.vue`: props `cardId: string`. Uses `useDraggable` on the popup root with a drag-handle ref (same shape as `ChatDock.vue` lines 18-44 — initial position from `cardPopup.positions[cardId]`, watcher writes back to the store). Root `:style` binds `left/top/width` and `z-index: zIndexFor(popupId)`. `@mousedown` / `@focusin` → `bringToFront(popupId)`. Default slot renders the card body (Plan 20 supplies it). A close button in the header calls `closeCard(cardId)`. No Escape handler on the component itself — the shared composable owns Escape.
- [ ] `CardPopupLayer.vue`: `<div v-for="cardId in cardPopup.openCardIds" :key="cardId"><CardPopup :cardId="cardId"><slot /></CardPopup></div>` — for now the slot is a placeholder (`<div class="p-4 text-sm">Card {{ cardId }} — body arrives in Plan 20</div>`) so the shell is testable end-to-end before Plan 20 lands.
- [ ] Modify `ChatDock.vue`: import `usePopupZIndex`; on mount `registerPopup('chat-dock', 'dock', () => dock.closeDock())`, on unmount `unregisterPopup('chat-dock')`; replace `class="fixed z-50 …"` with `:style="{ …, zIndex: zIndexFor('chat-dock') }"` (keep all other styles). The dock's own Escape handling (if any) is removed — Escape is now owned by the shared composable so a card popup opened after the dock is the topmost and closes first.
- [ ] Modify `layouts/default.vue`: add `<CardPopupLayer />` next to `<ChatDock />`.
- [ ] Tests: store max-3 rejection (4th `openCard` does not push + toast called), LIFO `closeTopmost` closes the most-recently-opened card popup, `bringToFront` reorders the z stack, positions persist across a simulated reload. Composable: dock + two card popups share one stack; Escape closes topmost regardless of kind; `unregister` on close removes from the stack; z-index values are monotonic with stack order.
- [ ] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-19b-card-popup-infra-matrix.md` — open the dock + up to 3 card popups; verify a 4th open is rejected with a toast; verify Escape closes the topmost (open dock, open card A, open card B → Escape closes B, Escape closes A, Escape closes dock); verify dragging a popup brings it to front; verify a hard reload restores the open card set.

## Acceptance

- `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
- `cd src/web-ui && pnpm test` (new + existing chat-dock tests pass)

## Notes for Plan 20

- Plan 20 replaces `CardPopupLayer`'s placeholder slot with the migrated `CardModal.vue` body (details/checklist/comments/related/docs tabs + the new Chat tab).
- Plan 20 wires `openCard` into the board's card-click / `BoardCard` open path (replacing the single `setOpenCardId` + `CardModal` mount with `cardPopup.openCard(cardId)`).
- The shared `usePopupZIndex` composable is the single z-order source for all popups from here on — no new popup should hardcode `z-50`/`z-40` etc.
