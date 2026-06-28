# Board Filter Redesign — Plan 4C

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Branch:** `task/phase-3-board-filter-redesign`
**Parent branch:** `feat/phase-3-web-ui`
**Prerequisite:** Plan 4B (E2E testing foundation) merged.

**Goal:** Replace the global card-type dropdown filter with a column-visibility dropdown multi-select. Card type filtering stays — but only at the column level (already implemented, in-memory). All other global filters (search, assignee, includeArchived, hideEmptyColumns) are unchanged.

**Motivation:** Card type as a global filter is redundant — it re-does what the per-column type dropdown already does. What's missing is the ability to hide entire columns. A dropdown multi-select (trigger button → popover with checkboxes) saves screen real estate compared to chips. When column selection is active, `hideEmptyColumns` is disabled (selecting a column then having it auto-hidden is contradictory).

---

## Architecture

**What changes:**
- `BoardFilters.type: number | null` → removed
- `BoardFilters.visibleColumnIds: string[]` → added (empty = all visible)
- `visibleColumns` computed: column-selection takes priority over `hideEmptyColumns`; the two are mutually exclusive at the UI level
- No backend change — `type` query param removed from `fetchBoard` API call; `visibleColumnIds` is purely client-side (columns already fetched)
- `BoardFilterBar.vue` / `BoardMobileList.vue` filter panel: remove type dropdown, add column chips

**What stays the same:**
- `ColumnHeader.vue` type dropdown — per-column in-memory type filter, unchanged
- `BoardColumn.vue` / `BoardMobileList.vue` per-column filtering logic — unchanged
- All other `BoardFilters` fields: `search`, `includeArchived`, `hideEmptyColumns`, `assigneeUserId`

---

## File Changes

### Modify
- `app/stores/board.ts` — update `BoardFilters` interface, `visibleColumns` computed, remove `type` from fetch params
- `app/composables/useBoardFilters.ts` — remove `type`, add `visibleColumnIds`
- `app/components/board/BoardFilterBar.vue` — remove type dropdown, add column chips + disable hideEmptyColumns when selection active
- `app/components/board/BoardMobileList.vue` — remove global `filterType` from `filterCards()`, remove type from filter panel, add column chips

### Test (update existing)
- `app/stores/__tests__/board.test.ts`
- `app/components/board/__tests__/BoardFilterBar.test.ts`
- `app/components/board/__tests__/BoardMobileList.test.ts`

---

## Task 1: Update BoardFilters interface and store

**Files:** `app/stores/board.ts`

- [x] **Step 1: Update `BoardFilters` interface**

```ts
export interface BoardFilters {
  search: string
  includeArchived: boolean
  hideEmptyColumns: boolean
  assigneeUserId: string | null
  visibleColumnIds: string[]   // empty = all columns shown
}
```

- [x] **Step 2: Update default state**

```ts
const boardFilters = ref<BoardFilters>({
  search: '',
  includeArchived: false,
  hideEmptyColumns: false,
  assigneeUserId: null,
  visibleColumnIds: [],
})
```

- [x] **Step 3: Remove `type` from `fetchBoard` query params**

Remove:
```ts
if (boardFilters.value.type !== null) searchParams.set('type', String(boardFilters.value.type))
```

- [x] **Step 4: Update `visibleColumns` computed**

```ts
const visibleColumns = computed(() => {
  // Column selection takes priority — explicit selection, show exactly those
  if (boardFilters.value.visibleColumnIds.length > 0) {
    return columns.value.filter(c => boardFilters.value.visibleColumnIds.includes(c.id))
  }
  // hideEmptyColumns only applies when no explicit selection
  if (!boardFilters.value.hideEmptyColumns) return columns.value
  const colIdsWithCards = new Set(
    columns.value
      .filter(c => (cardsByColumn.value.get(c.id) ?? []).length > 0)
      .map(c => c.id)
  )
  return columns.value.filter(c => colIdsWithCards.has(c.id))
})
```

- [x] **Step 5: Verify typecheck**

```bash
cd src/web-ui && pnpm typecheck
```

- [x] **Step 6: Commit**

```bash
git add app/stores/board.ts
git commit -m "feat(board): replace type filter with visibleColumnIds in BoardFilters"
```

---

## Task 2: Update useBoardFilters composable

**Files:** `app/composables/useBoardFilters.ts`

- [x] **Step 1: Remove `type`, add `visibleColumnIds`**

