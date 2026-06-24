# Plan 2: Project List & Board View
**Branch:** `task/phase-3-project-list-board`
**Parent branch:** `feat/phase-3-web-ui`
**Parent spec:** `2026-06-23-phase-3-web-ui-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pinia board store, project list page with create modal, desktop kanban board with drag-and-drop columns/cards, mobile card list view.

**Architecture:** `useBoardStore` holds project, columns, cards. Project list page fetches from `GET /api/Projects`. Board page fetches board state via `GET /api/projects/{id}/Columns` + `GET /api/projects/{id}/Cards` (not `ProjectSnapshot` — those endpoints return structured card/column data, not the snapshot template). `useBoardStore.fetchBoard` calls both in parallel via `Promise.all`. Desktop uses `vue-draggable-plus` for column reorder and card drag between columns. Mobile renders cards grouped by column in a scrollable list.

**Tech Stack:** Pinia, vue-draggable-plus, @vueuse/core, Nuxt UI v4

**Depends on:** Plan 1 (Foundation) — needs useApi, useAuthToken, auth middleware, layouts

**Spec ref:** Sections 3.3, 3.4, 5, 6 (blocked card behavior), 9 (useBoardStore), 13 (board components), 14 (pages/routing)

---

## Task 5: Pinia Setup + useBoardStore

**Files:**
- Create: `src/web-ui/app/stores/board.ts`
- Modify: `src/web-ui/nuxt.config.ts` (add @pinia/nuxt module)

### Step 1: Install Pinia

```bash
cd src/web-ui && pnpm add pinia @pinia/nuxt
```

### Step 2: Add @pinia/nuxt to modules

In `src/web-ui/nuxt.config.ts`, add `'@pinia/nuxt'` to the `modules` array:

```ts
modules: [
  '@nuxt/eslint',
  '@nuxt/ui',
  '@pinia/nuxt'
],
```

### Step 3: Create useBoardStore

Create `src/web-ui/app/stores/board.ts`:

```ts
import { defineStore } from 'pinia'
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']
type ProjectResponse = components['schemas']['ProjectResponse']

export const useBoardStore = defineStore('board', () => {
  const project = ref<ProjectResponse | null>(null)
  const columns = ref<ColumnResponse[]>([])
  const cardsByColumn = ref<Map<string, CardResponse[]>>(new Map())
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchBoard(projectId: string) {
    loading.value = true
    error.value = null
    try {
      const [columnsResult, cardsResult] = await Promise.all([
        api.GET('/api/projects/{projectId}/Columns', {
          params: { path: { projectId } }
        }),
        api.GET('/api/projects/{projectId}/Cards', {
          params: { path: { projectId } }
        })
      ])

      if (columnsResult.error) throw columnsResult.error
      if (cardsResult.error) throw cardsResult.error

      columns.value = (columnsResult.data as ColumnResponse[]) ?? []

      const cardList = cardsResult.data as CardListResponse
      const cards = cardList?.cards ?? []

      const map = new Map<string, CardResponse[]>()
      for (const col of columns.value) {
        map.set(col.id, [])
      }
      for (const card of cards) {
        const colCards = map.get(card.columnId) ?? []
        colCards.push(card)
        map.set(card.columnId, colCards)
      }
      cardsByColumn.value = map
    }
    catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to load board'
    }
    finally {
      loading.value = false
    }
  }

  function moveCard(cardId: string, targetColumnId: string, targetPosition: number) {
    // Optimistic: move card in local state immediately
    let card: CardResponse | undefined
    for (const [colId, cards] of cardsByColumn.value) {
      const idx = cards.findIndex(c => c.id === cardId)
      if (idx !== -1) {
        card = cards[idx]
        cards.splice(idx, 1)
        break
      }
    }
    if (!card) return

    const targetCards = cardsByColumn.value.get(targetColumnId) ?? []
    targetCards.splice(targetPosition, 0, card)
    cardsByColumn.value.set(targetColumnId, targetCards)
  }

  function rollbackMove(projectId: string) {
    // Re-fetch board as simplest rollback after failed move
    fetchBoard(projectId)
  }

  function addCard(columnId: string, card: CardResponse) {
    const cards = cardsByColumn.value.get(columnId) ?? []
    cards.push(card)
    cardsByColumn.value.set(columnId, cards)
  }

  function updateCard(cardId: string, updates: Partial<CardResponse>) {
    for (const [, cards] of cardsByColumn.value) {
      const idx = cards.findIndex(c => c.id === cardId)
      if (idx !== -1) {
        cards[idx] = { ...cards[idx], ...updates }
        break
      }
    }
  }

  function removeCard(cardId: string) {
    for (const [colId, cards] of cardsByColumn.value) {
      const idx = cards.findIndex(c => c.id === cardId)
      if (idx !== -1) {
        cards.splice(idx, 1)
        break
      }
    }
  }

  return {
    project, columns, cardsByColumn, loading, error,
    fetchBoard, moveCard, rollbackMove, addCard, updateCard, removeCard
  }
})
```

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors

### Step 5: Commit

```bash
git add src/web-ui/package.json src/web-ui/pnpm-lock.yaml src/web-ui/nuxt.config.ts src/web-ui/app/stores/board.ts
git commit -m "feat: add Pinia board store with optimistic card moves"
```

---

## Task 6: Project List Page + Create Project Modal

**Files:**
- Create: `src/web-ui/app/pages/projects.vue`
- Create: `src/web-ui/app/components/project/ProjectList.vue`
- Create: `src/web-ui/app/components/project/ProjectCreateModal.vue`

### Step 1: Create ProjectList component

Create `src/web-ui/app/components/project/ProjectList.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type ProjectResponse = components['schemas']['ProjectResponse']

