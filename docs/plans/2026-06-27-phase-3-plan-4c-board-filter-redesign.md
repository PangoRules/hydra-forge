# Board Filter Redesign — Plan 4C

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Branch:** `task/phase-3-board-filter-redesign`
**Parent branch:** `feat/phase-3-web-ui`
**Prerequisite:** Plan 4B (E2E testing foundation) merged.

**Goal:** Replace the global card-type dropdown filter with a column-visibility multi-chip selector. Card type filtering stays — but only at the column level (already implemented, in-memory). All other global filters (search, assignee, includeArchived, hideEmptyColumns) are unchanged.

**Motivation:** Card type as a global filter is redundant — it re-does what the per-column type dropdown already does. What's missing is the ability to hide entire columns. ADO-style: user picks which columns they care about; irrelevant columns collapse away. When column selection is active, `hideEmptyColumns` is disabled (selecting a column then having it auto-hidden is contradictory).

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

- [ ] **Step 1: Update `BoardFilters` interface**

```ts
export interface BoardFilters {
  search: string
  includeArchived: boolean
  hideEmptyColumns: boolean
  assigneeUserId: string | null
  visibleColumnIds: string[]   // empty = all columns shown
}
```

- [ ] **Step 2: Update default state**

```ts
const boardFilters = ref<BoardFilters>({
  search: '',
  includeArchived: false,
  hideEmptyColumns: false,
  assigneeUserId: null,
  visibleColumnIds: [],
})
```

- [ ] **Step 3: Remove `type` from `fetchBoard` query params**

Remove:
```ts
if (boardFilters.value.type !== null) searchParams.set('type', String(boardFilters.value.type))
```

- [ ] **Step 4: Update `visibleColumns` computed**

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

- [ ] **Step 5: Verify typecheck**

```bash
cd src/web-ui && pnpm typecheck
```

- [ ] **Step 6: Commit**

```bash
git add app/stores/board.ts
git commit -m "feat(board): replace type filter with visibleColumnIds in BoardFilters"
```

---

## Task 2: Update useBoardFilters composable

**Files:** `app/composables/useBoardFilters.ts`

- [ ] **Step 1: Remove `type`, add `visibleColumnIds`**

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

- [ ] **Step 2: Verify typecheck**

```bash
cd src/web-ui && pnpm typecheck
```

- [ ] **Step 3: Commit**

```bash
git add app/composables/useBoardFilters.ts
git commit -m "feat(board): add visibleColumnIds + columnSelectionActive to useBoardFilters"
```

---

## Task 3: Update BoardFilterBar (desktop)

**Files:** `app/components/board/BoardFilterBar.vue`

Currently: search input, type dropdown, assignee dropdown, includeArchived checkbox, hideEmptyColumns checkbox, + Card button.

After: same but type dropdown → column chips row.

- [ ] **Step 1: Replace type dropdown with column chips**

The component receives `columns` as a prop (list of `ColumnResponse` from the board store) so chips can render column names. If the component doesn't receive columns yet, add the prop.

```ts
// Props addition
const props = defineProps<{
  members: MemberResponse[]
  columns: ColumnResponse[]
}>()
```

```ts
const { search, assigneeUserId, includeArchived, hideEmptyColumns, visibleColumnIds, columnSelectionActive } = useBoardFilters()

function toggleColumn(id: string) {
  const current = visibleColumnIds.value
  visibleColumnIds.value = current.includes(id)
    ? current.filter(c => c !== id)
    : [...current, id]
}
```

Column chips template (replace the type `<select>` block):
```html
<!-- Column visibility chips -->
<div class="flex items-center gap-1 flex-wrap">
  <span class="text-xs text-gray-500 whitespace-nowrap">Columns:</span>
  <button
    v-for="col in columns"
    :key="col.id"
    type="button"
    class="px-2 py-0.5 rounded-full text-xs border transition-colors"
    :class="visibleColumnIds.includes(col.id)
      ? 'bg-primary-500 text-white border-primary-500'
      : 'bg-white text-gray-600 border-gray-300 hover:border-primary-400'"
    @click="toggleColumn(col.id)"
  >
    {{ col.name }}
  </button>
</div>
```

- [ ] **Step 2: Disable hideEmptyColumns when column selection active**

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
```

Add tooltip on the wrapper when disabled so user knows why:
```html
<span v-if="columnSelectionActive" class="text-xs text-gray-400">(column selected)</span>
```

- [ ] **Step 3: Wire `columns` prop from BoardView**

In `BoardView.vue` (or `board.vue` page), pass columns to `BoardFilterBar`:
```html
<BoardFilterBar :members="members" :columns="board.columns" />
```

- [ ] **Step 4: Verify typecheck + visual check**

```bash
cd src/web-ui && pnpm typecheck
```

Start dev server and confirm:
- Column chips render with correct names
- Clicking a chip toggles its selected state (highlighted)
- Selecting a chip hides other columns on the board
- `hideEmptyColumns` checkbox is greyed out and disabled when any chip selected
- Deselecting all chips restores all columns and re-enables `hideEmptyColumns`

- [ ] **Step 5: Commit**

```bash
git add app/components/board/BoardFilterBar.vue app/components/board/BoardView.vue app/pages/projects/\[id\]/board.vue
git commit -m "feat(board-filter): replace type dropdown with column visibility chips (desktop)"
```

---

## Task 4: Update BoardMobileList (mobile)

**Files:** `app/components/board/BoardMobileList.vue`

- [ ] **Step 1: Remove global `filterType` from `filterCards()`**

Currently `filterCards()` applies `filterType.value` from `useBoardFilters`. Remove that clause — type filtering in mobile is per-column only (already via `columnTypeFilters`).

```ts
// Remove these lines from filterCards():
if (filterType.value !== null) {
  filtered = filtered.filter(c => c.type === filterType.value)
}
```

Also remove `type: filterType` from the `useBoardFilters` destructure.

- [ ] **Step 2: Remove type from mobile filter panel**

In the collapsible filter panel, remove the type `<select>` block entirely.

- [ ] **Step 3: Add column chips to mobile filter panel**

Inside the filter panel (same panel that has assignee, includeArchived, etc.):

```ts
const { search, assigneeUserId, includeArchived, hideEmptyColumns, visibleColumnIds, columnSelectionActive } = useBoardFilters()

