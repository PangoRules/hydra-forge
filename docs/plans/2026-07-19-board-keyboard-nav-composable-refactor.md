# Board Keyboard Nav Composable Refactor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the Tab/hjkl highlight desync on the board, then extract board keyboard-nav state and handlers out of `board.vue` into two reusable composables (`useRovingFocus`, `useBoardKeyboardNav`) under a new `app/composables/keyboard/` folder, hardening `useKeyboard.ts`'s dispatch along the way.

**Architecture:** `useRovingFocus.ts` is a generic single-axis selection primitive (index + wraparound next/prev + select/selectById). `useBoardKeyboardNav.ts` composes two instances of it (columns axis, cards-within-column axis), owns every `Board`-scope `keyboard.register(...)` call, and exposes `selectedColumnIndex`/`selectedCardIndex`/`selectedCardId`/`syncToCard`/`syncToColumn`/`clampSelection`. `board.vue` shrinks to one call site. `useKeyboard.ts` gains an `enabled?: () => boolean` predicate per shortcut so `Board` shortcuts self-disable while any modal is open, replacing 9 duplicated inline guards.

**Tech Stack:** Nuxt 4 composables (auto-import + explicit import, matching existing mixed convention), Vitest, Vue 3 Composition API.

**Spec ref:** `docs/specs/2026-07-19-board-keyboard-nav-composable-refactor-design.md`

## Global Constraints

- No `console.log`/`console.error`/`console.warn` in production code (CLAUDE.md).
- Comments only when the WHY is non-obvious — no restating what code does.
- D-43 pattern: composables that own shared state read/write the underlying store directly; never duplicate state in local refs synced via watchers.
- No unnecessary abstractions — three similar lines beat a premature helper.
- Preserve exact existing runtime behavior for every migrated shortcut (wraparound direction, archived-project guards, modal-open guards) — this is a refactor, not a behavior change, except for the one bug fix in Task 1.

---

### Task 1: Fix the BoardView focus-ring / selection-ring desync

**Files:**
- Modify: `src/web-ui/app/components/board/BoardView.vue:20-25` (emits), `:51-55` (template root div)
- Test: `src/web-ui/app/components/board/__tests__/BoardView.test.ts` (new file)

**Interfaces:**
- Produces: `BoardView` emits a new `'container-focus': []` event when its root div receives native DOM focus. Later tasks (Task 6) wire this to `nav.clampSelection()`.

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/components/board/__tests__/BoardView.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import BoardView from '~/components/board/BoardView.vue'

const baseProps = {
  columns: [{ id: 'col-1', name: 'Backlog', position: 0, wipLimit: null, color: null }],
  cardsByColumn: new Map([['col-1', []]]),
  projectId: 'p1',
  includeArchived: false
}