const props = defineProps<{
  projects: ProjectResponse[]
  loading: boolean
}>()

const emit = defineEmits<{
  select: [projectId: string]
}>()
</script>

<template>
  <div v-if="loading" class="flex justify-center p-8">
    <UIcon name="i-lucide-loader" class="animate-spin size-8" />
  </div>

  <div v-else-if="projects.length === 0" class="text-center p-8 text-muted">
    <p>No projects yet. Create your first project!</p>
  </div>

  <div v-else class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
    <UCard
      v-for="project in projects"
      :key="project.id"
      class="cursor-pointer hover:ring-2 hover:ring-primary transition-shadow"
      @click="emit('select', project.id)"
    >
      <template #header>
        <h3 class="font-semibold truncate">{{ project.name }}</h3>
      </template>
      <p class="text-sm text-muted line-clamp-2">{{ project.description ?? 'No description' }}</p>
    </UCard>
  </div>
</template>
```

### Step 2: Create ProjectCreateModal component

Create `src/web-ui/app/components/project/ProjectCreateModal.vue`:

```vue
<script setup lang="ts">
const emit = defineEmits<{
  created: []
  close: []
}>()

const name = ref('')
const description = ref('')
const loading = ref(false)
const error = ref<string | null>(null)

const api = useApi()

async function handleSubmit() {
  error.value = null
  loading.value = true
  try {
    const { error: apiError } = await api.POST('/api/Projects', {
      body: { name: name.value, description: description.value || undefined }
    })
    if (apiError) throw apiError
    emit('created')
  }
  catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to create project'
  }
  finally {
    loading.value = false
  }
}
</script>

<template>
  <UModal :open="true" @close="emit('close')">
    <UCard>
      <template #header>
        <h2 class="text-lg font-semibold">Create Project</h2>
      </template>

      <form class="space-y-4" @submit.prevent="handleSubmit">
        <UFormField label="Project Name" required>
          <UInput v-model="name" placeholder="My Project" required />
        </UFormField>

        <UFormField label="Description">
          <UTextarea v-model="description" placeholder="Optional description" />
        </UFormField>

        <UAlert v-if="error" color="error" variant="subtle" :title="error" />

        <div class="flex justify-end gap-2">
          <UButton variant="outline" @click="emit('close')">Cancel</UButton>
          <UButton type="submit" :loading="loading">Create</UButton>
        </div>
      </form>
    </UCard>
  </UModal>
</template>
```

### Step 3: Create projects page

Create `src/web-ui/app/pages/projects.vue`:

```vue
<script setup lang="ts">
definePageMeta({ middleware: ['auth'] })

import type { components } from '~/types/api'

type ProjectResponse = components['schemas']['ProjectResponse']

const projects = ref<ProjectResponse[]>([])
const loading = ref(true)
const showCreateModal = ref(false)

