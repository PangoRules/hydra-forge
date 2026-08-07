# Plan 19b: Web UI — Card Popup Infrastructure (multi-popup shell + shared z-index + Escape LIFO)
**Branch:** `task/card-popup-refactor`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 19b
**Test scope:** unit

**Goal:** A reusable multi-popup shell so cards can open as independent draggable popups (max 3 simultaneously) alongside the existing `ChatDock`, sharing one z-index stack and one Escape-LIFO close order. This plan ships the **shell only** — no card content, no chat features. Plan 20 adopts the shell (migrates `CardModal.vue` content into `CardPopup.vue` instances) and builds the chat features on top.

> **Why a separate infra plan.** The existing card-detail surface is a single `CardModal.vue` (one open card at a time, `UTabs` body, `z-50` modal). The new direction (decisions Q1/Q3) is multiple simultaneously-open card popups + the chat dock, all sharing z-order and Escape behavior. Building that shell and the z-index/escape plumbing in the same plan as the chat features would mix infra adoption with feature work and make both hard to review. This plan delivers the reusable pieces; Plan 20 is the first consumer.

## Decisions (from session)

- **Q1 — Escape:** LIFO. One Escape closes the topmost popup (card popup or the chat dock). Repeat for each.
- **Q3 — Max cards:** 3 simultaneously. A 4th `openCard` call is rejected with an error toast; no popup opens.
- The chat dock (`ChatDock.vue`) already exists and is draggable with `useDraggable`; this plan extracts its z-index/escape logic into a shared composable and re-points the dock at it. The dock's own behavior (modes, session lifecycle, full-height) is unchanged.

## Files

- **Create:** `src/web-ui/app/composables/usePopupZIndex.ts` — shared z-index stack + Escape-LIFO close-order composable (module-level reactive state, used by both `CardPopup.vue` and `ChatDock.vue`).
- **Create:** `src/web-ui/app/stores/cardPopup.ts` — `openCardIds[]`, `activeCardId`, per-card `position {x,y}` and `zIndex`; `openCard`/`closeCard`/`closeTopmost`/`setActive`/`bringToFront` actions; max-3 enforcement.
- **Create:** `src/web-ui/app/components/card/CardPopup.vue` — draggable wrapper (reuses the `useDraggable` pattern already proven in `ChatDock.vue`), renders default-slot content, z-index from the shared composable, click-to-front, Escape handled by the shared composable.
- **Modify:** `src/web-ui/app/components/chat/ChatDock.vue` — replace the hardcoded `z-50` class with the shared composable's z-index; register the dock with the shared escape stack so Escape-LIFO includes it.
- **Modify:** `src/web-ui/app/layouts/default.vue` — mount a `<CardPopupLayer />` (renders all open `CardPopup` instances from the store) alongside the existing `<ChatDock />`.
- **Create:** `src/web-ui/app/components/card/CardPopupLayer.vue` — iterates `cardPopup.openCardIds`, renders one `<CardPopup :cardId="…">` per open card with a slot for the card body (Plan 20 fills the slot with the migrated `CardModal` body).
- **Create:** `src/web-ui/app/components/card/__tests__/cardPopup.test.ts` — store: max-3 rejection, LIFO close, bringToFront reorders, position persistence.
- **Create:** `src/web-ui/app/composables/__tests__/usePopupZIndex.test.ts` — register/unregister/bringToFront/closeTopmost; z-index monotonic with stack order; dock + card popup share one stack.
- **Modify:** `src/web-ui/app/stores/__tests__/chatDock.test.ts` — assert the dock now sources z-index from the shared composable (no regression to a static `z-50`).

## Steps

### Step 1: `usePopupZIndex.ts` — shared z-index stack + global Escape LIFO

**Pattern:** Module-level reactive state (not per-component). Single global `keydown` listener on `document` — registered once on first `registerPopup`, removed when stack empties.

