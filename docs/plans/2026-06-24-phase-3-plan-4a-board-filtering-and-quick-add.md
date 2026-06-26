# Board Filtering & Quick-Add Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Branch:** `task/phase-3-board-filtering`
**Parent branch:** `feat/phase-3-web-ui`
**Parent spec:** `2026-06-23-phase-3-web-ui-design.md` — Task 4A

**Goal:** Add filter controls, archived card toggle, and quick-add card buttons to the board view (desktop + mobile).

**Architecture:** Global filter bar above columns (desktop) / slide-out panel (mobile). Per-column filter controls in column headers. Backend `GET /api/projects/{id}/Cards` already supports `type`, `includeArchived`, `assigneeUserId`, `columnId` query params — no backend changes needed. All filtering logic is client-side on the already-fetched card list.

**Tech Stack:** Vue 3 + Nuxt 4 + TypeScript + Pinia. Components use named slots, explicit imports for shared components, `useApi()` for API calls.

---

## File Structure

### Create
- `app/components/board/BoardFilterBar.vue` — global filter bar: search input, type dropdown, archived toggle, hide empty toggle, + Card button
- `app/components/board/ColumnFilterRow.vue` — per-column inline text search input (row 2 of column header)
- `app/components/board/CardCreateModal.vue` — create card form: title, description, type, column (with preselect), archived/restored events

### Modify
- `app/stores/board.ts` — add `boardFilters` state, update `fetchBoard` to pass `type` + `includeArchived` as query params, add `hideEmptyColumns` logic
- `app/components/board/ColumnHeader.vue` — add Type dropdown, Archived toggle, ⋮ menu, + Add button
- `app/components/board/BoardColumn.vue` — add per-column filter state (search, type, archived), apply client-side filter, add `+`/`-` event emit
- `app/components/board/BoardView.vue` — add `BoardFilterBar` above columns, pass filter state, handle empty-column visibility
- `app/components/board/BoardMobileList.vue` — add accordion expand/collapse, filter panel, global + button
- `app/pages/projects/[id]/board.vue` — wire `BoardFilterBar`, `CardCreateModal`, pass filter state down

### Test
- `app/components/board/__tests__/BoardFilterBar.test.ts`
- `app/components/board/__tests__/ColumnHeader.test.ts`
- `app/components/board/__tests__/BoardMobileList.test.ts` — extend existing
- `app/stores/__tests__/board.test.ts`

---

### Task 1: Board store filter state + fetchBoard params

**Files:**
- Modify: `app/stores/board.ts`

- [ ] **Step 1: Add BoardFilters interface and state**

Add after `error` ref:

```ts
export interface BoardFilters {
  search: string
  type: number | null        // CardType enum: 0=Task,1=Bug,2=Epic,3=Spec,4=Idea
  includeArchived: boolean
  hideEmptyColumns: boolean
}

export const useBoardStore = defineStore('board', () => {
  const project = ref<{ id: string, name: string } | null>(null)
  const columns = ref<ColumnResponse[]>([])
  const cardsByColumn = ref<Map<string, CardResponse[]>>(new Map())
  const loading = ref(false)
  const error = ref<string | null>(null)
  const boardFilters = ref<BoardFilters>({
    search: '',
    type: null,
    includeArchived: false,
    hideEmptyColumns: false
  })
```

- [ ] **Step 2: Update `fetchBoard` to pass query params**

Change signature and add query params to the Cards.list call:

```ts
  async function fetchBoard(projectId: string, filters?: Partial<BoardFilters>) {
    loading.value = true
    error.value = null
    if (filters) {
      Object.assign(boardFilters.value, filters)
    }
    try {
      const [columnsResult, cardsResult] = await Promise.all([
        api.GET(ApiRoutes.Columns.list(projectId)),
        api.GET(ApiRoutes.Cards.list(projectId), {
          params: {
            query: {
              includeArchived: boardFilters.value.includeArchived || undefined,
              type: boardFilters.value.type ?? undefined
            }
          }
        })
      ])
```

The rest of the function stays the same (build map, sort by position).

- [ ] **Step 3: Update `rollbackMove` to preserve filters**

```ts
  function rollbackMove(projectId: string) {
    fetchBoard(projectId)
  }
```

No change needed — `fetchBoard` already reads current `boardFilters`.