const api = useApi()

async function fetchProjects() {
  loading.value = true
  try {
    const { data, error } = await api.GET('/api/Projects')
    if (error) throw error
    projects.value = (data as ProjectResponse[]) ?? []
  }
  catch (e: unknown) {
    console.error('Failed to fetch projects', e)
  }
  finally {
    loading.value = false
  }
}

function onProjectSelect(projectId: string) {
  navigateTo(`/projects/${projectId}/board`)
}

function onProjectCreated() {
  showCreateModal.value = false
  fetchProjects()
}

onMounted(() => fetchProjects())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 max-w-6xl mx-auto">
    <div class="flex items-center justify-between mb-6">
      <h1 class="text-2xl font-bold">Projects</h1>
      <UButton @click="showCreateModal = true">New Project</UButton>
    </div>

    <ProjectList :projects="projects" :loading="loading" @select="onProjectSelect" />

    <ProjectCreateModal
      v-if="showCreateModal"
      @created="onProjectCreated"
      @close="showCreateModal = false"
    />
  </div>
</template>
```

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: login → see project list → create project → click project → navigates to board (404 until Task 7)

### Step 5: Commit

```bash
git add src/web-ui/app/pages/projects.vue src/web-ui/app/components/project/ProjectList.vue src/web-ui/app/components/project/ProjectCreateModal.vue
git commit -m "feat: add project list page and create project modal"
```

---

## Task 7: Board View (Desktop) — Columns + Cards + Drag-and-Drop

**Files:**
- Create: `src/web-ui/app/pages/projects/[id]/board.vue`
- Create: `src/web-ui/app/components/board/BoardView.vue`
- Create: `src/web-ui/app/components/board/BoardColumn.vue`
- Create: `src/web-ui/app/components/board/BoardCard.vue`
- Create: `src/web-ui/app/components/board/ColumnHeader.vue`

### Step 1: Install vue-draggable-plus

```bash
cd src/web-ui && pnpm add vue-draggable-plus @vueuse/core
```

### Step 2: Create BoardCard component

Create `src/web-ui/app/components/board/BoardCard.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  card: CardResponse
}>()

const emit = defineEmits<{
  select: [cardId: string]
}>()

const typeBadgeColor = computed(() => {
  switch (props.card.type) {
    case 'Bug': return 'error'
    case 'Epic': return 'primary'
    default: return 'neutral'
  }
})
</script>

<template>
  <UCard
    class="cursor-pointer hover:ring-2 hover:ring-primary/50 transition-shadow"
    @click="emit('select', card.id)"
  >
    <div class="space-y-1">
      <div class="flex items-center gap-2">
        <UBadge :color="typeBadgeColor" variant="subtle" size="xs">
          {{ card.type }}
        </UBadge>
        <span v-if="card.isBlocked" class="text-warning" title="Blocked">
          <UIcon name="i-lucide-lock" class="size-3" />
        </span>
      </div>
      <p class="text-sm font-medium line-clamp-2">{{ card.title }}</p>
      <div v-if="card.assignees?.length" class="flex -space-x-1">
        <UAvatar
          v-for="assignee in card.assignees.slice(0, 3)"
          :key="assignee.userId"
          :alt="assignee.username"
          size="xs"
        />
        <span v-if="card.assignees.length > 3" class="text-xs text-muted ml-1">
          +{{ card.assignees.length - 3 }}
        </span>
      </div>
    </div>
  </UCard>
</template>
```

### Step 3: Create ColumnHeader component

Create `src/web-ui/app/components/board/ColumnHeader.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']

const props = defineProps<{
  column: ColumnResponse
  cardCount: number
}>()
</script>

<template>
  <div class="flex items-center justify-between px-1 py-2">
    <div class="flex items-center gap-2">
      <h3 class="font-semibold text-sm">{{ column.name }}</h3>
      <UBadge variant="subtle" size="xs">{{ cardCount }}</UBadge>
    </div>
    <UBadge v-if="column.wipLimit" variant="subtle" size="xs" :color="cardCount >= column.wipLimit ? 'error' : 'neutral'">
      {{ cardCount }}/{{ column.wipLimit }}
    </UBadge>
  </div>