```typescript
// src/web-ui/app/composables/usePopupZIndex.ts
import { ref, computed, watch } from 'vue'

interface PopupEntry {
  id: string
  kind: 'card' | 'dock'
  close: () => void
}

const stack = ref<PopupEntry[]>([])
const baseZ = 50

// Module-level state — one owned listener, registered once, removed when stack empties.
let listenerRegistered = false

function ensureListener() {
  if (listenerRegistered) return
  if (typeof document === 'undefined') return
  listenerRegistered = true
  document.addEventListener('keydown', onGlobalKeydown)
}

function removeListener() {
  if (!listenerRegistered) return
  listenerRegistered = false
  if (typeof document === 'undefined') return
  document.removeEventListener('keydown', onGlobalKeydown)
}

function onGlobalKeydown(e: KeyboardEvent) {
  if (e.key !== 'Escape') return
  // Don't close popups when a modal/dialog/popover is focused (select, modal, etc.)
  const activeEl = document.activeElement
  if (activeEl && activeEl instanceof HTMLElement) {
    const role = activeEl.closest('[role="dialog"], [role="listbox"], [role="menu"]')
    if (role) return
  }
  closeTopmost()
}

// Watch stack depth — add listener when first entry appears, remove when last one leaves.
watch(
  () => stack.value.length,
  (len) => {
    if (len > 0) ensureListener()
    else removeListener()
  }
)

export function usePopupZIndex() {
  function registerPopup(id: string, kind: 'card' | 'dock', closeFn: () => void) {
    const existing = stack.value.findIndex(e => e.id === id)
    if (existing !== -1) {
      // Re-register (e.g. dock re-mount after hot-reload): update closeFn, keep position.
      stack.value[existing]!.close = closeFn
      return
    }
    stack.value.push({ id, kind, close: closeFn })
  }

  function unregisterPopup(id: string) {
    stack.value = stack.value.filter(e => e.id !== id)
  }

  function bringToFront(id: string) {
    const idx = stack.value.findIndex(e => e.id === id)
    if (idx === -1) return
    const [entry] = stack.value.splice(idx, 1)
    stack.value.push(entry!)
  }

  function closeTopmost() {
    const top = stack.value[stack.value.length - 1]
    if (top) {
      top.close() // calls the closeFn — store's closeCard('card-X') or dock.closeDock()
      // closeFn is responsible for calling unregisterPopup via its own cleanup path.
      // If closeFn doesn't unregister (shouldn't happen), remove anyway as belt-and-suspenders.
      setTimeout(() => {
        if (stack.value.length && stack.value[stack.value.length - 1]?.id === top.id) {
          unregisterPopup(top.id)
        }
      }, 0)
    }
  }

  const zIndexFor = (id: string): number => {
    const idx = stack.value.findIndex(e => e.id === id)
    if (idx === -1) return baseZ
    return baseZ + idx
  }

  return { stack, registerPopup, unregisterPopup, bringToFront, closeTopmost, zIndexFor }
}
```

**Key points:**
- `popupId` convention: card popups use `cardId` (the card UUID string); dock uses the literal string `'chat-dock'`.
- `closeTopmost()` calls the stored `closeFn` — for cards, that's `() => cardPopupStore.closeCard(cardId)`; for the dock, `() => chatDockStore.closeDock()`. Each closeFn should call `unregisterPopup` from its own cleanup. The composable adds a `setTimeout(0)` belt-and-suspenders removal in case the closeFn forgets.
- `zIndexFor` returns `50 + index` — bottom of stack = 50, topmost = highest. The existing dock uses `z-50`, so this is backward-compatible: an unshared dock starts at 50. When card popups join, they get 51, 52, 53 under the dock.
- Gateway check for open dialogs/listboxes/menus prevents Escape from closing popups when the user is interacting with a focused select/modal/dropdown. This is critical: without it, pressing Escape to close a `USelectMenu` inside a card popup would close the whole popup instead.
- `registerPopup` is idempotent — if the same `id` is already registered (e.g. dock re-mounts after HMR), it updates the `closeFn` without creating a duplicate entry. This is important for Vue HMR during development.
- `unregisterPopup` is always safe — it's a no-op if the id isn't in the stack.

### Step 2: `stores/cardPopup.ts` — Pinia store for multi-popup state

**Reference:** existing `stores/chatDock.ts` (lines 1–156) for Pinia store pattern — `defineStore`, `ref()` for state, `computed` for derived state, localStorage watch pattern.