```ts
export function useBoardFilters() {
  const board = useBoardStore()

  const search = computed({
    get: () => board.boardFilters.search,
    set: (val: string) => { board.boardFilters.search = val }
  })

  const assigneeUserId = computed({
    get: () => board.boardFilters.assigneeUserId,
    set: (val: string | null) => { board.boardFilters.assigneeUserId = val }
  })

  const includeArchived = computed({
    get: () => board.boardFilters.includeArchived,
    set: (val: boolean) => { board.boardFilters.includeArchived = val }
  })

  const hideEmptyColumns = computed({
    get: () => board.boardFilters.hideEmptyColumns,
    set: (val: boolean) => { board.boardFilters.hideEmptyColumns = val }
  })

  const visibleColumnIds = computed({
    get: () => board.boardFilters.visibleColumnIds,
    set: (val: string[]) => { board.boardFilters.visibleColumnIds = val }
  })

  const columnSelectionActive = computed(() => board.boardFilters.visibleColumnIds.length > 0)

  return { search, assigneeUserId, includeArchived, hideEmptyColumns, visibleColumnIds, columnSelectionActive }
}
```

- [x] **Step 2: Verify typecheck**

```bash
cd src/web-ui && pnpm typecheck
```

- [x] **Step 3: Commit**

```bash
git add app/composables/useBoardFilters.ts
git commit -m "feat(board): add visibleColumnIds + columnSelectionActive to useBoardFilters"
```

---

## Task 3: Update BoardFilterBar (desktop)

**Files:** `app/components/board/BoardFilterBar.vue`

Currently: search input, type dropdown, assignee dropdown, includeArchived checkbox, hideEmptyColumns checkbox, + Card button.

After: same but type dropdown → column visibility dropdown multi-select.

- [x] **Step 1: Replace type dropdown with column visibility dropdown multi-select**

The component receives `columns` as a prop (list of `ColumnResponse` from the board store). If not already present, add the prop.

```ts
// Props addition
const props = defineProps<{
  members: MemberResponse[]
  columns: ColumnResponse[]
}>()
```

Script additions:
```ts
const { search, assigneeUserId, includeArchived, hideEmptyColumns, visibleColumnIds, columnSelectionActive, toggleColumnVisibility } = useBoardFilters()

const showColumnPicker = ref(false)
const columnPickerRef = ref<HTMLElement | null>(null)

// Close dropdown on outside click
onClickOutside(columnPickerRef, () => { showColumnPicker.value = false })
```

Import `onClickOutside` from `@vueuse/core` (add to existing import line):
```ts
import { onClickOutside } from '@vueuse/core'
```

Template — replace the type `<select>` block with:
```html
<!-- Column visibility dropdown -->
<div class="relative" ref="columnPickerRef">
  <UButton
    size="sm"
    variant="outline"
    data-testid="column-visibility-trigger"
    @click="showColumnPicker = !showColumnPicker"
  >
    {{ columnSelectionActive ? `${visibleColumnIds.length} column${visibleColumnIds.length > 1 ? 's' : ''}` : 'All columns' }}
  </UButton>
  <div
    v-if="showColumnPicker"
    class="absolute top-full left-0 mt-1 z-50 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-md shadow-lg p-2 min-w-[160px]"
  >
    <label
      v-for="col in columns"
      :key="col.id"
      class="flex items-center gap-2 px-2 py-1 text-xs hover:bg-gray-100 dark:hover:bg-gray-700 rounded cursor-pointer whitespace-nowrap"
    >
      <input
        type="checkbox"
        :checked="visibleColumnIds.includes(col.id)"
        @change="toggleColumnVisibility(col.id)"
        class="rounded"
      >
      {{ col.name }}
    </label>
  </div>
</div>
```

- [x] **Step 2: Disable hideEmptyColumns when column selection active**

(Unchanged from chip version — same logic applies)

```html
<label class="flex items-center gap-1 text-xs" :class="columnSelectionActive ? 'opacity-40 cursor-not-allowed' : ''">
  <input
    type="checkbox"
    :checked="hideEmptyColumns"
    :disabled="columnSelectionActive"
    @change="hideEmptyColumns = ($event.target as HTMLInputElement).checked"
  />
  Hide empty
</label>
<span v-if="columnSelectionActive" class="text-xs text-gray-400">(column selected)</span>
```

- [x] **Step 3: Wire `columns` prop from BoardView**

In `BoardView.vue` (or `board.vue` page), pass columns to `BoardFilterBar`:
```html
<BoardFilterBar :members="members" :columns="board.columns" />
```

- [x] **Step 4: Verify typecheck + visual check**

```bash
cd src/web-ui && pnpm typecheck
```

Start dev server and confirm:
- "All columns" trigger button renders in filter bar
- Clicking opens dropdown with checkboxes for each column name
- Checking a column shows only that column on the board
- Unchecking all shows all columns
- `hideEmptyColumns` checkbox is greyed out when column(s) selected
- Dropdown closes on outside click