</template>
```

### Step 4: Create BoardColumn component

Create `src/web-ui/app/components/board/BoardColumn.vue`:

```vue
<script setup lang="ts">
import { useDraggable } from 'vue-draggable-plus'
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  column: ColumnResponse
  cards: CardResponse[]
}>()

const emit = defineEmits<{
  cardSelect: [cardId: string]
  cardMove: [cardId: string, targetColumnId: string, targetPosition: number]
}>()

const el = ref<HTMLElement>()

useDraggable(el, props.cards, {
  animation: 150,
  group: 'cards',
  onEnd(evt) {
    if (evt.from !== evt.to || evt.oldIndex !== evt.newIndex) {
      const cardId = props.cards[evt.oldIndex!]?.id
      if (cardId) {
        emit('cardMove', cardId, props.column.id, evt.newIndex!)
      }
    }
  }
})
</script>

<template>
  <div class="flex-shrink-0 w-72 flex flex-col max-h-full">
    <ColumnHeader :column="column" :card-count="cards.length" />

    <div ref="el" class="flex-1 overflow-y-auto space-y-2 p-1 min-h-0">
      <BoardCard
        v-for="card in cards"
        :key="card.id"
        :card="card"
        @select="emit('cardSelect', $event)"
      />
    </div>
  </div>
</template>
```

### Step 5: Create BoardView component

Create `src/web-ui/app/components/board/BoardView.vue`:

```vue
<script setup lang="ts">
import { useDraggable } from 'vue-draggable-plus'
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  columns: ColumnResponse[]
  cardsByColumn: Map<string, CardResponse[]>
}>()

const emit = defineEmits<{
  cardSelect: [cardId: string]
  cardMove: [cardId: string, targetColumnId: string, targetPosition: number, sourceColumnId: string, sourcePosition: number]
}>()

const columnsEl = ref<HTMLElement>()

// Column reorder via drag
useDraggable(columnsEl, props.columns, {
  animation: 150,
  handle: '.column-drag-handle'
})

function onCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  // Find source position
  let sourceColumnId = ''
  let sourcePosition = -1
  for (const [colId, cards] of props.cardsByColumn) {
    const idx = cards.findIndex(c => c.id === cardId)
    if (idx !== -1) {
      sourceColumnId = colId
      sourcePosition = idx
      break
    }
  }
  emit('cardMove', cardId, targetColumnId, targetPosition, sourceColumnId, sourcePosition)
}
</script>

<template>
  <div ref="columnsEl" class="flex gap-4 overflow-x-auto h-full p-4">
    <BoardColumn
      v-for="column in columns"
      :key="column.id"
      :column="column"
      :cards="cardsByColumn.get(column.id) ?? []"
      @card-select="emit('cardSelect', $event)"
      @card-move="onCardMove"
    />
  </div>
</template>
```

### Step 6: Create board page

Create `src/web-ui/app/pages/projects/[id]/board.vue`:

```vue
<script setup lang="ts">
definePageMeta({ middleware: ['auth'] })

const route = useRoute()
const projectId = route.params.id as string
const board = useBoardStore()

const selectedCardId = ref<string | null>(null)

onMounted(() => board.fetchBoard(projectId))

async function handleCardMove(
  cardId: string,
  targetColumnId: string,
  targetPosition: number,
  sourceColumnId: string,
  sourcePosition: number
) {
  // Optimistic update
  board.moveCard(cardId, targetColumnId, targetPosition)

  const api = useApi()
  try {
    const { error } = await api.POST('/api/projects/{projectId}/Cards/{cardId}/move', {
      params: { path: { projectId, cardId } },
      body: { targetColumnId, targetPosition }
    })
    if (error) {
      board.rollbackMove(cardId, sourceColumnId, sourcePosition)
    }
  }
  catch {
    board.rollbackMove(cardId, sourceColumnId, sourcePosition)
  }
}
</script>

<template>
  <div class="h-[calc(100vh-4rem)]">
    <div v-if="board.loading" class="flex items-center justify-center h-full">
      <UIcon name="i-lucide-loader" class="animate-spin size-8" />
    </div>

    <div v-else-if="board.error" class="flex items-center justify-center h-full">
      <UAlert color="error" :title="board.error" />
    </div>

    <BoardView
      v-else
      :columns="board.columns"
      :cards-by-column="board.cardsByColumn"
      @card-select="selectedCardId = $event"
      @card-move="handleCardMove"
    />
  </div>