describe('BoardView', () => {
  it('emits container-focus when the root element receives focus', async () => {
    const wrapper = await mountSuspended(BoardView, { props: baseProps })
    await wrapper.find('[tabindex="0"]').trigger('focus')
    expect(wrapper.emitted('container-focus')).toBeTruthy()
  })

  it('does not apply a focus ring class to the root element', async () => {
    const wrapper = await mountSuspended(BoardView, { props: baseProps })
    const root = wrapper.find('[tabindex="0"]')
    expect(root.classes().join(' ')).not.toContain('focus:ring')
  })

  it('labels the root element for screen readers', async () => {
    const wrapper = await mountSuspended(BoardView, { props: baseProps })
    const root = wrapper.find('[tabindex="0"]')
    expect(root.attributes('aria-label')).toBeTruthy()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm vitest run app/components/board/__tests__/BoardView.test.ts`
Expected: FAIL — no `container-focus` emitted, `focus:ring` class still present, no `aria-label`.

- [ ] **Step 3: Fix BoardView.vue**

In `src/web-ui/app/components/board/BoardView.vue`, update the emits block (currently lines 20-25):

```ts
const emit = defineEmits<{
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
  'card-click': [card: CardResponse]
  'column-click': [columnId: string]
  'add-card': [columnId: string]
  'container-focus': []
}>()
```

Update the template root div (currently lines 51-55):

```html
  <div
    tabindex="0"
    class="flex gap-4 pb-4 flex-1 min-h-0 rounded-lg outline-none"
    aria-label="Board columns — use h j k l to navigate, Enter to open, ? for shortcuts"
    @focus="emit('container-focus')"
  >
```

This removes the `focus:ring-2 focus:ring-primary/30 focus:ring-inset` classes — that ring was a second, independent highlight mechanism competing visually with the real `selectedColumnIndex`/`selectedCardId` ring on `BoardColumn`/`BoardCard`. The selection ring already shows where hjkl acts; a container-wide ring on top of it read as a misplaced highlight.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm vitest run app/components/board/__tests__/BoardView.test.ts`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/components/board/BoardView.vue src/web-ui/app/components/board/__tests__/BoardView.test.ts
git commit -m "$(cat <<'EOF'
fix(board): remove BoardView's own focus ring, it competes with the selection ring

BoardView's tabindex=0 container painted a full-row focus ring on Tab,
independent of the selectedColumnIndex/selectedCardId ring already
shown on the selected column/card. Tab-ing into the board made the
highlight look misplaced since two rings with different geometry
appeared. The selection ring already communicates where hjkl acts;
drop the redundant container ring, keep tabindex + add an aria-label
and a container-focus emit for the composable refactor in this branch
to hook into.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Harden `useKeyboard.ts` dispatch and move it into `composables/keyboard/`

**Files:**
- Move: `src/web-ui/app/composables/useKeyboard.ts` → `src/web-ui/app/composables/keyboard/useKeyboard.ts`
- Move: `src/web-ui/app/composables/__tests__/useKeyboard.test.ts` → `src/web-ui/app/composables/keyboard/__tests__/useKeyboard.test.ts`
- Modify: `src/web-ui/app/components/card/CardModal.vue:10` (import path)
- Modify: `src/web-ui/app/components/shared/KeyboardShortcutOverlay.vue:2` (import path)
- Modify: `src/web-ui/app/pages/projects/[id]/board.vue:12` (import path only — this task does not touch board.vue's usage, Task 6 removes it)

**Interfaces:**
- Produces: `register(scope, key, handler, description, allowWhileEditing?, enabled?)` — new optional 6th parameter `enabled?: () => boolean`. When present and returns `false`, the shortcut is skipped during matching (as if unregistered). Existing 5-arg call sites (`CardModal.vue`) are unaffected — `enabled` defaults to always-true.

- [ ] **Step 1: Move the files**

```bash
mkdir -p src/web-ui/app/composables/keyboard/__tests__
git mv src/web-ui/app/composables/useKeyboard.ts src/web-ui/app/composables/keyboard/useKeyboard.ts
git mv src/web-ui/app/composables/__tests__/useKeyboard.test.ts src/web-ui/app/composables/keyboard/__tests__/useKeyboard.test.ts
```

- [ ] **Step 2: Update the three explicit import paths**

In `src/web-ui/app/components/card/CardModal.vue`, change line 10:
```ts
import { useKeyboard } from '~/composables/keyboard/useKeyboard'
```

In `src/web-ui/app/components/shared/KeyboardShortcutOverlay.vue`, change line 2:
```ts
import { useKeyboard } from '~/composables/keyboard/useKeyboard'
```

In `src/web-ui/app/pages/projects/[id]/board.vue`, change line 12:
```ts
import { useKeyboard } from '~/composables/keyboard/useKeyboard'
```

- [ ] **Step 3: Write the failing test for the `enabled` predicate**

Append to `src/web-ui/app/composables/keyboard/__tests__/useKeyboard.test.ts` (inside the existing `describe('useKeyboard', ...)` block, after the last `it`):

```ts
  it('skips a shortcut whose enabled() returns false', () => {
    const handler = vi.fn()
    kb.register('Board', 'a', handler, 'Archive', false, () => false)
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'a' }))
    expect(handler).not.toHaveBeenCalled()
  })

  it('calls a shortcut once its enabled() predicate becomes true', () => {
    let enabled = false
    const handler = vi.fn()
    kb.register('Board', 'a', handler, 'Archive', false, () => enabled)

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'a' }))
    expect(handler).not.toHaveBeenCalled()

    enabled = true
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'a' }))
    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('treats a shortcut with no enabled() as always enabled', () => {
    const handler = vi.fn()
    kb.register('Board', 'z', handler, 'Zoom')
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'z' }))
    expect(handler).toHaveBeenCalledTimes(1)
  })
```

- [ ] **Step 4: Run test to verify it fails**

Run: `cd src/web-ui && pnpm vitest run app/composables/keyboard/__tests__/useKeyboard.test.ts`
Expected: FAIL on the first new test — `disabledHandler` gets called because `enabled` isn't checked yet (TS will also complain `register` only takes 4 params... actually 5 with `allowWhileEditing`, the 6th `enabled` arg is new — expect a type error until Step 5).

- [ ] **Step 5: Implement the `enabled` predicate**

Replace the full contents of `src/web-ui/app/composables/keyboard/useKeyboard.ts`:

```ts
interface Shortcut {
  key: string
  handler: (e: KeyboardEvent) => void
  description: string
  scope: string
  allowWhileEditing?: boolean
  enabled?: () => boolean
}

// Module-level shortcuts list so all instances share the same list
const shortcuts: Shortcut[] = []

function handleKeyDown(event: KeyboardEvent) {
  // Find the last registered, currently-enabled shortcut that matches (last wins for same key)
  const matchingShortcut = [...shortcuts]
    .reverse()
    .find((s: Shortcut) => s.key === event.key && (s.enabled?.() ?? true))

  // Skip shortcuts when focus is in INPUT/TEXTAREA/contentEditable (except Escape, or shortcut opts in)
  if (matchingShortcut && !matchingShortcut.allowWhileEditing && event.key !== 'Escape') {
    if ((event.target instanceof HTMLElement)
      && (['INPUT', 'TEXTAREA'].includes(event.target.nodeName)
        || event.target.isContentEditable)) {
      return
    }
  }

  if (matchingShortcut) {
    event.preventDefault()
    event.stopPropagation()
    matchingShortcut.handler(event)
  }
}

// Global listener setup - only one listener per window
let globalListenerAttached = false

export function useKeyboard() {
  function register(scope: string, key: string, handler: (e: KeyboardEvent) => void, description: string, allowWhileEditing?: boolean, enabled?: () => boolean) {
    shortcuts.push({ key, handler, description, scope, allowWhileEditing, enabled })
  }

  function unregister(scope: string) {
    // Filter out all shortcuts with the given scope
    const filtered = shortcuts.filter((s: Shortcut) => s.scope !== scope)
    // Replace the shortcuts array with the filtered array
    shortcuts.length = 0
    shortcuts.push(...filtered)
  }

  function clear() {
    shortcuts.length = 0
  }

  function getShortcuts(scope: string) {
    return shortcuts.filter((s: Shortcut) => s.scope === scope)
  }

  function getAllShortcuts() {
    return shortcuts
  }

  // Only attach the global listener once
  if (import.meta.client && !globalListenerAttached) {
    window.addEventListener('keydown', handleKeyDown)
    globalListenerAttached = true
  }

  return {
    register,
    unregister,
    clear,
    getShortcuts,
    getAllShortcuts
  }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `cd src/web-ui && pnpm vitest run app/composables/keyboard/__tests__/useKeyboard.test.ts`
Expected: PASS (all tests, including the two new ones)

- [ ] **Step 7: Typecheck the three updated import sites**

Run: `cd src/web-ui && pnpm typecheck`
Expected: zero errors

- [ ] **Step 8: Commit**

```bash
git add src/web-ui/app/composables/keyboard/useKeyboard.ts src/web-ui/app/composables/keyboard/__tests__/useKeyboard.test.ts src/web-ui/app/components/card/CardModal.vue src/web-ui/app/components/shared/KeyboardShortcutOverlay.vue "src/web-ui/app/pages/projects/[id]/board.vue"
git commit -m "$(cat <<'EOF'
refactor(keyboard): move useKeyboard into composables/keyboard/, add enabled predicate

Moving useKeyboard.ts under a dedicated keyboard/ folder ahead of two
new composables (useRovingFocus, useBoardKeyboardNav) landing there in
this branch. register() gains an optional enabled?: () => boolean
predicate checked before a shortcut can match — this replaces the
if (isModalOpen()) return guard duplicated in every Board-scope
handler with one predicate passed at registration time.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Create `useRovingFocus.ts` — generic single-axis selection primitive

**Files:**
- Create: `src/web-ui/app/composables/keyboard/useRovingFocus.ts`
- Test: `src/web-ui/app/composables/keyboard/__tests__/useRovingFocus.test.ts`

**Interfaces:**
- Produces: `useRovingFocus(ids: () => string[])` returning `{ selectedIndex: Ref<number>, selectedId: ComputedRef<string | null>, next(): void, prev(): void, select(index: number): void, selectById(id: string): void, isSelected(index: number): boolean }`. Consumed by Task 5 (`useBoardKeyboardNav.ts`), twice (columns axis, cards axis).

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/composables/keyboard/__tests__/useRovingFocus.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { ref } from 'vue'
import { useRovingFocus } from '~/composables/keyboard/useRovingFocus'

describe('useRovingFocus', () => {
  it('starts at index 0', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    expect(nav.selectedIndex.value).toBe(0)
    expect(nav.selectedId.value).toBe('a')
  })

  it('next wraps around at the end', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    nav.next()
    nav.next()
    expect(nav.selectedIndex.value).toBe(2)
    nav.next()
    expect(nav.selectedIndex.value).toBe(0)
  })

  it('prev wraps around at the start', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    nav.prev()
    expect(nav.selectedIndex.value).toBe(2)
  })

  it('select sets an exact index', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    nav.select(2)
    expect(nav.selectedIndex.value).toBe(2)
    expect(nav.selectedId.value).toBe('c')
  })

  it('selectById finds the matching index', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    nav.selectById('b')
    expect(nav.selectedIndex.value).toBe(1)
  })

  it('selectById is a no-op for an unknown id', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    nav.selectById('zzz')
    expect(nav.selectedIndex.value).toBe(0)
  })

  it('isSelected reflects the current index', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    nav.select(1)
    expect(nav.isSelected(1)).toBe(true)
    expect(nav.isSelected(0)).toBe(false)
  })

  it('next/prev are no-ops on an empty list', () => {
    const ids = ref<string[]>([])
    const nav = useRovingFocus(() => ids.value)
    nav.next()
    expect(nav.selectedIndex.value).toBe(0)
    nav.prev()
    expect(nav.selectedIndex.value).toBe(0)
  })

  it('selectedId is null for an empty list', () => {
    const ids = ref<string[]>([])
    const nav = useRovingFocus(() => ids.value)
    expect(nav.selectedId.value).toBeNull()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm vitest run app/composables/keyboard/__tests__/useRovingFocus.test.ts`
Expected: FAIL — `useRovingFocus` module does not exist yet.

- [ ] **Step 3: Implement `useRovingFocus.ts`**

Create `src/web-ui/app/composables/keyboard/useRovingFocus.ts`:

```ts
export function useRovingFocus(ids: () => string[]) {
  const selectedIndex = ref(0)

  const selectedId = computed(() => ids()[selectedIndex.value] ?? null)

  function next() {
    const length = ids().length
    if (length === 0) return
    selectedIndex.value = (selectedIndex.value + 1) % length
  }

  function prev() {
    const length = ids().length
    if (length === 0) return
    selectedIndex.value = (selectedIndex.value - 1 + length) % length
  }

  function select(index: number) {
    selectedIndex.value = index
  }

  function selectById(id: string) {
    const index = ids().indexOf(id)
    if (index !== -1) selectedIndex.value = index
  }

  function isSelected(index: number): boolean {
    return index === selectedIndex.value
  }

  return { selectedIndex, selectedId, next, prev, select, selectById, isSelected }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm vitest run app/composables/keyboard/__tests__/useRovingFocus.test.ts`
Expected: PASS (9 tests)

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/composables/keyboard/useRovingFocus.ts src/web-ui/app/composables/keyboard/__tests__/useRovingFocus.test.ts
git commit -m "$(cat <<'EOF'
feat(keyboard): add useRovingFocus generic single-axis selection primitive

Pure index-based selection state (next/prev wraparound, select,
selectById, isSelected) with no DOM/tabindex/board opinion baked in.
useBoardKeyboardNav (next task) composes two instances of this for
the board's column/card axes; a future flat-list nav (Projects page,
chat) can plug into it directly without modification.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Create `useBoardKeyboardNav.ts` — board-specific 2D keyboard composable

**Files:**
- Create: `src/web-ui/app/composables/keyboard/useBoardKeyboardNav.ts`
- Test: `src/web-ui/app/composables/keyboard/__tests__/useBoardKeyboardNav.test.ts`

**Interfaces:**
- Consumes: `useRovingFocus` (Task 3), `useKeyboard` (Task 2), `useCardMove` from `~/composables/useCardMove` (existing, signature `useCardMove(projectId: string): { moveCardToColumn(cardId: string, targetColumnId: string, targetPosition: number): Promise<void>, isMoving: Ref<boolean> }`), `useBoardStore` from `~/stores/board` (existing, exposes `visibleColumns: ComputedRef<ColumnResponse[]>` and `cardsByColumn: Ref<Map<string, CardResponse[]>>`).
- Produces: `useBoardKeyboardNav(options)` returning `{ selectedColumnIndex: Ref<number>, selectedCardIndex: Ref<number>, selectedCardId: ComputedRef<string | null>, syncToCard(card: CardResponse): void, syncToColumn(columnId: string): void, clampSelection(): void, activate(): void, deactivate(): void }`. `activate()`/`deactivate()` register/unregister the `Board`-scope shortcuts — **the composable does NOT call `onMounted`/`onBeforeUnmount` internally** (calling those with no active component instance on the stack is a silent no-op in Vue — there is nothing to attach the hook to). The caller (Task 6, `board.vue`) calls `activate()`/`deactivate()` from its own `onMounted`/`onBeforeUnmount`, exactly like the existing `realtime.connect(projectId)`/`realtime.disconnect(projectId)` calls already in that file. Consumed by Task 6 (`board.vue`).

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/composables/keyboard/__tests__/useBoardKeyboardNav.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest'
import { ref, type Ref } from 'vue'
import { setActivePinia, createPinia } from 'pinia'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { useBoardKeyboardNav } from '~/composables/keyboard/useBoardKeyboardNav'
import { useKeyboard } from '~/composables/keyboard/useKeyboard'
import { useBoardStore } from '~/stores/board'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

// useBoardKeyboardNav explicitly imports useBoardStore and (via useCardMove) reads
// useApi/useAppToast through auto-import — mock only the auto-imported leaves and
// exercise the REAL Pinia store, matching the pattern in useColumnManage.test.ts.
const mockPOST = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn().mockResolvedValue({ data: undefined, error: undefined }),
  POST: mockPOST
}))

mockNuxtImport('useAppToast', () => () => ({
  success: vi.fn(),
  error: vi.fn(),
  remove: vi.fn(),
  clear: vi.fn()
}))

function makeCard(id: string, columnId: string): CardResponse {
  return {
    id,
    projectId: 'p1',
    columnId,
    cardNumber: 1,
    title: id,
    description: '',
    type: 0,
    position: 0,
    dueAt: null,
    version: 1,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    movedAt: new Date().toISOString(),
    archivedAt: null,
    parentCardId: null,
    assignees: [],
    watchers: []
  } as unknown as CardResponse
}

function press(key: string, opts: Partial<KeyboardEventInit> = {}) {
  window.dispatchEvent(new KeyboardEvent('keydown', { key, ...opts }))
}

describe('useBoardKeyboardNav', () => {
  let onOpenCard: Mock<(card: CardResponse) => void>
  let onCreateCard: Mock<(columnId?: string) => void>
  let onArchiveCard: Mock<(card: CardResponse) => void>
  let onShowShortcuts: Mock<() => void>
  let anyModalOpen: Ref<boolean>
  let projectArchived: Ref<boolean>
  let board: ReturnType<typeof useBoardStore>

  beforeEach(() => {
    setActivePinia(createPinia())
    useKeyboard().clear()
    mockPOST.mockReset().mockResolvedValue({ data: undefined, error: undefined })

    board = useBoardStore()
    board.columns = [
      { id: 'col-1', name: 'Backlog', position: 0, wipLimit: null, color: null },
      { id: 'col-2', name: 'In Progress', position: 1, wipLimit: null, color: null }
    ] as never
    board.cardsByColumn = new Map([
      ['col-1', [makeCard('card-1', 'col-1'), makeCard('card-2', 'col-1')]],
      ['col-2', [makeCard('card-3', 'col-2')]]
    ])

    onOpenCard = vi.fn<(card: CardResponse) => void>()
    onCreateCard = vi.fn<(columnId?: string) => void>()
    onArchiveCard = vi.fn<(card: CardResponse) => void>()
    onShowShortcuts = vi.fn<() => void>()
    anyModalOpen = ref(false)
    projectArchived = ref(false)
  })

  function setup() {
    const nav = useBoardKeyboardNav({
      projectId: 'p1',
      anyModalOpen,
      projectArchived,
      onOpenCard,
      onCreateCard,
      onArchiveCard,
      onShowShortcuts
    })
    nav.activate()
    return nav
  }

  it('starts selecting the first column and card', () => {
    const nav = setup()
    expect(nav.selectedColumnIndex.value).toBe(0)
    expect(nav.selectedCardIndex.value).toBe(0)
    expect(nav.selectedCardId.value).toBe('card-1')
  })

  it('j/k move the card selection within the current column', () => {
    const nav = setup()
    press('j')
    expect(nav.selectedCardIndex.value).toBe(1)
    press('k')
    expect(nav.selectedCardIndex.value).toBe(0)
  })

  it('l/h move the column selection and reset the card selection to 0', () => {
    const nav = setup()
    press('j')
    expect(nav.selectedCardIndex.value).toBe(1)
    press('l')
    expect(nav.selectedColumnIndex.value).toBe(1)
    expect(nav.selectedCardIndex.value).toBe(0)
    press('h')
    expect(nav.selectedColumnIndex.value).toBe(0)
    expect(nav.selectedCardIndex.value).toBe(0)
  })

  it('Enter opens the currently selected card', () => {
    const nav = setup()
    void nav
    press('l')
    press('Enter')
    expect(onOpenCard).toHaveBeenCalledWith(expect.objectContaining({ id: 'card-3' }))
  })

  it('n creates a card in the currently selected column, unless archived', () => {
    setup()
    press('n')
    expect(onCreateCard).toHaveBeenCalledWith('col-1')
    onCreateCard.mockClear()
    projectArchived.value = true
    press('n')
    expect(onCreateCard).not.toHaveBeenCalled()
  })

  it('a archives the currently selected card, unless already archived', () => {
    setup()
    press('a')
    expect(onArchiveCard).toHaveBeenCalledWith(expect.objectContaining({ id: 'card-1' }))
    onArchiveCard.mockClear()
    board.cardsByColumn.get('col-1')![0]!.archivedAt = '2026-01-01'
    press('a')
    expect(onArchiveCard).not.toHaveBeenCalled()
  })

  it('? shows the shortcut overlay', () => {
    setup()
    press('?')
    expect(onShowShortcuts).toHaveBeenCalledTimes(1)
  })

  it('Board shortcuts are disabled while a modal is open', () => {
    const nav = setup()
    anyModalOpen.value = true
    press('j')
    expect(nav.selectedCardIndex.value).toBe(0)
  })

  it('deactivate unregisters the Board shortcuts', () => {
    const nav = setup()
    nav.deactivate()
    press('j')
    expect(nav.selectedCardIndex.value).toBe(0)
  })

  it('Ctrl+Shift+ArrowRight moves the selected card to the next column and follows it', () => {
    const nav = setup()
    press('ArrowRight', { ctrlKey: true, shiftKey: true })
    expect(board.cardsByColumn.get('col-2')?.map(c => c.id)).toEqual(['card-3', 'card-1'])
    expect(board.cardsByColumn.get('col-1')?.map(c => c.id)).toEqual(['card-2'])
    expect(nav.selectedColumnIndex.value).toBe(1)
    expect(nav.selectedCardIndex.value).toBe(1)
  })

  it('Ctrl+Shift+ArrowDown reorders the selected card down within its column', () => {
    const nav = setup()
    press('ArrowDown', { ctrlKey: true, shiftKey: true })
    expect(board.cardsByColumn.get('col-1')?.map(c => c.id)).toEqual(['card-2', 'card-1'])
    expect(nav.selectedCardIndex.value).toBe(1)
  })

  it('plain ArrowRight (no modifiers) does not move a card', () => {
    setup()
    press('ArrowRight')
    expect(board.cardsByColumn.get('col-1')?.map(c => c.id)).toEqual(['card-1', 'card-2'])
  })

  it('syncToCard selects the column/card matching the given card', () => {
    const nav = setup()
    nav.syncToCard(makeCard('card-3', 'col-2'))
    expect(nav.selectedColumnIndex.value).toBe(1)
    expect(nav.selectedCardIndex.value).toBe(0)
  })

  it('syncToColumn selects the given column and resets the card index', () => {
    const nav = setup()
    press('j')
    nav.syncToColumn('col-2')
    expect(nav.selectedColumnIndex.value).toBe(1)
    expect(nav.selectedCardIndex.value).toBe(0)
  })

  it('clampSelection pulls an out-of-range card index back in bounds', () => {
    const nav = setup()
    press('l') // move to col-2, which has only 1 card
    board.cardsByColumn.set('col-2', [])
    nav.clampSelection()
    expect(nav.selectedCardIndex.value).toBe(0)
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm vitest run app/composables/keyboard/__tests__/useBoardKeyboardNav.test.ts`
Expected: FAIL — `useBoardKeyboardNav` module does not exist yet.

- [ ] **Step 3: Implement `useBoardKeyboardNav.ts`**

Create `src/web-ui/app/composables/keyboard/useBoardKeyboardNav.ts`:

```ts
import type { Ref, ComputedRef } from 'vue'
import type { components } from '~/types/api'
import { useBoardStore } from '~/stores/board'
import { useCardMove } from '~/composables/useCardMove'
import { useKeyboard } from './useKeyboard'
import { useRovingFocus } from './useRovingFocus'

type CardResponse = components['schemas']['CardResponse']

export function useBoardKeyboardNav(options: {
  projectId: string
  anyModalOpen: Ref<boolean> | ComputedRef<boolean>
  projectArchived: Ref<boolean>
  onOpenCard: (card: CardResponse) => void
  onCreateCard: (columnId?: string) => void
  onArchiveCard: (card: CardResponse) => void
  onShowShortcuts: () => void
}) {
  const board = useBoardStore()
  const keyboard = useKeyboard()
  const { moveCardToColumn } = useCardMove(options.projectId)

  const columnAxis = useRovingFocus(() => board.visibleColumns.map(c => c.id))
  const cardAxis = useRovingFocus(() => {
    const col = board.visibleColumns[columnAxis.selectedIndex.value]
    if (!col) return []
    return (board.cardsByColumn.get(col.id) ?? []).map(c => c.id)
  })

  function currentColumn() {
    return board.visibleColumns[columnAxis.selectedIndex.value]
  }

  function currentCards(): CardResponse[] {
    const col = currentColumn()
    if (!col) return []
    return board.cardsByColumn.get(col.id) ?? []
  }

  function currentCard(): CardResponse | undefined {
    return currentCards()[cardAxis.selectedIndex.value]
  }

  function syncToCard(card: CardResponse) {
    for (const [colIdx, col] of board.visibleColumns.entries()) {
      const cards = board.cardsByColumn.get(col.id) ?? []
      const cardIdx = cards.findIndex(c => c.id === card.id)
      if (cardIdx !== -1) {
        columnAxis.select(colIdx)
        cardAxis.select(cardIdx)
        break
      }
    }
  }

  function syncToColumn(columnId: string) {
    const idx = board.visibleColumns.findIndex(c => c.id === columnId)
    if (idx !== -1) {
      columnAxis.select(idx)
      cardAxis.select(0)
    }
  }

  function clampSelection() {
    const columns = board.visibleColumns
    if (columns.length === 0) return
    if (columnAxis.selectedIndex.value >= columns.length) {
      columnAxis.select(columns.length - 1)
    }
    const cards = currentCards()
    if (cards.length > 0 && cardAxis.selectedIndex.value >= cards.length) {
      cardAxis.select(cards.length - 1)
    }
  }

  function moveSelectedCard(direction: -1 | 1) {
    if (options.anyModalOpen.value || options.projectArchived.value) return
    const columns = board.visibleColumns
    if (columns.length < 2) return
    const card = currentCard()
    if (!card) return
    const targetIdx = columnAxis.selectedIndex.value + direction
    if (targetIdx < 0 || targetIdx >= columns.length) return
    const toCol = columns[targetIdx]
    if (!toCol) return
    const targetCards = board.cardsByColumn.get(toCol.id) ?? []
    const newPos = targetCards.length
    moveCardToColumn(card.id, toCol.id, newPos)
    columnAxis.select(targetIdx)
    cardAxis.select(newPos)
  }

  function reorderSelectedCard(direction: -1 | 1) {
    if (options.anyModalOpen.value || options.projectArchived.value) return
    const cards = currentCards()
    if (cards.length < 2) return
    const card = currentCard()
    if (!card) return
    const newPos = cardAxis.selectedIndex.value + direction
    if (newPos < 0 || newPos >= cards.length) return
    const col = currentColumn()
    if (!col) return
    moveCardToColumn(card.id, col.id, newPos)
    cardAxis.select(newPos)
  }

  const boardEnabled = () => !options.anyModalOpen.value

  function activate() {
    keyboard.register('Board', 'j', (e) => {
      e.preventDefault()
      if (currentCards().length === 0) return
      cardAxis.next()
    }, 'Next card', false, boardEnabled)

    keyboard.register('Board', 'k', (e) => {
      e.preventDefault()
      if (currentCards().length === 0) return
      cardAxis.prev()
    }, 'Previous card', false, boardEnabled)

    keyboard.register('Board', 'l', (e) => {
      e.preventDefault()
      if (board.visibleColumns.length === 0) return
      columnAxis.next()
      cardAxis.select(0)
    }, 'Next column', false, boardEnabled)

    keyboard.register('Board', 'h', (e) => {
      e.preventDefault()
      if (board.visibleColumns.length === 0) return
      columnAxis.prev()
      cardAxis.select(0)
    }, 'Previous column', false, boardEnabled)

    keyboard.register('Board', '?', (e) => {
      e.preventDefault()
      options.onShowShortcuts()
    }, 'Show keyboard shortcuts', false, boardEnabled)

    keyboard.register('Board', 'n', (e) => {
      e.preventDefault()
      if (options.projectArchived.value) return
      const col = currentColumn()
      if (col) options.onCreateCard(col.id)
    }, 'Create new card', false, boardEnabled)

    keyboard.register('Board', 'Enter', (e) => {
      e.preventDefault()
      const card = currentCard()
      if (card) options.onOpenCard(card)
    }, 'Open card', false, boardEnabled)

    keyboard.register('Board', 'a', (e) => {
      e.preventDefault()
      if (options.projectArchived.value) return
      const card = currentCard()
      if (card && !card.archivedAt) options.onArchiveCard(card)
    }, 'Archive card', false, boardEnabled)

    keyboard.register('Board', 'ArrowRight', (e) => {
      if (!e.ctrlKey || !e.shiftKey) return
      e.preventDefault()
      moveSelectedCard(1)
    }, 'Move card right', false, boardEnabled)

    keyboard.register('Board', 'ArrowLeft', (e) => {
      if (!e.ctrlKey || !e.shiftKey) return
      e.preventDefault()
      moveSelectedCard(-1)
    }, 'Move card left', false, boardEnabled)

    keyboard.register('Board', 'ArrowUp', (e) => {
      if (!e.ctrlKey || !e.shiftKey) return
      e.preventDefault()
      reorderSelectedCard(-1)
    }, 'Move card up', false, boardEnabled)

    keyboard.register('Board', 'ArrowDown', (e) => {
      if (!e.ctrlKey || !e.shiftKey) return
      e.preventDefault()
      reorderSelectedCard(1)
    }, 'Move card down', false, boardEnabled)
  }

  function deactivate() {
    keyboard.unregister('Board')
  }

  return {
    selectedColumnIndex: columnAxis.selectedIndex,
    selectedCardIndex: cardAxis.selectedIndex,
    selectedCardId: cardAxis.selectedId,
    syncToCard,
    syncToColumn,
    clampSelection,
    activate,
    deactivate
  }
}
```

**Note on lifecycle:** this composable deliberately does NOT call `onMounted`/`onBeforeUnmount` itself. `onMounted` registered with no active component instance on the call stack (e.g. called directly from a plain function, or after the composable's own setup body already returned control past Vue's synchronous setup phase in some call patterns) is silently ignored by Vue — the callback would simply never run. `activate()`/`deactivate()` are plain functions the caller invokes from its own component lifecycle hooks (Task 6 does this in `board.vue`), mirroring the existing `realtime.connect(projectId)`/`realtime.disconnect(projectId)` calls already in that file.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm vitest run app/composables/keyboard/__tests__/useBoardKeyboardNav.test.ts`
Expected: PASS (15 tests)

- [ ] **Step 5: Typecheck**

Run: `cd src/web-ui && pnpm typecheck`
Expected: zero errors

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/composables/keyboard/useBoardKeyboardNav.ts src/web-ui/app/composables/keyboard/__tests__/useBoardKeyboardNav.test.ts
git commit -m "$(cat <<'EOF'
feat(keyboard): add useBoardKeyboardNav, the board-specific 2D nav composable

Composes two useRovingFocus instances (columns axis, cards-within-
column axis) and owns every Board-scope keyboard.register call that
previously lived inline in board.vue's onMounted, plus the
handleCardClick/handleColumnClick selection-sync logic (renamed
syncToCard/syncToColumn) and moveSelectedCard/reorderSelectedCard.
Behavior is unchanged from the inline version — same wraparound,
same archived/modal guards, now via the enabled predicate instead of
a duplicated inline check in every handler.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Migrate `board.vue` to `useBoardKeyboardNav`

**Files:**
- Modify: `src/web-ui/app/pages/projects/[id]/board.vue`

**Interfaces:**
- Consumes: `useBoardKeyboardNav` (Task 4) — `nav.selectedColumnIndex`, `nav.selectedCardId`, `nav.syncToCard`, `nav.syncToColumn`, `nav.clampSelection`.

- [ ] **Step 1: Remove the old `useKeyboard` import, add `useBoardKeyboardNav`**

In `src/web-ui/app/pages/projects/[id]/board.vue`, replace line 12:
```ts
import { useKeyboard } from '~/composables/keyboard/useKeyboard'
```
with:
```ts
import { useBoardKeyboardNav } from '~/composables/keyboard/useBoardKeyboardNav'
```

- [ ] **Step 2: Replace the "Board navigation tracking" section**

Replace this block (currently lines 78-95):
```ts
// Board navigation tracking
const selectedColumnIndex = ref(0)
const selectedCardIndex = ref(0)

const selectedCardIdForKeyboard = computed(() => {
  const columns = board.visibleColumns
  if (!columns.length) return null
  const col = columns[selectedColumnIndex.value]
  if (!col) return null
  const cards = board.cardsByColumn.get(col.id) ?? []
  const card = cards[selectedCardIndex.value]
  return card?.id ?? null
})

const { moveCardToColumn } = useCardMove(projectId)
const realtime = useRealtime()
const presence = usePresence()
const keyboard = useKeyboard()
```

with:
```ts
const { moveCardToColumn } = useCardMove(projectId)
const realtime = useRealtime()
const presence = usePresence()

function openCardModal(card: CardResponse) {
  selectedCard.value = card
  selectedCardId.value = card.id
  showCardModal.value = true
}

function requestArchive(card: CardResponse) {
  archiveTargetCard.value = card
  showArchiveConfirm.value = true
}

const nav = useBoardKeyboardNav({
  projectId,
  anyModalOpen,
  projectArchived,
  onOpenCard: openCardModal,
  onCreateCard: handleAddCard,
  onArchiveCard: requestArchive,
  onShowShortcuts: () => { showShortcutOverlay.value = true }
})
```

- [ ] **Step 3: Replace `handleCardClick`/`handleColumnClick`**

Replace this block (currently lines 105-127):
```ts
function handleCardClick(card: CardResponse) {
  selectedCard.value = card
  selectedCardId.value = card.id
  showCardModal.value = true
  // Sync keyboard selection state to match the clicked card
  for (const [colIdx, col] of board.visibleColumns.entries()) {
    const cards = board.cardsByColumn.get(col.id) ?? []
    const cardIdx = cards.findIndex(c => c.id === card.id)
    if (cardIdx !== -1) {
      selectedColumnIndex.value = colIdx
      selectedCardIndex.value = cardIdx
      break
    }
  }
}

function handleColumnClick(columnId: string) {
  const idx = board.visibleColumns.findIndex(c => c.id === columnId)
  if (idx !== -1) {
    selectedColumnIndex.value = idx
    selectedCardIndex.value = 0
  }
}
```

with:
```ts
function handleCardClick(card: CardResponse) {
  openCardModal(card)
  nav.syncToCard(card)
}
```

(`handleColumnClick` is deleted outright — the template binds `@column-click="nav.syncToColumn"` directly, see Step 5.)

- [ ] **Step 4: Delete the inline shortcut registration block and now-composable-owned helpers**

In the `onMounted(async () => { ... })` block, delete everything from the `// Register board shortcuts` comment (currently line 194) through the closing `})` of that `onMounted` (currently line 351) — i.e. delete lines 194-350, but **keep** the `onMounted(async () => { ... })` wrapper itself and everything before line 194 (the `board.fetchBoard`/`realtime.connect`/project-fetch logic). The `onMounted` body should end right after the `projectArchived.value = !!project.archivedAt` block, immediately closing with `})`.

Concretely, this block:
```ts
  onMounted(async () => {
    ... (unchanged fetch/connect logic) ...
    if (data) {
      const project = data as components['schemas']['ProjectResponse']
      projectName.value = project.name
      projectArchived.value = !!project.archivedAt
    }

    // Register board shortcuts
    // All Board shortcuts guarded by anyModalOpen — prevent background navigation when overlay open
    function isModalOpen() {
      return anyModalOpen.value
    }

    keyboard.register('Board', 'j', ...) 
    ... (through the Ctrl+Shift+ArrowDown registration) ...
  })
```
becomes:
```ts
  onMounted(async () => {
    ... (unchanged fetch/connect logic) ...
    if (data) {
      const project = data as components['schemas']['ProjectResponse']
      projectName.value = project.name
      projectArchived.value = !!project.archivedAt
    }
    nav.activate()
  })
```

`nav.activate()` is added right after the existing fetch/connect logic — `useBoardKeyboardNav` does not register its shortcuts on its own (see Task 4's note: `onMounted` called with no active component instance is a silent no-op), so `board.vue`'s own `onMounted` must call it, the same way it already calls `realtime.connect(projectId)`/`presence.connect(projectId)` earlier in this same function.

The `moveSelectedCard`/`reorderSelectedCard` function declarations that sat between the registration calls (originally lines 292-326) are deleted along with this block — they now live inside `useBoardKeyboardNav.ts` (Task 4).

- [ ] **Step 5: Update `onBeforeUnmount`**

Replace:
```ts
onBeforeUnmount(() => {
  realtime.disconnect(projectId)
  presence.disconnect(projectId)
  // Unregister board shortcuts
  keyboard.unregister('Board')
})
```
with:
```ts
onBeforeUnmount(() => {
  realtime.disconnect(projectId)
  presence.disconnect(projectId)
  nav.deactivate()
})
```

(`nav.deactivate()` calls `keyboard.unregister('Board')` internally — see Task 4.)

- [ ] **Step 6: Update the template**

In the `<BoardView ... />` block (currently lines 536-548), change:
```html
          <BoardView
            :columns="board.visibleColumns"
            :cards-by-column="board.cardsByColumn"
            :project-id="projectId"
            :include-archived="board.boardFilters.includeArchived"
            :readonly="projectArchived"
            :selected-card-id="selectedCardIdForKeyboard"
            :selected-column-index="selectedColumnIndex"
            @card-move="moveCardToColumn"
            @card-click="handleCardClick"
            @column-click="handleColumnClick"
            @add-card="handleAddCard"
          />
```
to:
```html
          <BoardView
            :columns="board.visibleColumns"
            :cards-by-column="board.cardsByColumn"
            :project-id="projectId"
            :include-archived="board.boardFilters.includeArchived"
            :readonly="projectArchived"
            :selected-card-id="nav.selectedCardId.value"
            :selected-column-index="nav.selectedColumnIndex.value"
            @card-move="moveCardToColumn"
            @card-click="handleCardClick"
            @column-click="nav.syncToColumn"
            @add-card="handleAddCard"
            @container-focus="nav.clampSelection"
          />
```

Note: `.value` is required here — Vue's template ref auto-unwrapping only applies to top-level bindings in scope (like the old `selectedCardIdForKeyboard`), not to nested property access like `nav.selectedCardId`. Omitting `.value` type-checks as a mismatch (`ComputedRef<string | null>` is not assignable to `string`).

- [ ] **Step 7: Typecheck and lint**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: zero errors on both. If lint flags the deleted `isModalOpen` local function reference elsewhere, remove that reference too (it should not exist anymore after Step 4).

- [ ] **Step 8: Run the full vitest suite**

Run: `cd src/web-ui && pnpm vitest run`
Expected: PASS — all existing suites plus the new ones from Tasks 1, 3, 4.

- [ ] **Step 9: Commit**

```bash
git add "src/web-ui/app/pages/projects/[id]/board.vue"
git commit -m "$(cat <<'EOF'
refactor(board): migrate board.vue to useBoardKeyboardNav

Deletes ~180 lines of inline selection state, sync handlers, and
keyboard.register calls from board.vue's onMounted, replacing them
with a single useBoardKeyboardNav() call. board.vue keeps only what's
genuinely page-specific (fetch/connect lifecycle, modal state, bulk
actions); all board keyboard-nav logic now lives in one composable
that can be tested and reasoned about independently.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Documentation — D-47, plan-6 note, CLAUDE.md pattern, validation matrix

**Files:**
- Modify: `docs/DECISIONS.md` (append `D-47`)
- Modify: `docs/plans/2026-06-23-phase-3-plan-6-polish-hardening.md` (Task 21 pre-execution note)
- Modify: `CLAUDE.md` (Nuxt UI v4 patterns section)
- Modify: `docs/manual-validation/2026-06-23-phase-3-plan-6-polish-hardening-matrix.md` (§1 new row)

- [ ] **Step 1: Append `D-47` to `docs/DECISIONS.md`**

Append after the existing `D-46` entry (end of file):

```markdown

## D-47: Keyboard Navigation — Roving-Focus Composable Split

| Field | Value |
|---|---|
| **Topic** | How board keyboard navigation (hjkl, selection highlight, roving tabindex) is structured to stay reusable as more sections of the app gain keyboard nav |
| **Date** | 2026-07-19 |
| **Status** | ✅ Settled |
| **Decision** | **Keyboard-nav selection state and dispatch live in `app/composables/keyboard/`: `useRovingFocus.ts` (generic single-axis index/selection primitive, no DOM opinion) and `useBoardKeyboardNav.ts` (board-specific composable that wires two `useRovingFocus` instances — columns axis, cards-within-column axis — to `Board`-scope `keyboard.register` calls). `useKeyboard.ts` gained an `enabled?: () => boolean` predicate per shortcut, replacing a manual `if (isModalOpen()) return` guard duplicated in every Board handler.** |
| **Rationale** | The five keyboard-nav commits that shipped hjkl navigation, roving tabindex, and card move/reorder left `board.vue` holding ~270 lines of inline selection state, sync handlers, and shortcut registrations — the same shape of duplication D-43 already flagged for filter state. Along the way, a visual bug surfaced: `BoardView`'s own `tabindex=0` container carried its own `focus:ring` class, a second highlight mechanism entirely disconnected from the `selectedColumnIndex`/`selectedCardId` ring that actually reflects keyboard selection. Tab-ing into the board painted a ring shaped like the whole column row, which read as misaligned next to the real per-column/per-card ring. Splitting into a generic primitive + a board-specific wrapper fixes the duplication (one composable owns selection state, `syncToCard`/`syncToColumn` are its only write path) and gives the next keyboard-nav consumer (Projects page, later chat) a primitive to plug into directly instead of re-inlining the pattern. |
| **Alternatives considered** | 1. Single board-specific composable only, no generic primitive (rejected — YAGNI-adjacent but the columns/cards split is genuinely two instances of the same index logic; extracting it costs nothing and is exactly what a flat-list nav elsewhere would need). 2. True per-item ARIA roving-tabindex (tabindex physically moving between individual cards/columns as selection changes) (deferred, not rejected — bigger a11y rework, belongs to the still-open Task 25 ARIA pass, not this fix). 3. A scope-priority stack in `useKeyboard.ts` instead of a per-shortcut `enabled` predicate (rejected — only two scopes exist today (`Board`, `Card`); a predicate is the minimal mechanism that removes the duplicated guard without adding a new registry concept). |
| **Impact** | New folder `app/composables/keyboard/` holds `useKeyboard.ts` (moved), `useRovingFocus.ts`, `useBoardKeyboardNav.ts`. `board.vue` calls `useBoardKeyboardNav` once instead of registering 9+ shortcuts inline. `BoardView.vue` dropped its own focus ring in favor of the existing selection ring (`aria-label` added for screen readers). Any future keyboard-nav surface should start from `useRovingFocus` rather than re-inlining index/wraparound logic. |
```

- [ ] **Step 2: Update the Task 21 pre-execution note in `docs/plans/2026-06-23-phase-3-plan-6-polish-hardening.md`**

Find this line (currently line 19):
```
> - **Task 21** (keyboard shortcuts) — not built, still needed as written.
```
Replace with:
```
> - **Task 21** (keyboard shortcuts) — built (across 5 commits `5f4835f`..`09f39f1`), then refactored into `app/composables/keyboard/` (`useKeyboard.ts` + new `useRovingFocus.ts`/`useBoardKeyboardNav.ts`) per `docs/specs/2026-07-19-board-keyboard-nav-composable-refactor-design.md` and D-47 — superseding this task's original inline-in-`board.vue` Step 3 sketch. Nothing further needed here.
```

- [ ] **Step 3: Add the composable pattern to `CLAUDE.md`**

In `CLAUDE.md`, under the `### Nuxt UI v4 patterns` section, after the existing bullet that starts with `**Shared filter state & logic via composables** (D-43)`, add a new bullet:

```markdown
- **Keyboard navigation via composables** (D-47) — Any keyboard-nav surface (selection highlight + shortcut dispatch) follows the pattern in `app/composables/keyboard/`: `useRovingFocus.ts` is a generic single-axis selection primitive (index + wraparound next/prev + select/selectById), and `useBoardKeyboardNav.ts` is the board-specific composable built on top of it (2D column/card selection + all `Board`-scope `keyboard.register` calls). `useKeyboard.ts`'s `register()` takes an `enabled?: () => boolean` predicate — use it instead of an inline `if (modalOpen) return` guard duplicated per handler. Start any new keyboard-nav surface (Projects page, chat) from `useRovingFocus` rather than re-inlining index/selection logic.
```

- [ ] **Step 4: Add a validation-matrix row**

In `docs/manual-validation/2026-06-23-phase-3-plan-6-polish-hardening-matrix.md`, after row `1.2` in the `## 1. Keyboard Shortcuts (Task 21)` table, insert:

```markdown
| 1.2a | Press Tab repeatedly until focus reaches the board area, observe the board | Only the existing column/card selection ring is visible — no separate/misaligned ring appears around the whole column row | ☐ |
```

- [ ] **Step 5: Commit**

```bash
git add docs/DECISIONS.md docs/plans/2026-06-23-phase-3-plan-6-polish-hardening.md CLAUDE.md docs/manual-validation/2026-06-23-phase-3-plan-6-polish-hardening-matrix.md
git commit -m "$(cat <<'EOF'
docs: add D-47, update plan-6 Task 21 note, CLAUDE.md pattern, validation matrix

Documents the keyboard-nav composable split (useRovingFocus +
useBoardKeyboardNav) and the focus-ring bug that motivated it, so the
pattern is discoverable before the Projects page (or any future
section) needs keyboard nav.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: Final verification pass

**Files:** none (verification only)

- [ ] **Step 1: Full typecheck**

Run: `cd src/web-ui && pnpm typecheck`
Expected: zero errors

- [ ] **Step 2: Full lint**

Run: `cd src/web-ui && pnpm lint`
Expected: zero errors

- [ ] **Step 3: Full vitest suite**

Run: `cd src/web-ui && pnpm vitest run`
Expected: PASS, all suites (existing + new `BoardView.test.ts`, `useKeyboard.test.ts` additions, `useRovingFocus.test.ts`, `useBoardKeyboardNav.test.ts`)

- [ ] **Step 4: Manual smoke check**

With the API server running (`dotnet run --project src/HydraForge.Server`, `Development` env) and `cd src/web-ui && pnpm dev` running:
- Open a project board, click into the search box, then Tab forward until focus reaches the board area.
- Confirm only the existing green column/card ring is visible — no extra ring shape appears.
- Press `hjkl` and confirm the ring moves as before; press `?` for the shortcut overlay; press `Enter` to open a card; press `n` to create a card; press `a` to archive a card; press `Ctrl+Shift+ArrowRight`/`ArrowDown` to move/reorder a card.
- Confirm all match pre-refactor behavior (same as the already-passing rows in the plan-6 validation matrix §1).

No commit for this task — it's a verification gate, not a code change. If anything fails, return to the relevant task above and fix before considering the plan complete.