```typescript
// src/web-ui/app/stores/cardPopup.ts
import { defineStore } from 'pinia'

const LS_KEY = 'hydraforge:cardPopup:state'

interface StoredState {
  openCardIds: string[]
  positions: Record<string, { x: number; y: number }>
}

function loadState(): StoredState | null {
  if (typeof localStorage === 'undefined') return null
  try {
    const raw = localStorage.getItem(LS_KEY)
    return raw ? JSON.parse(raw) : null
  } catch { return null }
}

function saveState(ids: string[], pos: Record<string, { x: number; y: number }>) {
  if (typeof localStorage === 'undefined') return
  try {
    localStorage.setItem(LS_KEY, JSON.stringify({ openCardIds: ids, positions: pos }))
  } catch { /* quota exceeded — non-fatal */ }
}

export const useCardPopupStore = defineStore('cardPopup', () => {
  const saved = loadState()

  const openCardIds = ref<string[]>(saved?.openCardIds ?? [])
  const activeCardId = ref<string | null>(null)
  const positions = ref<Record<string, { x: number; y: number }>>(saved?.positions ?? {})

  const toast = useAppToast()
  const popupZ = usePopupZIndex()

  // Max cards
  const canOpen = computed(() => openCardIds.value.length < 3)

  // Cascade offset for new popups — each new card opens 24px down-right from the last one.
  // Resets to a default anchor when the stack is empty.
  const DEFAULT_ANCHOR = { x: 48, y: 48 }
  const CASCADE_OFFSET = 24

  function nextPosition(): { x: number; y: number } {
    if (openCardIds.value.length === 0) return { ...DEFAULT_ANCHOR }
    const lastId = openCardIds.value[openCardIds.value.length - 1]!
    const lastPos = positions.value[lastId] ?? DEFAULT_ANCHOR
    return { x: lastPos.x + CASCADE_OFFSET, y: lastPos.y + CASCADE_OFFSET }
  }

  function openCard(cardId: string) {
    // Already open — activate and bring to front (no-op duplicate)
    if (openCardIds.value.includes(cardId)) {
      setActive(cardId)
      bringToFront(cardId)
      return
    }

    // Max 3 enforcement
    if (openCardIds.value.length >= 3) {
      toast.error('Close a card popup first (max 3 open)')
      return
    }

    openCardIds.value = [...openCardIds.value, cardId]
    positions.value[cardId] = nextPosition()
    activeCardId.value = cardId

    popupZ.registerPopup(cardId, 'card', () => closeCard(cardId))
    popupZ.bringToFront(cardId)
  }

  function closeCard(cardId: string) {
    openCardIds.value = openCardIds.value.filter(id => id !== cardId)
    delete positions.value[cardId]
    popupZ.unregisterPopup(cardId)

    // If the closed card was the active one, set active to the new topmost.
    if (activeCardId.value === cardId) {
      activeCardId.value = openCardIds.value[openCardIds.value.length - 1] ?? null
    }
  }

  function closeTopmost() {
    popupZ.closeTopmost() // delegates to the composable, which calls the right closeFn
  }

  function setActive(cardId: string) {
    activeCardId.value = cardId
  }

  function bringToFront(cardId: string) {
    if (!openCardIds.value.includes(cardId)) return
    openCardIds.value = openCardIds.value.filter(id => id !== cardId)
    openCardIds.value = [...openCardIds.value, cardId]
    popupZ.bringToFront(cardId)
  }

  // Persist openCardIds + positions to localStorage on every change (same pattern as
  // chatDock's LS_ACTIVE_SESSION_KEY watcher — Pinia survives client-side navigation
  // but a hard reload resets it, localStorage bridges the gap).
  watch([openCardIds, positions], () => {
    saveState(openCardIds.value, positions.value)
  }, { deep: true })

  return {
    openCardIds, activeCardId, positions, canOpen,
    openCard, closeCard, closeTopmost, setActive, bringToFront
  }
})
```

**Key points:**
- `openCard` is the entry point Plan 20 calls from the board — `cardPopup.openCard(cardId)`.
- `closeCard` removes from `openCardIds`, deletes the position entry, unregisters from z-index, and re-assigns `activeCardId` to the new topmost card (or null).
- `closeTopmost` delegates entirely to `usePopupZIndex.closeTopmost()`. The card popup store doesn't know or care whether the topmost is a card or the dock — the composable's stored `closeFn` handles that.
- `bringToFront` reorders both the Pinia `openCardIds` array (last = topmost) AND the composable's z-stack (moves the entry to the end).
- localStorage persistence matches the existing `chatDock` pattern: a `watch` on the state refs writes on every change. On store creation, state is restored from localStorage. Non-fatal if localStorage is unavailable or quota-exceeded.
- `canOpen` is a computed boolean — `openCardIds.length < 3`. Plan 20 components can use this to disable the "open card" UI when at max capacity.