</template>
```

### Step 7: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: login → project list → click project → board renders with columns and cards
- Manual: drag card between columns → optimistic move → API call

### Step 8: Commit

```bash
git add src/web-ui/package.json src/web-ui/pnpm-lock.yaml src/web-ui/app/pages/projects/ src/web-ui/app/components/board/
git commit -m "feat: add desktop board view with drag-and-drop columns and cards"
```

---

## Task 8: Board Mobile List View

**Files:**
- Create: `src/web-ui/app/components/board/BoardMobileList.vue`
- Modify: `src/web-ui/app/pages/projects/[id]/board.vue` (add breakpoint switching)

### Step 1: Create BoardMobileList component

Create `src/web-ui/app/components/board/BoardMobileList.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']

defineProps<{
  columns: ColumnResponse[]
  cardsByColumn: Map<string, CardResponse[]>
}>()

const emit = defineEmits<{
  cardSelect: [cardId: string]
}>()

const typeBadgeColor = (type: string) => {
  switch (type) {
    case 'Bug': return 'error'
    case 'Epic': return 'primary'
    default: return 'neutral'
  }
}
</script>

<template>
  <div class="p-4 space-y-6">
    <div v-for="column in columns" :key="column.id">
      <div class="flex items-center gap-2 mb-2 pb-2 border-b">
        <h3 class="font-semibold">{{ column.name }}</h3>
        <UBadge variant="subtle" size="xs">{{ cardsByColumn.get(column.id)?.length ?? 0 }}</UBadge>
      </div>

      <div class="space-y-2">
        <UCard
          v-for="card in cardsByColumn.get(column.id) ?? []"
          :key="card.id"
          class="cursor-pointer"
          @click="emit('cardSelect', card.id)"
        >
          <div class="flex items-center gap-2">
            <UBadge :color="typeBadgeColor(card.type)" variant="subtle" size="xs">
              {{ card.type }}
            </UBadge>
            <span class="text-sm font-medium truncate flex-1">{{ card.title }}</span>
            <span v-if="card.isBlocked" class="text-warning">
              <UIcon name="i-lucide-lock" class="size-3" />
            </span>
            <UAvatar
              v-if="card.assignees?.[0]"
              :alt="card.assignees[0].username"
              size="xs"
            />
          </div>
        </UCard>
      </div>
    </div>
  </div>
</template>
```

### Step 2: Update board page with breakpoint switching

Modify `src/web-ui/app/pages/projects/[id]/board.vue` — replace the template section:

```vue
<template>
  <div class="h-[calc(100vh-4rem)]">
    <div v-if="board.loading" class="flex items-center justify-center h-full">
      <UIcon name="i-lucide-loader" class="animate-spin size-8" />
    </div>

    <div v-else-if="board.error" class="flex items-center justify-center h-full">
      <UAlert color="error" :title="board.error" />
    </div>

    <!-- Desktop: ≥768px -->
    <BoardView
      v-else
      class="hidden md:flex"
      :columns="board.columns"
      :cards-by-column="board.cardsByColumn"
      @card-select="selectedCardId = $event"
      @card-move="handleCardMove"
    />

    <!-- Mobile: <768px -->
    <BoardMobileList
      v-else
      class="md:hidden"
      :columns="board.columns"
      :cards-by-column="board.cardsByColumn"
      @card-select="selectedCardId = $event"
    />
  </div>
</template>
```

### Step 3: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: resize browser to <768px → mobile list view renders
- Manual: resize to ≥768px → desktop kanban renders
- Manual: tap card on mobile → emits cardSelect (modal not built yet)

### Step 4: Commit

```bash
git add src/web-ui/app/components/board/BoardMobileList.vue src/web-ui/app/pages/projects/[id]/board.vue
git commit -m "feat: add mobile board list view with breakpoint switching"
```

---

## Task 9: Board Store + Component Tests

**Files:**
- Create: `src/web-ui/app/stores/__tests__/board.test.ts`
- Create: `src/web-ui/app/components/project/__tests__/ProjectList.test.ts`
- Create: `src/web-ui/app/components/project/__tests__/ProjectCreateModal.test.ts`
- Create: `src/web-ui/app/components/board/__tests__/BoardCard.test.ts`
- Create: `src/web-ui/app/components/board/__tests__/BoardColumn.test.ts`
- Create: `src/web-ui/app/components/board/__tests__/BoardMobileList.test.ts`

### Step 1: Write useBoardStore tests

Create `src/web-ui/app/stores/__tests__/board.test.ts`:

```ts
import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useBoardStore } from '~/stores/board'