- [x] **Step 5: Commit**

```bash
git add app/components/board/BoardFilterBar.vue app/components/board/BoardView.vue app/pages/projects/\[id\]/board.vue
git commit -m "feat(board-filter): replace type dropdown with column visibility dropdown (desktop)"
```

---

## Task 4: Update BoardMobileList (mobile)

**Files:** `app/components/board/BoardMobileList.vue`

- [x] **Step 1: Remove global `filterType` from `filterCards()`**

Currently `filterCards()` applies `filterType.value` from `useBoardFilters`. Remove that clause — type filtering in mobile is per-column only (already via `columnTypeFilters`).

```ts
// Remove these lines from filterCards():
if (filterType.value !== null) {
  filtered = filtered.filter(c => c.type === filterType.value)
}
```

Also remove `type: filterType` from the `useBoardFilters` destructure.

- [x] **Step 2: Remove type from mobile filter panel**

In the collapsible filter panel, remove the type `<select>` block entirely.

- [x] **Step 3: Add column visibility dropdown to mobile filter panel**

Inside the filter panel (same panel that has assignee, includeArchived, etc.):

```ts
const { search, assigneeUserId: filterAssignee, includeArchived, hideEmptyColumns, visibleColumnIds, columnSelectionActive, toggleColumnVisibility } = useBoardFilters()

const showColumnPicker = ref(false)
const columnPickerRef = ref<HTMLElement | null>(null)

onClickOutside(columnPickerRef, () => { showColumnPicker.value = false })
```

Template — replace type `<select>` block with:
```html
<!-- Column visibility dropdown (mobile filter panel) -->
<div class="relative" ref="columnPickerRef">
  <UButton
    size="sm"
    variant="outline"
    data-testid="column-visibility-trigger"
    @click="showColumnPicker = !showColumnPicker"
  >
    {{ columnSelectionActive ? `${visibleColumnIds.length} column${visibleColumnIds.length > 1 ? 's' : ''}` : 'All columns' }}
  </UButton>
  <div
    v-if="showColumnPicker"
    class="absolute top-full left-0 mt-1 z-50 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-md shadow-lg p-2 min-w-[160px]"
  >
    <label
      v-for="col in columns"
      :key="col.id"
      class="flex items-center gap-2 px-2 py-1 text-xs hover:bg-gray-100 dark:hover:bg-gray-700 rounded cursor-pointer whitespace-nowrap"
    >
      <input
        type="checkbox"
        :checked="visibleColumnIds.includes(col.id)"
        @change="toggleColumnVisibility(col.id)"
        class="rounded"
      >
      {{ col.name }}
    </label>
  </div>
</div>
```

- [x] **Step 4: Disable `hideEmptyColumns` in mobile panel when column selection active**

Same pattern as desktop — disable + show "(column selected)" note.

- [x] **Step 5: Accordion rendering uses `visibleColumns` from store**

Confirm mobile accordion iterates `board.visibleColumns` (already the case since plan 4a). No change needed if it does — just verify.

- [x] **Step 6: Verify typecheck + visual check (mobile)**

```bash
cd src/web-ui && pnpm typecheck
```

Test on mobile viewport:
- "All columns" trigger button renders in filter panel
- Clicking opens dropdown with checkboxes for each column name
- Checking/unchecking hides/shows accordion sections
- Per-column type dropdown still works independently
- `hideEmptyColumns` disabled when selection active
- Dropdown closes on outside click

- [x] **Step 7: Commit**

```bash
git add app/components/board/BoardMobileList.vue
git commit -m "feat(board-filter): column visibility dropdown + remove global type filter (mobile)"
```

---

## Task 5: Update tests

**Files:**
- `app/stores/__tests__/board.test.ts`
- `app/components/board/__tests__/BoardFilterBar.test.ts`
- `app/components/board/__tests__/BoardMobileList.test.ts`

- [x] **Step 1: board.test.ts — update BoardFilters shape**

Replace any `type` references in test setup with `visibleColumnIds`. Update `visibleColumns` computed tests:
- No selection + `hideEmptyColumns: false` → all columns returned
- No selection + `hideEmptyColumns: true` → empty columns filtered out
- Selection active → only selected columns returned regardless of `hideEmptyColumns`