### Step 3: `CardPopup.vue` — draggable popup shell

**Reference:** existing `ChatDock.vue` (lines 1–252) for the `useDraggable` pattern + drag-handle ref + watcher-writing-position-back-to-store + full-height-override pattern.

```vue
<!-- src/web-ui/app/components/card/CardPopup.vue -->
<script setup lang="ts">
import { useDraggable } from '@vueuse/core'

const props = defineProps<{ cardId: string }>()

const cardPopup = useCardPopupStore()
const popupZ = usePopupZIndex()

const dragHandle = ref<HTMLElement | null>(null)
const popupRef = ref<HTMLElement | null>(null)

const POPUP_WIDTH_PX = 520
const POPUP_MIN_HEIGHT_PX = 300

// Initial position from the store (set by openCard's nextPosition() cascade).
// Falls back to a sensible default if the position isn't set (shouldn't happen).
const initialPos = cardPopup.positions[props.cardId] ?? { x: 48, y: 48 }

const { x, y } = useDraggable(popupRef, {
  handle: dragHandle,
  initialValue: () => ({ ...initialPos }),
  preventDefault: true,
  // Constrain within viewport — don't let the user drag it fully off-screen.
  onMove(position) {
    const el = popupRef.value
    if (!el) return position
    const rect = el.getBoundingClientRect()
    const maxX = Math.max(0, window.innerWidth - rect.width)
    const maxY = Math.max(0, window.innerHeight - 48) // leave 48px for bottom bar / dock FAB
    return {
      x: Math.max(0, Math.min(position.x, maxX)),
      y: Math.max(0, Math.min(position.y, maxY))
    }
  }
})

// Write drag position back to the store so Plan 20's re-mounts restore the exact spot.
watch([x, y], ([nx, ny]) => {
  if (cardPopup.positions[props.cardId]) {
    cardPopup.positions[props.cardId] = { x: nx, y: ny }
  }
})

// Bring this popup to front on mousedown/focusin anywhere inside it — ensures
// the user clicking a card popup to interact with it also raises its z-index.
function handlePointerDown() {
  cardPopup.setActive(props.cardId)
  cardPopup.bringToFront(props.cardId)
}

// Track min-height via CSS custom property so the popup is never shorter than
// 300px, but can grow with content.
const minHeightStyle = computed(() => `${POPUP_MIN_HEIGHT_PX}px`)
</script>

<template>
  <ClientOnly>
    <div
      :ref="popupRef"
      class="fixed bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg shadow-2xl flex flex-col overflow-hidden"
      :style="{
        width: `${POPUP_WIDTH_PX}px`,
        minHeight: minHeightStyle,
        maxHeight: 'calc(100vh - 2rem)',
        maxWidth: 'calc(100vw - 1rem)',
        left: `${x}px`,
        top: `${y}px`,
        zIndex: popupZ.zIndexFor(cardId)
      }"
      @mousedown="handlePointerDown"
      @focusin="handlePointerDown"
    >
      <!-- Header — drag handle + close button -->
      <div
        :ref="dragHandle"
        class="shrink-0 flex items-center justify-between px-4 py-2 border-b border-gray-200 dark:border-gray-700 cursor-move select-none"
      >
        <!-- Title placeholder — Plan 20 replaces this with the card title -->
        <span class="text-sm font-medium truncate">
          Card {{ cardId.slice(0, 8) }}…
        </span>
        <UButton
          icon="i-lucide-x"
          variant="ghost"
          size="xs"
          title="Close"
          aria-label="Close card popup"
          @click.stop="cardPopup.closeCard(cardId)"
        />
      </div>

      <!-- Body — default slot. Plan 20 fills this with the migrated CardModal body. -->
      <div class="flex-1 min-h-0 overflow-y-auto p-4">
        <slot>
          <!-- Placeholder until Plan 20 slots in real content -->
          <p class="text-sm text-gray-500">
            Card {{ cardId.slice(0, 8) }}… — body arrives in Plan 20
          </p>
        </slot>
      </div>
    </div>
  </ClientOnly>
</template>
```