- [ ] **Step 4: Add `visibleColumns` getter**

Add after `removeCard`:

```ts
  const visibleColumns = computed(() => {
    if (!boardFilters.value.hideEmptyColumns) return columns.value
    const colIdsWithCards = new Set<string>()
    for (const [colId, cards] of cardsByColumn.value) {
      if (cards.length > 0) colIdsWithCards.add(colId)
    }
    return columns.value.filter(c => colIdsWithCards.has(c.id))
  })
```

- [ ] **Step 5: Export new members**

Replace the return block:

```ts
  return {
    project, columns, cardsByColumn, loading, error,
    fetchBoard, moveCard, rollbackMove, addCard, updateCard, removeCard,
    boardFilters, visibleColumns
  }
```

- [ ] **Step 6: Verify typecheck passes**

Run: `pnpm typecheck`
Expected: no errors

- [ ] **Step 7: Commit**

```bash
git add app/stores/board.ts
git commit -m "feat(board): add filter state and fetchBoard query params"
```

---

### Task 2: Global BoardFilterBar component

**Files:**
- Create: `app/components/board/BoardFilterBar.vue`
- Test: `app/components/board/__tests__/BoardFilterBar.test.ts`

- [ ] **Step 1: Create BoardFilterBar component**

```vue
<script setup lang="ts">
import type { BoardFilters } from '~/stores/board'

const filters = defineModel<BoardFilters>({ required: true })

const emit = defineEmits<{
  'add-card': []
}>()

const cardTypes = [
  { label: 'All', value: null },
  { label: 'Task', value: 0 },
  { label: 'Bug', value: 1 },
  { label: 'Epic', value: 2 },
  { label: 'Spec', value: 3 },
  { label: 'Idea', value: 4 }
]

function updateSearch(val: string) {
  filters.value = { ...filters.value, search: val }
}

function updateType(val: string | null) {
  filters.value = { ...filters.value, type: val ? Number(val) : null }
}

function toggleArchived() {
  filters.value = { ...filters.value, includeArchived: !filters.value.includeArchived }
}

function toggleHideEmpty() {
  filters.value = { ...filters.value, hideEmptyColumns: !filters.value.hideEmptyColumns }
}
</script>

<template>
  <div class="flex items-center gap-3 px-4 py-2 border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900">
    <input
      :value="filters.search"
      placeholder="Search cards across board..."
      class="flex-1 min-w-0 px-3 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800 focus:outline-none focus:ring-2 focus:ring-primary"
      @input="updateSearch(($event.target as HTMLInputElement).value)"
    />

    <select
      :value="filters.type ?? ''"
      class="px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
      @change="updateType(($event.target as HTMLSelectElement).value || null)"
    >
      <option
        v-for="t in cardTypes"
        :key="t.label"
        :value="t.value ?? ''"
      >
        {{ t.label }}
      </option>
    </select>

    <label class="flex items-center gap-1.5 text-sm whitespace-nowrap cursor-pointer">
      <input
        type="checkbox"
        :checked="filters.includeArchived"
        class="rounded"
        @change="toggleArchived"
      />
      Archived
    </label>

    <label class="flex items-center gap-1.5 text-sm whitespace-nowrap cursor-pointer">
      <input
        type="checkbox"
        :checked="filters.hideEmptyColumns"
        class="rounded"
        @change="toggleHideEmpty"
      />
      Hide empty
    </label>

    <UButton
      size="sm"
      icon="i-lucide-plus"
      color="primary"
      @click="emit('add-card')"
    >
      Card
    </UButton>
  </div>
</template>
```

- [ ] **Step 2: Write BoardFilterBar test**

Create `app/components/board/__tests__/BoardFilterBar.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import BoardFilterBar from '~/components/board/BoardFilterBar.vue'

const defaultFilters = {
  search: '',
  type: null,
  includeArchived: false,
  hideEmptyColumns: false
}

describe('BoardFilterBar', () => {
  it('renders search input', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    expect(wrapper.find('input[placeholder*="Search"]').exists()).toBe(true)
  })

  it('renders type dropdown with All option', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    const select = wrapper.find('select')
    expect(select.exists()).toBe(true)
    expect(select.text()).toContain('All')
  })

  it('renders archived checkbox', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    expect(wrapper.text()).toContain('Archived')
  })

  it('emits add-card on button click', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('add-card')).toBeTruthy()
  })

  it('emits update:modelValue when search changes', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    const input = wrapper.find('input[placeholder*="Search"]')
    await input.setValue('test query')
    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    const emitted = wrapper.emitted('update:modelValue')![0][0] as typeof defaultFilters
    expect(emitted.search).toBe('test query')
  })
})
```