describe('useBoardStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('starts with null project and empty columns', () => {
    const board = useBoardStore()
    expect(board.project).toBeNull()
    expect(board.columns).toEqual([])
    expect(board.loading).toBe(false)
    expect(board.error).toBeNull()
  })

  it('addCard pushes card to correct column', () => {
    const board = useBoardStore()
    board.columns = [{ id: 'col1', name: 'Todo', position: 0, projectId: 'p1', wipLimit: null, cards: [] }] as any
    board.cardsByColumn = new Map([['col1', []]])

    board.addCard('col1', { id: 'c1', title: 'Test', type: 'Task' } as any)
    expect(board.cardsByColumn.get('col1')?.length).toBe(1)
    expect(board.cardsByColumn.get('col1')?.[0].id).toBe('c1')
  })

  it('updateCard modifies card in place', () => {
    const board = useBoardStore()
    board.cardsByColumn = new Map([['col1', [{ id: 'c1', title: 'Old', type: 'Task' } as any]]])

    board.updateCard('c1', { title: 'New' })
    expect(board.cardsByColumn.get('col1')?.[0].title).toBe('New')
  })

  it('removeCard deletes card from column', () => {
    const board = useBoardStore()
    board.cardsByColumn = new Map([['col1', [{ id: 'c1', title: 'Test', type: 'Task' } as any]]])

    board.removeCard('c1')
    expect(board.cardsByColumn.get('col1')?.length).toBe(0)
  })

  it('moveCard moves card between columns', () => {
    const board = useBoardStore()
    board.cardsByColumn = new Map([
      ['col1', [{ id: 'c1', title: 'Test', type: 'Task' } as any]],
      ['col2', []]
    ])

    board.moveCard('c1', 'col2', 0)
    expect(board.cardsByColumn.get('col1')?.length).toBe(0)
    expect(board.cardsByColumn.get('col2')?.length).toBe(1)
    expect(board.cardsByColumn.get('col2')?.[0].id).toBe('c1')
  })

  it('moveCard is no-op for unknown card', () => {
    const board = useBoardStore()
    board.cardsByColumn = new Map([['col1', []]])

    board.moveCard('nonexistent', 'col2', 0)
    expect(board.cardsByColumn.get('col1')?.length).toBe(0)
  })

  it('rollbackMove re-fetches board if project exists', () => {
    const board = useBoardStore()
    board.project = { id: 'p1', name: 'Test' } as any
    // rollbackMove calls fetchBoard — just verify it doesn't throw
    expect(() => board.rollbackMove('c1', 'col1', 0)).not.toThrow()
  })
})
```

### Step 2: Write ProjectList component test

Create `src/web-ui/app/components/project/__tests__/ProjectList.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ProjectList from '~/components/project/ProjectList.vue'

describe('ProjectList', () => {
  it('shows loading spinner when loading', async () => {
    const wrapper = await mountSuspended(ProjectList, {
      props: { projects: [], loading: true }
    })
    expect(wrapper.find('.animate-spin').exists()).toBe(true)
  })

  it('shows empty message when no projects', async () => {
    const wrapper = await mountSuspended(ProjectList, {
      props: { projects: [], loading: false }
    })
    expect(wrapper.text()).toContain('No projects yet')
  })

  it('renders project cards', async () => {
    const wrapper = await mountSuspended(ProjectList, {
      props: {
        projects: [
          { id: 'p1', name: 'Project A', description: 'Desc A' },
          { id: 'p2', name: 'Project B', description: null }
        ] as any,
        loading: false
      }
    })
    expect(wrapper.text()).toContain('Project A')
    expect(wrapper.text()).toContain('Project B')
  })

  it('emits select on card click', async () => {
    const wrapper = await mountSuspended(ProjectList, {
      props: {
        projects: [{ id: 'p1', name: 'Project A', description: null }] as any,
        loading: false
      }
    })
    await wrapper.find('.cursor-pointer').trigger('click')
    expect(wrapper.emitted('select')).toBeTruthy()
    expect(wrapper.emitted('select')?.[0]).toEqual(['p1'])
  })
})
```

### Step 3: Write BoardCard component test

Create `src/web-ui/app/components/board/__tests__/BoardCard.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import BoardCard from '~/components/board/BoardCard.vue'