**Key points:**
- `useDraggable` from `@vueuse/core` — same import as `ChatDock.vue` line 2. The drag-handle ref is the header bar (`#dragHandle`), same as `ChatDock.vue` line 21–38.
- Viewport constraints via `onMove` — prevents dragging the popup so far off-screen the user can't recover it. The 48px bottom margin leaves room for the dock's FAB button (`bottom-4` = 16px + 24px button size ≈ 48px). `maxWidth` and `maxHeight` via CSS `max-*` prevent overflow on window resize — the CSS clamp is a second line of defense in case the drag constraint drifts.
- `@mousedown` and `@focusin` both call `bringToFront` — clicking anywhere inside the popup raises it. `@focusin` covers keyboard-tab focus entering the popup for accessibility.
- The close button uses `@click.stop` — prevents the `mousedown` on the popup root from firing `bringToFront` before the popup closes (the popup would briefly flash to front then disappear).
- No Escape handler on the component — Escape is owned by the global listener in `usePopupZIndex`.
- `ClientOnly` wrapper — prevents SSR hydration mismatches. The popup uses `window` APIs (viewport constraint, `useDraggable` position calculation). Same pattern as `ChatDock.vue` lines 102/251.

### Step 4: `CardPopupLayer.vue` — renders all open card popups

```vue
<!-- src/web-ui/app/components/card/CardPopupLayer.vue -->
<script setup lang="ts">
const cardPopup = useCardPopupStore()
</script>

<template>
  <ClientOnly>
    <CardPopup
      v-for="cardId in cardPopup.openCardIds"
      :key="cardId"
      :card-id="cardId"
    >
      <!-- Slot intentionally empty — Plan 20 fills this with the migrated CardModal body -->
    </CardPopup>
  </ClientOnly>
</template>
```

**Key points:**
- Iterates `cardPopup.openCardIds` (a reactive ref array from the Pinia store). Each open card gets one `<CardPopup>` instance.
- `:key="cardId"` ensures Vue reuses the DOM node for the same card across re-renders (critical for keeping the drag position and focus state when unrelated popups open/close).
- The default slot is empty — in Plan 20, each `<CardPopup>` gets slotted content with the card's detail panels (description, checklist, comments, attachments, dependencies, metadata, specs/plans, and the new Chat tab).
- `ClientOnly` — SSR-safe. All popups use browser-only APIs.

### Step 5: Modify `ChatDock.vue` — integrate with shared z-index stack

**File:** `src/web-ui/app/components/chat/ChatDock.vue`

Changes:
1. **Add import** at line 2 (after `@vueuse/core` import):
   ```typescript
   import { usePopupZIndex } from '~/composables/usePopupZIndex'
   ```

2. **Add popupZ composable** in the script setup (after `const dock = useChatDockStore()`):
   ```typescript
   const popupZ = usePopupZIndex()
   ```

3. **Register/unregister on mount/unmount** (replaces the existing Escape listener at lines 65–77):
   ```typescript
   // REMOVE lines 65–77 entirely:
   //   function onKeydown(e: KeyboardEvent) { ... }
   //   const isClient = import.meta.client
   //   onMounted(() => { if (isClient) window.addEventListener('keydown', onKeydown) })
   //   onUnmounted(() => { if (isClient) window.removeEventListener('keydown', onKeydown) })
   //
   // REPLACE with:
   onMounted(() => {
     popupZ.registerPopup('chat-dock', 'dock', () => dock.closeDock())
   })
   onUnmounted(() => {
     popupZ.unregisterPopup('chat-dock')
   })
   ```

4. **Replace the hardcoded `z-50` class** on the popup `<div>` (currently line 119: `class="fixed z-50 ..."`):
   - Remove `z-50` from the class string.
   - Add `zIndex: popupZ.zIndexFor('chat-dock')` to the `:style` binding:
   ```html
   <!-- Before (line 119): -->
   class="fixed z-50 max-w-[calc(100vw-1rem)] max-h-[calc(100vh-1rem)] bg-white ..."
   :style="{
     width: `${DOCK_WIDTH_PX}px`,
     left: `${x}px`,
     top: dock.isFullHeight ? '1rem' : `${y}px`
   }"
   
   <!-- After: -->
   class="fixed max-w-[calc(100vw-1rem)] max-h-[calc(100vh-1rem)] bg-white ..."
   :style="{
     width: `${DOCK_WIDTH_PX}px`,
     left: `${x}px`,
     top: dock.isFullHeight ? '1rem' : `${y}px`,
     zIndex: popupZ.zIndexFor('chat-dock')
   }"
   ```