- [ ] **Step 3: Verify tests pass**

Run: `pnpm run test`
Expected: 4 new tests pass

- [ ] **Step 4: Commit**

```bash
git add app/components/board/BoardFilterBar.vue app/components/board/__tests__/BoardFilterBar.test.ts
git commit -m "feat(board): add global BoardFilterBar component"
```

---

### Task 3: Column header filter controls

**Files:**
- Modify: `app/components/board/ColumnHeader.vue`
- Modify: `app/components/board/BoardColumn.vue`
- Modify: `app/components/board/BoardView.vue`

- [ ] **Step 1: Add emit types to ColumnHeader**

Replace entire ColumnHeader.vue:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']

defineProps<{
  column: ColumnResponse
  cardCount: number
}>()

const emit = defineEmits<{
  'add-card': []
  'filter-type': [value: number | null]
  'filter-archived': [value: boolean]
  'menu-action': [action: string]
}>()

const cardTypes = [
  { label: 'Type', value: null },
  { label: 'Task', value: 0 },
  { label: 'Bug', value: 1 },
  { label: 'Epic', value: 2 },
  { label: 'Spec', value: 3 },
  { label: 'Idea', value: 4 }
]
</script>

<template>
  <div class="px-2 pt-2 pb-1">
    <!-- Row 1: title + controls -->
    <div class="flex items-center justify-between mb-1">
      <div class="flex items-center gap-2 min-w-0">
        <span class="column-drag-handle cursor-grab text-gray-300 hover:text-gray-500 shrink-0">
          <UIcon name="i-lucide-grip-vertical" class="size-4" />
        </span>
        <div
          v-if="column.color"
          class="size-3 rounded-full shrink-0"
          :style="{ backgroundColor: column.color }"
        />
        <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-200 truncate">
          {{ column.name }}
        </h3>
        <span class="text-xs text-gray-400 bg-gray-100 dark:bg-gray-700 rounded px-1.5 py-0.5 shrink-0">
          {{ cardCount }}
        </span>
        <span
          v-if="column.wipLimit && cardCount > Number(column.wipLimit)"
          class="text-xs text-red-500 font-medium shrink-0"
        >
          WIP: {{ column.wipLimit }}
        </span>
      </div>
      <div class="flex items-center gap-1 shrink-0">
        <select
          class="text-xs px-1.5 py-1 border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
          @change="emit('filter-type', ($event.target as HTMLSelectElement).value ? Number(($event.target as HTMLSelectElement).value) : null)"
        >
          <option
            v-for="t in cardTypes"
            :key="t.label"
            :value="t.value ?? ''"
          >
            {{ t.label }}
          </option>
        </select>
        <label class="flex items-center gap-0.5 text-xs cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-700 px-1 py-1 rounded">
          <input
            type="checkbox"
            class="size-3"
            @change="emit('filter-archived', ($event.target as HTMLInputElement).checked)"
          />
          <span class="text-gray-500">Arch</span>
        </label>
        <button
          class="text-xs px-1.5 py-1 text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700 rounded"
          title="Column actions"
        >
          ⋮
        </button>
        <button
          class="text-xs px-2 py-1 rounded border border-primary text-primary bg-primary/5 hover:bg-primary/10"
          title="Add card to this column"
          @click="emit('add-card')"
        >
          + Add
        </button>
      </div>
    </div>
    <!-- Row 2: inline search -->
    <slot name="filter-row" />
  </div>