```ts
it('returns only selected columns when visibleColumnIds set', () => {
  store.columns = [
    { id: 'c1', name: 'Backlog', position: 0, wipLimit: null, color: null },
    { id: 'c2', name: 'In Dev', position: 1, wipLimit: null, color: null },
  ]
  store.boardFilters.visibleColumnIds = ['c2']
  store.boardFilters.hideEmptyColumns = true
  // hideEmptyColumns ignored when selection active
  expect(store.visibleColumns.map(c => c.id)).toEqual(['c2'])
})

it('hides empty columns when hideEmptyColumns true and no selection', () => {
  store.columns = [
    { id: 'c1', name: 'Backlog', position: 0, wipLimit: null, color: null },
    { id: 'c2', name: 'In Dev', position: 1, wipLimit: null, color: null },
  ]
  store.cardsByColumn = new Map([['c2', [{ id: 'card1' } as any]]])
  store.boardFilters.hideEmptyColumns = true
  store.boardFilters.visibleColumnIds = []
  expect(store.visibleColumns.map(c => c.id)).toEqual(['c2'])
})
```

- [x] **Step 2: BoardFilterBar.test.ts — replace type dropdown tests with column visibility dropdown tests**

Remove: tests asserting type `<select>` renders + type change emits.

Replace with:
```ts
it('renders column visibility trigger button', () => {
  const wrapper = mount(BoardFilterBar, {
    props: { members: [], columns: [
      { id: 'c1', name: 'Backlog' },
      { id: 'c2', name: 'In Dev' },
    ]}
  })
  expect(wrapper.find('[data-testid="column-visibility-trigger"]').exists()).toBe(true)
  expect(wrapper.text()).toContain('All columns')
})

it('opens dropdown with checkboxes on trigger click', async () => {
  const wrapper = mount(BoardFilterBar, {
    props: { members: [], columns: [
      { id: 'c1', name: 'Backlog' },
    ]}
  })
  await wrapper.find('[data-testid="column-visibility-trigger"]').trigger('click')
  expect(wrapper.text()).toContain('Backlog')
  const checkbox = wrapper.find('input[type="checkbox"]')
  expect(checkbox.exists()).toBe(true)
})

it('toggles column visibility on checkbox change', async () => {
  const board = useBoardStore()
  const wrapper = mount(BoardFilterBar, {
    props: { members: [], columns: [
      { id: 'c1', name: 'Backlog' },
    ]}
  })
  await wrapper.find('[data-testid="column-visibility-trigger"]').trigger('click')
  const checkbox = wrapper.find('input[type="checkbox"]')
  await checkbox.setValue(true)
  expect(board.boardFilters.visibleColumnIds).toEqual(['c1'])
  await checkbox.setValue(false)
  expect(board.boardFilters.visibleColumnIds).toEqual([])
})

it('disables hideEmptyColumns when column selected', async () => {
  const board = useBoardStore()
  board.boardFilters.visibleColumnIds = ['c1']
  const wrapper = mount(BoardFilterBar, {
    props: { members: [], columns: [
      { id: 'c1', name: 'Backlog' },
    ]}
  })
  const checkbox = wrapper.find('[data-testid="hide-empty-checkbox"]')
  expect(checkbox.attributes('disabled')).toBeDefined()
})
```

Add `data-testid="column-visibility-trigger"` to the trigger button and `data-testid="hide-empty-checkbox"` to the hideEmpty checkbox in the component to make tests reliable.

- [x] **Step 3: BoardMobileList.test.ts — update filter panel**

Remove chip assertions. Add column visibility trigger + checkbox tests mirroring desktop pattern.

- [x] **Step 4: Run tests**

```bash
cd src/web-ui && pnpm test --run
```

All tests must pass.

- [x] **Step 5: Commit**

```bash
git add app/stores/__tests__/board.test.ts app/components/board/__tests__/BoardFilterBar.test.ts app/components/board/__tests__/BoardMobileList.test.ts
git commit -m "test(board-filter): update tests for column visibility chips"
```

---

## Done Criteria

- [x] No `type` in `BoardFilters` interface or `useBoardFilters` return
- [x] No `type` query param sent to backend in `fetchBoard`
- [x] Column visibility dropdown trigger button in desktop filter bar — shows "All columns" or "N columns"
- [x] Column visibility dropdown trigger button in mobile filter panel
- [x] Clicking trigger opens popover with checkboxes per column
- [x] Checking a column checkbox shows only that column on the board
- [x] Unchecking all checkboxes shows all columns
- [x] `hideEmptyColumns` disabled (greyed, non-interactive) when any column selected
- [x] `hideEmptyColumns` re-enabled when all columns deselected
- [x] Dropdown popover closes on outside click
- [x] Per-column type dropdown in `ColumnHeader` still works (unmodified)
- [x] All tests pass: `pnpm test --run`
- [x] Typecheck clean: `pnpm typecheck`