5. **Keep everything else unchanged.** The dock's own mode management, session lifecycle, dragging, full-height toggle, localStorage persistence — all stay as-is. The only behavioral change is Escape now goes through the shared stack (so a card popup opened after the dock is the topmost and closes first on Escape).

### Step 6: Modify `layouts/default.vue` — mount CardPopupLayer

**File:** `src/web-ui/app/layouts/default.vue`

The `<ChatDock />` is at line 81, outside the `UDashboardGroup` but inside `UApp`. Place `CardPopupLayer` next to it:

```html
<!-- Before (lines 81): -->
    <ChatDock />
  </UApp>

<!-- After: -->
    <CardPopupLayer />
    <ChatDock />
  </UApp>
```

Also add the import at line 5:
```typescript
import CardPopupLayer from '~/components/card/CardPopupLayer.vue'
```

**Order matters:** `CardPopupLayer` before `ChatDock` in the template so card popups render *behind* the dock in DOM order (z-index handles stacking, but DOM order is the fallback for same-z-index edge cases). Both are `position: fixed` so their DOM order in the layout is purely for z-index fallback, not visual flow.

### Step 7: Tests

#### 7a: `usePopupZIndex` composable tests
**File:** `src/web-ui/app/composables/__tests__/usePopupZIndex.test.ts`

Test patterns follow existing composable tests (`useSidebarCollapse.test.ts` — lines 1–27: import composable directly, test ref values, no mocks needed).

Assertions:
1. **stack starts empty** — `stack.value.length === 0`
2. **register adds entry** — register a card popup, stack length = 1, entry has correct id/kind/close
3. **register is idempotent** — register same id twice, stack length still = 1, second call updates closeFn
4. **unregister removes entry** — register two entries, unregister first, stack length = 1, remaining entry is second
5. **bringToFront moves to end** — register three entries A/B/C, bringToFront A, stack order = [B, C, A]
6. **closeTopmost calls the topmost closeFn** — register two entries with mock closeFns, closeTopmost() → only the topmost closeFn was called
7. **zIndexFor returns baseZ + index** — register three entries, zIndexFor first = 50, second = 51, third = 52
8. **zIndexFor returns baseZ for unknown id** — `zIndexFor('nonexistent') === 50`
9. **dock + card popups share one stack** — register dock + two card popups, stack has 3 entries, closeTopmost closes the most-recently-registered regardless of kind
10. **listener lifecycle** — manual `document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))` → closeTopmost called. Close all entries → stack empty → second Escape dispatch does not call any closeFn (listener removed).

**Note:** The global `document` keydown listener uses `watch()` on `stack.value.length`. In `vitest` with `happy-dom`, `document.addEventListener` works. The test creates entries, dispatches `keydown` events, and asserts the mock closeFns were called. No Nuxt mocks needed — the composable is pure JavaScript with Vue reactivity.

#### 7b: `cardPopup` store tests
**File:** `src/web-ui/app/components/card/__tests__/cardPopup.test.ts`

Test patterns follow existing store tests (`chatDock.test.ts` — lines 1–168: `mockNuxtImport` for `useApi`/`useAppToast`/`useRoute`, `setActivePinia(createPinia())` in `beforeEach`).

Mocks needed:
```typescript
const mockToastError = vi.fn()
mockNuxtImport('useAppToast', () => () => ({ error: mockToastError, success: vi.fn() }))
```