</template>
```

- [ ] **Step 2: Add per-column filter state to BoardColumn**

Replace BoardColumn.vue:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'
import ColumnHeader from '~/components/board/ColumnHeader.vue'
import BoardCard from '~/components/board/BoardCard.vue'

type CardResponse = components['schemas']['CardResponse']
type ColumnResponse = components['schemas']['ColumnResponse']

const props = defineProps<{
  column: ColumnResponse
  cards: CardResponse[]
  projectId: string
}>()

const emit = defineEmits<{
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
  'card-click': [card: CardResponse]
  'add-card': [columnId: string]
}>()

// Per-column filter state
const columnSearch = ref('')
const columnType = ref<number | null>(null)
const columnArchived = ref(false)

const filteredCards = computed(() => {
  let result = props.cards

  // Column text search
  if (columnSearch.value) {
    const q = columnSearch.value.toLowerCase()
    result = result.filter(c =>
      c.title.toLowerCase().includes(q) ||
      String(c.cardNumber).includes(q)
    )
  }

  // Column type filter
  if (columnType.value !== null) {
    result = result.filter(c => c.type === columnType.value)
  }

  // Column archived filter
  if (!columnArchived.value) {
    result = result.filter(c => !c.archivedAt)
  }

  return result
})

function handleFilterType(value: number | null) {
  columnType.value = value
}

function handleFilterArchived(value: boolean) {
  columnArchived.value = value
}
</script>

<template>
  <div class="flex flex-col bg-gray-50 dark:bg-gray-900 rounded-lg min-w-[280px] max-w-[320px] w-[300px] shrink-0">
    <ColumnHeader
      :column="column"
      :card-count="filteredCards.length"
      @add-card="emit('add-card', column.id)"
      @filter-type="handleFilterType"
      @filter-archived="handleFilterArchived"
    >
      <template #filter-row>
        <input
          :value="columnSearch"
          placeholder="Filter cards in this column..."
          class="w-full mt-1 px-2 py-1 text-xs border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800 focus:outline-none focus:ring-1 focus:ring-primary"
          @input="columnSearch = ($event.target as HTMLInputElement).value"
        />
      </template>
    </ColumnHeader>

    <div class="flex-1 p-2 space-y-2 min-h-[100px]">
      <BoardCard
        v-for="card in filteredCards"
        :key="card.id"
        :card="card"
        :project-id="projectId"
        @click="emit('card-click', card)"
      />
    </div>
  </div>
</template>
```

- [ ] **Step 3: Update BoardView to pass add-card event up**

Replace BoardView.vue:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'
import BoardColumn from '~/components/board/BoardColumn.vue'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']

defineProps<{
  columns: ColumnResponse[]
  cardsByColumn: Map<string, CardResponse[]>
  projectId: string
}>()

const emit = defineEmits<{
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
  'card-click': [card: CardResponse]
  'add-card': [columnId: string]
}>()

function handleCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  emit('card-move', cardId, targetColumnId, targetPosition)
}

function handleCardClick(card: CardResponse) {
  emit('card-click', card)
}
</script>

<template>
  <div class="flex gap-4 overflow-x-auto pb-4 h-full">
    <BoardColumn
      v-for="col in columns"
      :key="col.id"
      :column="col"
      :cards="cardsByColumn.get(col.id) ?? []"
      :project-id="projectId"
      @card-move="handleCardMove"
      @card-click="handleCardClick"
      @add-card="(colId: string) => emit('add-card', colId)"
    />
  </div>
</template>
```

- [ ] **Step 4: Verify typecheck passes**

Run: `pnpm typecheck`
Expected: no errors

- [ ] **Step 5: Commit**

```bash
git add app/components/board/ColumnHeader.vue app/components/board/BoardColumn.vue app/components/board/BoardView.vue
git commit -m "feat(board): add per-column filter controls to column headers"
```

---

### Task 4: + Add card flow (CardCreateModal)

**Files:**
- Create: `app/components/board/CardCreateModal.vue`
- Modify: `app/pages/projects/[id]/board.vue`

- [ ] **Step 1: Create CardCreateModal component**

```vue
<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'

type ColumnResponse = components['schemas']['ColumnResponse']

const props = defineProps<{
  projectId: string
  columns: ColumnResponse[]
  preselectedColumnId?: string
}>()

const emit = defineEmits<{
  close: []
  created: []
}>()

const isOpen = ref(true)
const api = useApi()
const toast = useToast()

const title = ref('')
const description = ref('')
const cardType = ref(0)
const columnId = ref(props.preselectedColumnId ?? '')
const saving = ref(false)

const canSave = computed(() => title.value.trim().length > 0 && columnId.value.length > 0)