function toggleColumn(id: string) {
  const current = visibleColumnIds.value
  visibleColumnIds.value = current.includes(id)
    ? current.filter(c => c !== id)
    : [...current, id]
}
```

```html
<!-- Column visibility chips (mobile filter panel) -->
<div class="flex flex-col gap-1">
  <span class="text-xs text-gray-500">Columns:</span>
  <div class="flex flex-wrap gap-1">
    <button
      v-for="col in board.columns"
      :key="col.id"
      type="button"
      class="px-2 py-0.5 rounded-full text-xs border transition-colors"
      :class="visibleColumnIds.includes(col.id)
        ? 'bg-primary-500 text-white border-primary-500'
        : 'bg-white text-gray-600 border-gray-300'"
      @click="toggleColumn(col.id)"
    >
      {{ col.name }}
    </button>
  </div>
</div>
```

- [ ] **Step 4: Disable `hideEmptyColumns` in mobile panel when column selection active**

Same pattern as desktop — disable + show "(column selected)" note.

- [ ] **Step 5: Accordion rendering uses `visibleColumns` from store**

Confirm mobile accordion iterates `board.visibleColumns` (already the case since plan 4a). No change needed if it does — just verify.

- [ ] **Step 6: Verify typecheck + visual check (mobile)**

```bash
cd src/web-ui && pnpm typecheck
```

Test on mobile viewport:
- Column chips appear in filter panel
- Toggling hides/shows accordion sections for those columns
- Per-column type dropdown still works independently
- `hideEmptyColumns` disabled when chips active

- [ ] **Step 7: Commit**

```bash
git add app/components/board/BoardMobileList.vue
git commit -m "feat(board-filter): column visibility chips + remove global type filter (mobile)"
```

---

## Task 5: Update tests

**Files:**
- `app/stores/__tests__/board.test.ts`
- `app/components/board/__tests__/BoardFilterBar.test.ts`
- `app/components/board/__tests__/BoardMobileList.test.ts`

- [ ] **Step 1: board.test.ts — update BoardFilters shape**

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

- [ ] **Step 2: BoardFilterBar.test.ts — replace type dropdown tests with chip tests**

Remove: tests asserting type `<select>` renders + type change emits.

Add:
```ts
it('renders a chip for each column', () => {
  const wrapper = mount(BoardFilterBar, {
    props: { members: [], columns: [
      { id: 'c1', name: 'Backlog' },
      { id: 'c2', name: 'In Dev' },
    ]}
  })
  const chips = wrapper.findAll('[data-testid="column-chip"]')
  expect(chips).toHaveLength(2)
  expect(chips[0].text()).toBe('Backlog')
})

it('toggles column selection on chip click', async () => {
  // set up store with empty visibleColumnIds
  // click chip → visibleColumnIds gains that id
  // click again → removed
})

it('disables hideEmptyColumns when column selected', async () => {
  // select a chip
  const checkbox = wrapper.find('[data-testid="hide-empty-checkbox"]')
  expect(checkbox.attributes('disabled')).toBeDefined()
})
```

Add `data-testid` attributes to chips and the hideEmptyColumns checkbox in the component to make tests reliable.

- [ ] **Step 3: BoardMobileList.test.ts — update filter panel**

Remove type dropdown assertions. Add chip assertions mirroring desktop tests.

- [ ] **Step 4: Run tests**

```bash
cd src/web-ui && pnpm test --run
```

All tests must pass.

- [ ] **Step 5: Commit**

```bash
git add app/stores/__tests__/board.test.ts app/components/board/__tests__/BoardFilterBar.test.ts app/components/board/__tests__/BoardMobileList.test.ts
git commit -m "test(board-filter): update tests for column visibility chips"
```

---

## Done Criteria

- [ ] No `type` in `BoardFilters` interface or `useBoardFilters` return
- [ ] No `type` query param sent to backend in `fetchBoard`
- [ ] Column chips appear in desktop filter bar — all project columns, correct names
- [ ] Column chips appear in mobile filter panel
- [ ] Selecting chip(s) shows only those columns on the board
- [ ] Selecting no chips shows all columns
- [ ] `hideEmptyColumns` disabled (greyed, non-interactive) when any chip selected
- [ ] `hideEmptyColumns` re-enabled when all chips deselected
- [ ] Per-column type dropdown in `ColumnHeader` still works (unmodified)
- [ ] All tests pass: `pnpm test --run`
- [ ] Typecheck clean: `pnpm typecheck`