Assertions:
1. **starts with empty state** — `openCardIds.length === 0`, `activeCardId === null`, `canOpen === true`
2. **openCard adds to openCardIds** — open 'a', openCardIds = ['a'], activeCardId = 'a'
3. **openCard same card is no-op** — open 'a' twice, openCardIds = ['a'] (no duplicate), activeCardId stays 'a', toast NOT called
4. **max-3 enforcement** — open 'a', 'b', 'c' → success. open 'd' → `mockToastError` called with 'Close a card popup first (max 3 open)', openCardIds still ['a', 'b', 'c']
5. **canOpen reflects capacity** — open 3 cards → `canOpen === false`
6. **closeCard removes and re-assigns active** — open a/b/c, closeCard('c'), openCardIds = ['a', 'b'], activeCardId = 'b' (new topmost)
7. **closeCard of non-active leaves active unchanged** — open a/b, closeCard('a'), openCardIds = ['b'], activeCardId = 'b' (was b, still b)
8. **closeCard last card sets active to null** — open 'a', closeCard('a'), openCardIds = [], activeCardId = null
9. **bringToFront reorders openCardIds** — open a/b/c, bringToFront('a'), openCardIds = ['b', 'c', 'a'] (a is now last = topmost)
10. **positions cascade** — open 'a', 'b', 'c' → positions differ: `positions['a']` is at default anchor (48,48), `positions['b']` is (72,72), `positions['c']` is (96,96)
11. **positions cleaned on close** — open 'a', closeCard('a') → `positions['a']` is `undefined`
12. **localStorage persistence** — open 'a' + 'b', reload store (create new Pinia + new store instance) → `openCardIds` = ['a', 'b'], positions restored

#### 7c: `chatDock` store test — add z-index composable assertion

**File:** `src/web-ui/app/stores/__tests__/chatDock.test.ts`

Add one test at the end of the existing `describe('chatDock store', ...)` block:
13. **dock registers with popup z-index stack on open** — mock `usePopupZIndex` to verify `registerPopup('chat-dock', 'dock', ...)` is called when the dock opens. Alternatively, test directly: set `dock.isOpen = true` in a component mount, assert the dock's z-index comes from `usePopupZIndex.zIndexFor('chat-dock')` rather than a static value. Choose the direct store test approach (no mount needed): import `usePopupZIndex`, register the dock manually, verify `zIndexFor('chat-dock')` returns 50, then register a card popup, verify `zIndexFor('chat-dock')` still returns 50 (card got 51, dock stayed at 50).

## Acceptance

- [ ] `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build`
- [ ] `cd src/web-ui && pnpm test` (new + existing chat-dock tests pass)

## Manual validation

Create `docs/manual-validation/2026-08-02-phase-7-chat-plan-19b-card-popup-infra-matrix.md`:

| # | Scenario | Steps | Expected |
|---|----------|-------|----------|
| 1 | Dock opens with z-index 50 | Open dock via FAB button. Inspect dock's `z-index` style in DevTools. | `z-index: 50` |
| 2 | Open card popup 1 | Call `cardPopup.openCard('card-1')` from browser console. Card popup appears. | Card popup `z-index: 51`, dock stays at 50 |
| 3 | Open card popup 2 | Call `cardPopup.openCard('card-2')`. | Card-2 gets `z-index: 52`. Clicking card-2's header → it moves to front. |
| 4 | Open card popup 3 | Call `cardPopup.openCard('card-3')`. | Card-3 gets `z-index: 53`. |
| 5 | Max-3 rejection | Call `cardPopup.openCard('card-4')`. | Error toast "Close a card popup first (max 3 open)". No 4th popup. |
| 6 | Escape LIFO — card first | With dock (50) + card-1 (51) + card-2 (52) open. Press Escape. | Card-2 closes. Press Escape again → card-1 closes. Press Escape again → dock closes. |
| 7 | Escape LIFO — card opened after dock | Open dock (50). Open card-1 (51). Press Escape. | Card-1 closes (topmost). Dock stays open at z-index 50. |
| 8 | Escape when nothing open | Close everything. Press Escape. | No error, no toast, nothing happens. |
| 9 | Drag brings to front | Open card-1 (51) and card-2 (52). Drag card-1 by its header. | Card-1 gets `z-index: 52`, card-2 drops to 51. |
| 10 | Hard reload restores open cards | Open card-1 + card-2. Hard reload browser. | Both card popups re-open at their last positions. |
| 11 | Dock Escape after card popups close | Open dock (50), open card-1 (51). Press Escape twice. | Card-1 closes, then dock closes (both via Escape). Dock FAB reappears. |

## Notes for Plan 20

- Plan 20 replaces `CardPopupLayer`'s placeholder slot with the migrated `CardModal.vue` body (details/checklist/comments/related/docs tabs + the new Chat tab).
- Plan 20 wires `openCard` into the board's card-click / `BoardCard` open path (replacing the single `setOpenCardId` + `CardModal` mount with `cardPopup.openCard(cardId)`).
- The shared `usePopupZIndex` composable is the single z-order source for all popups from here on — no new popup should hardcode `z-50`/`z-40` etc.