async function handleCreate() {
  if (!canSave.value) return
  saving.value = true
  const { error } = await api.POST(ApiRoutes.Cards.create(props.projectId), {
    body: {
      columnId: columnId.value,
      title: title.value.trim(),
      description: description.value,
      type: cardType.value
    }
  })
  saving.value = false
  if (error) {
    toast.add({ title: 'Failed to create card', color: 'error' })
  } else {
    toast.add({ title: 'Card created', color: 'success' })
    emit('created')
    closeWithAnimation()
  }
}

function closeWithAnimation() {
  isOpen.value = false
  setTimeout(() => emit('close'), 200)
}
</script>

<template>
  <AppModal
    :open="isOpen"
    title="Create card"
    width="sm:max-w-lg"
    @update:open="closeWithAnimation"
    @close="closeWithAnimation"
  >
    <template #body>
      <div class="space-y-4 p-1">
        <div>
          <label class="block text-sm font-medium mb-1">Title *</label>
          <input
            v-model="title"
            class="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800 focus:outline-none focus:ring-2 focus:ring-primary"
            placeholder="Card title"
            @keydown.enter="handleCreate"
          />
        </div>
        <div>
          <label class="block text-sm font-medium mb-1">Description</label>
          <textarea
            v-model="description"
            class="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800 focus:outline-none focus:ring-2 focus:ring-primary min-h-[80px]"
            placeholder="Optional description"
          />
        </div>
        <div class="flex gap-4">
          <div class="flex-1">
            <label class="block text-sm font-medium mb-1">Type</label>
            <select
              v-model="cardType"
              class="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
            >
              <option :value="0">Task</option>
              <option :value="1">Bug</option>
              <option :value="2">Epic</option>
              <option :value="3">Spec</option>
              <option :value="4">Idea</option>
            </select>
          </div>
          <div class="flex-1">
            <label class="block text-sm font-medium mb-1">Column *</label>
            <select
              v-model="columnId"
              class="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
              :disabled="!!preselectedColumnId"
            >
              <option
                v-for="col in columns"
                :key="col.id"
                :value="col.id"
              >
                {{ col.name }}
              </option>
            </select>
          </div>
        </div>
      </div>
    </template>
    <template #footer>
      <div class="flex justify-end gap-3 w-full">
        <UButton variant="ghost" @click="closeWithAnimation">
          Cancel
        </UButton>
        <UButton
          :disabled="!canSave"
          :loading="saving"
          color="primary"
          @click="handleCreate"
        >
          Create
        </UButton>
      </div>
    </template>
  </AppModal>
</template>
```

- [ ] **Step 2: Wire CardCreateModal in board page**

In `board.vue`, add state and handler:

After `selectedCardId`:
```ts
const showCreateModal = ref(false)
const createColumnId = ref<string | null>(null)

function handleAddCard(columnId?: string) {
  createColumnId.value = columnId ?? null
  showCreateModal.value = true
}
```

Before `</template>` closing, add:
```vue
<CardCreateModal
  v-if="showCreateModal"
  :project-id="projectId"
  :columns="board.columns"
  :preselected-column-id="createColumnId"
  @close="showCreateModal = false"
  @created="board.fetchBoard(projectId)"
/>
```

And in the template area where `BoardView` is, emit `add-card`:
```vue
<BoardView
  :columns="board.columns"
  :cards-by-column="board.cardsByColumn"
  :project-id="projectId"
  class="hidden md:flex"
  @card-move="handleCardMove"
  @card-click="handleCardClick"
  @add-card="handleAddCard"
/>
```

- [ ] **Step 3: Verify typecheck passes**

Run: `pnpm typecheck`
Expected: no errors

- [ ] **Step 4: Commit**

```bash
git add app/components/board/CardCreateModal.vue app/pages/projects/[id]/board.vue
git commit -m "feat(board): add CardCreateModal with column picker"
```

---

### Task 5: Mobile accordion + filter panel

**Files:**
- Modify: `app/components/board/BoardMobileList.vue`

- [ ] **Step 1: Add accordion state and filter panel to BoardMobileList**

Replace script with expanded state + filter panel logic. Add after `archiveTargetCard`:

```ts
// Accordion state
const expandedColumns = ref<Set<string>>(new Set())

function toggleColumn(colId: string) {
  if (expandedColumns.value.has(colId)) {
    expandedColumns.value.delete(colId)
  } else {
    expandedColumns.value.add(colId)
  }
}