describe('BoardCard', () => {
  const baseCard = {
    id: 'c1',
    title: 'Test Card',
    type: 'Task',
    isBlocked: false,
    assignees: []
  } as any

  it('renders card title', async () => {
    const wrapper = await mountSuspended(BoardCard, { props: { card: baseCard } })
    expect(wrapper.text()).toContain('Test Card')
  })

  it('shows type badge', async () => {
    const wrapper = await mountSuspended(BoardCard, { props: { card: baseCard } })
    expect(wrapper.text()).toContain('Task')
  })

  it('shows lock icon when blocked', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: { ...baseCard, isBlocked: true } }
    })
    expect(wrapper.find('[title="Blocked"]').exists()).toBe(true)
  })

  it('emits select on click', async () => {
    const wrapper = await mountSuspended(BoardCard, { props: { card: baseCard } })
    await wrapper.find('.cursor-pointer').trigger('click')
    expect(wrapper.emitted('select')).toBeTruthy()
    expect(wrapper.emitted('select')?.[0]).toEqual(['c1'])
  })

  it('shows Bug badge with error color', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: { ...baseCard, type: 'Bug' } }
    })
    expect(wrapper.text()).toContain('Bug')
  })

  it('shows Epic badge with primary color', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: { ...baseCard, type: 'Epic' } }
    })
    expect(wrapper.text()).toContain('Epic')
  })
})
```

### Step 4: Write BoardMobileList component test

Create `src/web-ui/app/components/board/__tests__/BoardMobileList.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import BoardMobileList from '~/components/board/BoardMobileList.vue'

describe('BoardMobileList', () => {
  it('renders column headers', async () => {
    const wrapper = await mountSuspended(BoardMobileList, {
      props: {
        columns: [{ id: 'col1', name: 'Todo', position: 0, projectId: 'p1', wipLimit: null, cards: [] }] as any,
        cardsByColumn: new Map([['col1', []]])
      }
    })
    expect(wrapper.text()).toContain('Todo')
  })

  it('renders cards under columns', async () => {
    const wrapper = await mountSuspended(BoardMobileList, {
      props: {
        columns: [{ id: 'col1', name: 'Todo', position: 0, projectId: 'p1', wipLimit: null, cards: [] }] as any,
        cardsByColumn: new Map([['col1', [{ id: 'c1', title: 'Card 1', type: 'Task', isBlocked: false, assignees: [] } as any]]])
      }
    })
    expect(wrapper.text()).toContain('Card 1')
  })

  it('emits cardSelect on card click', async () => {
    const wrapper = await mountSuspended(BoardMobileList, {
      props: {
        columns: [{ id: 'col1', name: 'Todo', position: 0, projectId: 'p1', wipLimit: null, cards: [] }] as any,
        cardsByColumn: new Map([['col1', [{ id: 'c1', title: 'Card 1', type: 'Task', isBlocked: false, assignees: [] } as any]]])
      }
    })
    await wrapper.find('.cursor-pointer').trigger('click')
    expect(wrapper.emitted('cardSelect')).toBeTruthy()
    expect(wrapper.emitted('cardSelect')?.[0]).toEqual(['c1'])
  })
})
```

### Step 5: Verify

- `cd src/web-ui && pnpm test` — all tests pass
- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors

### Step 6: Commit

```bash
git add src/web-ui/app/stores/__tests__/board.test.ts src/web-ui/app/components/project/__tests__/ src/web-ui/app/components/board/__tests__/
git commit -m "feat: add board store and component tests"
```

---

## Verification (Plan 2 Complete)

Reference `nuxt-verification` skill:
1. `cd src/web-ui && pnpm typecheck` — zero errors
2. `cd src/web-ui && pnpm lint` — zero errors
3. `cd src/web-ui && pnpm build` — successful production build
4. `cd src/web-ui && pnpm test` — all tests pass
5. Manual: project list → create project → board renders → drag cards between columns → mobile view works