// Filter panel state
const showFilters = ref(false)
const mobileSearch = ref('')
const mobileType = ref<number | null>(null)
const mobileArchived = ref(false)
const mobileHideEmpty = ref(false)
```

And filtered cards for mobile (similar to per-column but global on mobile):

```ts
const filteredCardsByColumn = computed(() => {
  const result = new Map<string, components['schemas']['CardResponse'][]>()
  for (const [colId, cards] of props.cardsByColumn) {
    let filtered = cards
    if (mobileSearch.value) {
      const q = mobileSearch.value.toLowerCase()
      filtered = filtered.filter(c =>
        c.title.toLowerCase().includes(q) ||
        String(c.cardNumber).includes(q)
      )
    }
    if (mobileType.value !== null) {
      filtered = filtered.filter(c => c.type === mobileType.value)
    }
    if (!mobileArchived.value) {
      filtered = filtered.filter(c => !c.archivedAt)
    }
    result.set(colId, filtered)
  }
  return result
})

const filteredColumns = computed(() => {
  if (!mobileHideEmpty.value) return props.columns
  return props.columns.filter(c => (filteredCardsByColumn.value.get(c.id)?.length ?? 0) > 0)
})
```

- [ ] **Step 2: Update mobile template**

Replace template (keeping existing ConfirmDialog at bottom). Key changes:

1. **Global top bar** — search input + Filter button + + button
2. **Filter slide-out panel** — shown when `showFilters` is true, with Type dropdown + Archived checkbox + Hide empty checkbox
3. **Accordion columns** — each column section is collapsible

Template structure:

```vue
<template>
  <div>
    <!-- Global mobile bar -->
    <div class="flex items-center gap-2 px-4 py-2 border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900">
      <input
        v-model="mobileSearch"
        placeholder="Search cards..."
        class="flex-1 px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md"
      />
      <UButton variant="ghost" size="sm" @click="showFilters = !showFilters">
        Filter
      </UButton>
      <UButton size="sm" icon="i-lucide-plus" @click="emit('add-card')" />
    </div>

    <!-- Filter slide-out -->
    <div v-if="showFilters" class="flex items-center gap-3 px-4 py-2 bg-gray-50 dark:bg-gray-800 border-b">
      <select v-model="mobileType" class="text-xs px-2 py-1 border rounded">
        <option :value="null">All types</option>
        <option :value="0">Task</option>
        <option :value="1">Bug</option>
        <option :value="2">Epic</option>
        <option :value="3">Spec</option>
        <option :value="4">Idea</option>
      </select>
      <label class="flex items-center gap-1 text-xs">
        <input v-model="mobileArchived" type="checkbox" class="rounded" />
        Archived
      </label>
      <label class="flex items-center gap-1 text-xs">
        <input v-model="mobileHideEmpty" type="checkbox" class="rounded" />
        Hide empty
      </label>
    </div>

    <!-- Columns as accordion -->
    <div class="p-4 space-y-2">
      <div v-for="column in filteredColumns" :key="column.id" class="bg-white dark:bg-gray-800 rounded-lg border">
        <div
          class="flex items-center justify-between px-3 py-2 cursor-pointer"
          @click="toggleColumn(column.id)"
        >
          <div class="flex items-center gap-2">
            <div v-if="column.color" class="size-3 rounded-full" :style="{ backgroundColor: column.color }" />
            <h3 class="text-sm font-semibold">{{ column.name }}</h3>
            <span class="text-xs text-gray-400 bg-gray-100 dark:bg-gray-700 rounded px-1.5 py-0.5">
              {{ filteredCardsByColumn.get(column.id)?.length ?? 0 }}
            </span>
          </div>
          <span class="text-xs text-gray-400">{{ expandedColumns.has(column.id) ? '▼' : '▶' }}</span>
        </div>
        <div v-if="expandedColumns.has(column.id)" class="px-3 pb-3 space-y-2">
          <div
            v-for="card in filteredCardsByColumn.get(column.id) ?? []"
            :key="card.id"
            class="bg-gray-50 dark:bg-gray-700 rounded p-3 cursor-pointer"
            @click="emit('card-click', card)"
          >
            <div class="flex items-center gap-2">
              <UIcon :name="typeIcons[card.type] ?? 'i-lucide-square'" class="size-4 shrink-0 text-gray-400" />
              <span class="text-xs font-medium text-gray-500">#{{ card.cardNumber }}</span>
              <p class="text-sm font-medium truncate">{{ card.title }}</p>
              <span v-if="card.archivedAt" class="text-xs text-gray-400 shrink-0">archived</span>
            </div>
            <p v-if="card.description" class="text-xs text-gray-500 mt-1 line-clamp-2">
              {{ stripHtml(card.description) }}
            </p>
          </div>
        </div>
      </div>
    </div>

    <ConfirmDialog
      v-model:open="showArchiveConfirm"
      title="Archive card"
      :message="archiveTargetCard ? `Archive #${archiveTargetCard.cardNumber} ${archiveTargetCard.title}?` : ''"
      confirm-text="Archive"
      @confirm="confirmArchive"
    />
  </div>
</template>
```

- [ ] **Step 3: Verify typecheck passes**

Run: `pnpm typecheck`
Expected: no errors

- [ ] **Step 4: Verify existing tests still pass**

Run: `pnpm run test`
Expected: 54+ tests pass (existing + any new)

- [ ] **Step 5: Commit**

```bash
git add app/components/board/BoardMobileList.vue
git commit -m "feat(board): add mobile accordion columns and filter panel"
```

---

### Task 6: Wire board page — global filters + hide empty columns

**Files:**
- Modify: `app/pages/projects/[id]/board.vue`
- Modify: `app/components/board/BoardView.vue`
- Test: manual validation via browser

- [ ] **Step 1: Add BoardFilterBar to board page**

In `board.vue`, add before BoardView:

```vue
<BoardFilterBar
  v-model="board.boardFilters"
  class="hidden md:flex"
  @add-card="handleAddCard()"
/>
```

Import it:
```ts
import BoardFilterBar from '~/components/board/BoardFilterBar.vue'
```

- [ ] **Step 2: Wire visibleColumns in BoardView**

In `board.vue`, replace `board.columns` with `board.visibleColumns` for BoardView:

```vue
<BoardView
  :columns="board.visibleColumns"
  ...
/>
```

Also wire `@add-card` from BoardMobileList:
```vue
<BoardMobileList
  :columns="board.visibleColumns"
  :cards-by-column="board.cardsByColumn"
  :project-id="projectId"
  class="md:hidden"
  @card-click="handleCardClick"
  @add-card="handleAddCard"
/>
```

Add `add-card` emit to BoardMobileList props:
```ts
const emit = defineEmits<{
  'card-click': [card: CardResponse]
  'add-card': []
}>()
```

- [ ] **Step 3: Re-fetch board on filter changes with debounce**

Add a debounced watch on boardFilters to re-fetch on type/archived changes. But text search is client-side. Only re-fetch when `includeArchived` or `type` changes.

Actually, since type and archived are handled server-side in `fetchBoard`, and text search is client-side, we should re-fetch when filters change. Add in `board.vue`:

```ts
watch(() => [board.boardFilters.type, board.boardFilters.includeArchived], () => {
  board.fetchBoard(projectId)
}, { deep: false })
```

- [ ] **Step 4: Verify typecheck passes**

Run: `pnpm typecheck`
Expected: no errors

- [ ] **Step 5: Commit**

```bash
git add app/pages/projects/[id]/board.vue app/components/board/BoardView.vue app/components/board/BoardMobileList.vue
git commit -m "feat(board): wire board page with global filters and empty column toggle"
```

---

### Task 7: Tests for board store filters + mobile

**Files:**
- Create: `app/stores/__tests__/board.test.ts`
- Create or modify: `app/components/board/__tests__/BoardMobileList.test.ts` (extend with accordion tests)

- [ ] **Step 1: Write board store filter test**

Create `app/stores/__tests__/board.test.ts`:

```ts
import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useBoardStore } from '~/stores/board'

describe('BoardStore filters', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('has default filter state', () => {
    const store = useBoardStore()
    expect(store.boardFilters.search).toBe('')
    expect(store.boardFilters.type).toBe(null)
    expect(store.boardFilters.includeArchived).toBe(false)
    expect(store.boardFilters.hideEmptyColumns).toBe(false)
  })

  it('visibleColumns returns all columns by default', () => {
    const store = useBoardStore()
    store.columns = [
      { id: 'c1', name: 'Todo', position: 0, wipLimit: null, color: null },
      { id: 'c2', name: 'Done', position: 1, wipLimit: null, color: null }
    ]
    store.cardsByColumn = new Map([
      ['c1', [{ id: 'card1', columnId: 'c1', title: 'Task', type: 0, cardNumber: 1, position: 0, version: 1, dueAt: null, parentCardId: null, projectId: 'p1', archivedAt: null, assignees: [], watchers: [], createdAt: '', updatedAt: '', movedAt: '', description: '' }]],
      ['c2', []]
    ])
    expect(store.visibleColumns.length).toBe(2)
  })

  it('visibleColumns hides empty columns when set', () => {
    const store = useBoardStore()
    store.columns = [
      { id: 'c1', name: 'Todo', position: 0, wipLimit: null, color: null },
      { id: 'c2', name: 'Done', position: 1, wipLimit: null, color: null }
    ]
    store.cardsByColumn = new Map([
      ['c1', [{ id: 'card1', columnId: 'c1', title: 'Task', type: 0, cardNumber: 1, position: 0, version: 1, dueAt: null, parentCardId: null, projectId: 'p1', archivedAt: null, assignees: [], watchers: [], createdAt: '', updatedAt: '', movedAt: '', description: '' }]],
      ['c2', []]
    ])
    store.boardFilters.hideEmptyColumns = true
    expect(store.visibleColumns.length).toBe(1)
    expect(store.visibleColumns[0].id).toBe('c1')
  })
})
```

- [ ] **Step 2: Extend BoardMobileList test with accordion**

Add to `app/components/board/__tests__/BoardMobileList.test.ts`:

```ts
it('renders filter panel toggle', async () => {
  const wrapper = await mountSuspended(BoardMobileList, {
    props: { columns: [], cardsByColumn: new Map(), projectId: 'p1' }
  })
  expect(wrapper.text()).toContain('Filter')
})

it('renders accordion column headers', async () => {
  const columns = [makeColumn('col1', 'Backlog')]
  const cardsByColumn = new Map([['col1', [makeCard('c1', 'col1', 'My Task')]]])
  const wrapper = await mountSuspended(BoardMobileList, {
    props: { columns, cardsByColumn, projectId: 'p1' }
  })
  expect(wrapper.text()).toContain('Backlog')
  // Arrow indicates collapsed state
  expect(wrapper.text()).toContain('▶')
})
```

- [ ] **Step 3: Verify all tests pass**

Run: `pnpm run test`
Expected: 56+ tests pass

- [ ] **Step 4: Commit**

```bash
git add app/stores/__tests__/board.test.ts app/components/board/__tests__/BoardMobileList.test.ts
git commit -m "test(board): add store filter tests and mobile accordion tests"
```

---

## Spec Coverage Checklist

| Spec requirement | Task |
|---|---|
| Global search input | Task 2 (BoardFilterBar) |
| Global type dropdown | Task 2 |
| Global archived toggle | Task 2 |
| Global hide empty columns | Task 2 + Task 1 (visibleColumns) |
| Global + Card button | Task 2 + Task 4 (CardCreateModal) |
| Per-column Type filter | Task 3 (ColumnHeader) |
| Per-column Arch toggle | Task 3 |
| Per-column ⋮ menu | Task 3 (deferred actions, only stub) |
| Per-column + Add (preselected) | Task 3 + Task 4 |
| Per-column inline search | Task 3 (filter-row slot in BoardColumn) |
| Mobile: filter slide-out panel | Task 5 |
| Mobile: accordion columns | Task 5 |
| Mobile: global + button | Task 5 + Task 4 |
| + Card: 3 entry points, 1 form | Task 4 (preselectedColumnId) |
| Board store filter state | Task 1 |
| fetchBoard passes includeArchived + type | Task 1 |
| Filter persistence on re-fetch | Task 1 (fetchBoard reads current filters) |
| Visible/hide empty columns logic | Task 1 (visibleColumns computed) |

## Self-Review

- No placeholders (TBD/TODO): ✅
- Types consistent: BoardFilters interface used in store and components, CardType enum values match backend
- No placeholder references to undefined types
- Tasks produce testable software independently: each task compiles, passes typecheck, and either adds store logic, a component, or wires them together
- Empty column visibility is both global (hideEmptyColumns toggle) and automatic in BoardColumn (filteredCards computed skips archived cards)
- Mobile: columns start collapsed, user taps to expand
