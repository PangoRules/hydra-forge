# Plan 6: Polish & Hardening — Keyboard, Errors, Blocked Cards, Archive, ARIA, Tablet, PWA
**Branch:** `task/phase-3-polish-hardening`
**Parent branch:** `feat/phase-3-web-ui`
**Parent spec:** `2026-06-23-phase-3-web-ui-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keyboard shortcuts with overlay, error toast system with correlationId copy, blocked card indicators + move warning modal, archive card with dependents warning, ARIA labels + focus management + color contrast, tablet responsive pass, PWA manifest.

**Architecture:** `useKeyboard` composable registers shortcuts scoped to active component. `useErrorToast` wraps Nuxt UI toast with ProblemDetails parsing. Blocked card indicators show on `BoardCard`; move warning modal intercepts 409. Archive modal shows dependents list. ARIA pass adds labels, roles, focus traps to modals. Tablet pass adjusts sidebar (collapsible) and board (compact columns). PWA via `@vite-pwa/nuxt`.

**Tech Stack:** Nuxt UI toast, @vite-pwa/nuxt

**Depends on:** All previous plans — this is the final hardening pass

**Spec ref:** Sections 5 (tablet), 6 (blocked cards, archive), 11 (error handling), 12 (keyboard shortcuts), 16 (tablet responsive), 17 (PWA)

---

## Task 21: Keyboard Shortcuts + Overlay

**Files:**
- Create: `src/web-ui/app/composables/useKeyboard.ts`
- Create: `src/web-ui/app/components/shared/KeyboardShortcutOverlay.vue`
- Modify: `src/web-ui/app/pages/projects/[id]/board.vue` (register board shortcuts)
- Modify: `src/web-ui/app/components/card/CardModal.vue` (register modal shortcuts)

### Step 1: Create useKeyboard composable

Create `src/web-ui/app/composables/useKeyboard.ts`:

```ts
interface Shortcut {
  key: string
  handler: (e: KeyboardEvent) => void
  description: string
  scope: string
}

const shortcuts = ref<Shortcut[]>([])

export function useKeyboard() {
  function register(scope: string, key: string, handler: (e: KeyboardEvent) => void, description: string) {
    shortcuts.value.push({ key, handler, description, scope })
  }

  function unregister(scope: string) {
    shortcuts.value = shortcuts.value.filter(s => s.scope !== scope)
  }

  function getShortcuts(scope: string) {
    return shortcuts.value.filter(s => s.scope === scope)
  }

  function getAllShortcuts() {
    return shortcuts.value
  }

  // Global listener
  if (typeof window !== 'undefined') {
    window.addEventListener('keydown', (e) => {
      // Don't fire shortcuts when typing in inputs
      const target = e.target as HTMLElement
      if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable) {
        // Allow Escape always
        if (e.key !== 'Escape') return
      }

      // Fire matching shortcuts (last registered wins for same key)
      const matching = [...shortcuts.value].reverse().find(s => s.key === e.key || s.key === e.code)
      if (matching) {
        e.preventDefault()
        matching.handler(e)
      }
    })
  }

  return { register, unregister, getShortcuts, getAllShortcuts }
}
```

### Step 2: Create KeyboardShortcutOverlay component

Create `src/web-ui/app/components/shared/KeyboardShortcutOverlay.vue`:

```vue
<script setup lang="ts">
const { getAllShortcuts } = useKeyboard()

const grouped = computed(() => {
  const groups: Record<string, { key: string; description: string }[]> = {}
  for (const s of getAllShortcuts()) {
    if (!groups[s.scope]) groups[s.scope] = []
    groups[s.scope].push({ key: s.key, description: s.description })
  }
  return groups
})
</script>

<template>
  <UModal :open="true" :ui="{ width: 'sm:max-w-md' }">
    <UCard>
      <template #header>
        <h2 class="text-lg font-semibold">Keyboard Shortcuts</h2>
      </template>

      <div class="space-y-4">
        <div v-for="(shortcuts, scope) in grouped" :key="scope">
          <h3 class="text-sm font-semibold text-muted uppercase mb-2">{{ scope }}</h3>
          <div class="space-y-1">
            <div v-for="s in shortcuts" :key="s.key" class="flex justify-between text-sm">
              <kbd class="px-1.5 py-0.5 bg-muted rounded text-xs font-mono">{{ s.key }}</kbd>
              <span class="text-muted">{{ s.description }}</span>
            </div>
          </div>
        </div>
      </div>
    </UCard>
  </UModal>
</template>
```

### Step 3: Register board shortcuts

In `src/web-ui/app/pages/projects/[id]/board.vue`:

```vue
<script setup lang="ts">
// ... existing imports ...

const keyboard = useKeyboard()
const showShortcutOverlay = ref(false)

// Column/card navigation state
const selectedColumnIndex = ref(0)
const selectedCardIndex = ref(0)

onMounted(() => {
  // ... existing onMounted ...

  keyboard.register('Board', 'j', () => {
    selectedCardIndex.value = Math.min(
      selectedCardIndex.value + 1,
      (board.cardsByColumn.get(board.columns[selectedColumnIndex.value]?.id)?.length ?? 1) - 1
    )
  }, 'Next card')

  keyboard.register('Board', 'k', () => {
    selectedCardIndex.value = Math.max(selectedCardIndex.value - 1, 0)
  }, 'Previous card')

  keyboard.register('Board', 'h', () => {
    selectedColumnIndex.value = Math.max(selectedColumnIndex.value - 1, 0)
    selectedCardIndex.value = 0
  }, 'Previous column')

  keyboard.register('Board', 'l', () => {
    selectedColumnIndex.value = Math.min(selectedColumnIndex.value + 1, board.columns.length - 1)
    selectedCardIndex.value = 0
  }, 'Next column')

  keyboard.register('Board', 'Enter', () => {
    const col = board.columns[selectedColumnIndex.value]
    const cards = board.cardsByColumn.get(col?.id) ?? []
    const card = cards[selectedCardIndex.value]
    if (card) selectedCardId.value = card.id
  }, 'Open selected card')

  keyboard.register('Board', '?', () => {
    showShortcutOverlay.value = true
  }, 'Show shortcuts')
})

onBeforeUnmount(() => {
  // ... existing onBeforeUnmount ...
  keyboard.unregister('Board')
})
</script>

<template>
  <!-- ... existing template ... -->

  <KeyboardShortcutOverlay
    v-if="showShortcutOverlay"
    @close="showShortcutOverlay = false"
  />
</template>
```

### Step 4: Register modal shortcuts

In `CardModal.vue`, Escape is already handled. Add Ctrl+Enter for description save (already in CardDescription).

### Step 5: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: press `?` → overlay shows. `j`/`k` navigate cards. `Enter` opens card. `Escape` closes.

### Step 6: Commit

```bash
git add src/web-ui/app/composables/useKeyboard.ts src/web-ui/app/components/shared/KeyboardShortcutOverlay.vue src/web-ui/app/pages/projects/[id]/board.vue
git commit -m "feat: add keyboard shortcuts with overlay for board navigation"
```

---

## Task 22: Error Toast System

**Files:**
- Create: `src/web-ui/app/composables/useErrorToast.ts`
- Create: `src/web-ui/app/components/shared/ErrorToast.vue`
- Modify: `src/web-ui/app/composables/useApi.ts` (integrate toast on error)

### Step 1: Create useErrorToast composable

Create `src/web-ui/app/composables/useErrorToast.ts`:

```ts
import type { ApiError } from '~/lib/api-error'

export function useErrorToast() {
  const toast = useToast()

  function showError(error: ApiError | Error) {
    if (error instanceof ApiError) {
      toast.add({
        title: error.title,
        description: error.detail ?? undefined,
        color: 'error',
        actions: error.correlationId !== 'unknown' ? [{
          label: `ID: ${error.correlationId.slice(0, 8)}...`,
          onClick: () => {
            navigator.clipboard.writeText(error.correlationId)
            toast.add({ title: 'Correlation ID copied', color: 'neutral', duration: 2000 })
          }
        }] : undefined,
        duration: 6000
      })
    }
    else {
      toast.add({
        title: error.message || 'An error occurred',
        color: 'error',
        duration: 5000
      })
    }
  }

  function showWarning(title: string, description?: string) {
    toast.add({ title, description, color: 'warning', duration: 5000 })
  }

  function showSuccess(title: string) {
    toast.add({ title, color: 'success', duration: 3000 })
  }

  return { showError, showWarning, showSuccess }
}
```

### Step 2: Integrate toast into useApi

Modify `src/web-ui/app/composables/useApi.ts` — add toast on error in `onResponse` hook:

```ts
// Inside onResponse error handler, after throwing ApiError:
// Note: can't call useErrorToast() here because composable context.
// Instead, components catch ApiError and call useErrorToast themselves.
// The onResponse hook already throws ApiError — that's sufficient.
```

Components that call API methods should wrap in try/catch and call `useErrorToast().showError(error)`.

### Step 3: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: trigger API error (e.g., invalid login) → toast appears with correlationId copy button

### Step 4: Commit

```bash
git add src/web-ui/app/composables/useErrorToast.ts
git commit -m "feat: add error toast system with correlationId copy"
```

---

## Task 23: Blocked Card Indicators + Move Warning

**Files:**
- Modify: `src/web-ui/app/components/board/BoardCard.vue` (enhance blocked indicator)
- Create: `src/web-ui/app/components/card/BlockedMoveWarning.vue`
- Modify: `src/web-ui/app/pages/projects/[id]/board.vue` (handle 409 on move)

### Step 1: Enhance BoardCard blocked indicator

In `BoardCard.vue`, the lock icon already exists. Add blocked-by count badge:

```vue
<span v-if="card.isBlocked" class="text-warning flex items-center gap-0.5" title="Blocked">
  <UIcon name="i-lucide-lock" class="size-3" />
  <span v-if="card.blockedByCount" class="text-xs">{{ card.blockedByCount }}</span>
</span>
```

### Step 2: Create BlockedMoveWarning component

Create `src/web-ui/app/components/card/BlockedMoveWarning.vue`:

```vue
<script setup lang="ts">
defineProps<{
  blockedByCards: { id: string; title: string }[]
}>()

const emit = defineEmits<{
  confirm: []
  cancel: []
}>()
</script>

<template>
  <UModal :open="true" @close="emit('cancel')">
    <UCard>
      <template #header>
        <div class="flex items-center gap-2">
          <UIcon name="i-lucide-alert-triangle" class="size-5 text-warning" />
          <h3 class="font-semibold">Blocked Card</h3>
        </div>
      </template>

      <div class="space-y-3">
        <p class="text-sm">This card is blocked by:</p>
        <ul class="space-y-1">
          <li v-for="card in blockedByCards" :key="card.id" class="text-sm flex items-center gap-2">
            <UIcon name="i-lucide-lock" class="size-3 text-warning" />
            {{ card.title }}
          </li>
        </ul>
        <p class="text-sm text-muted">Move anyway?</p>
      </div>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton variant="outline" @click="emit('cancel')">Cancel</UButton>
          <UButton color="warning" @click="emit('confirm')">Move Anyway</UButton>
        </div>
      </template>
    </UCard>
  </UModal>
</template>
```

### Step 3: Handle 409 on card move

Modify `handleCardMove` in board page to catch 409 and show warning:

```ts
async function handleCardMove(...) {
  board.moveCard(cardId, targetColumnId, targetPosition)

  try {
    const { error } = await api.POST('/api/projects/{projectId}/Cards/{cardId}/move', {
      params: { path: { projectId, cardId } },
      body: { targetColumnId, targetPosition }
    })
    if (error) {
      if (error.status === 409) {
        // Show blocked move warning
        board.rollbackMove(cardId, sourceColumnId, sourcePosition)
        pendingBlockedMove.value = {
          cardId, targetColumnId, targetPosition, sourceColumnId, sourcePosition,
          blockedByCards: (error as ApiError).detail ? JSON.parse((error as ApiError).detail!) : []
        }
        return
      }
      board.rollbackMove(cardId, sourceColumnId, sourcePosition)
    }
  }
  catch {
    board.rollbackMove(cardId, sourceColumnId, sourcePosition)
  }
}

async function confirmBlockedMove() {
  if (!pendingBlockedMove.value) return
  const { cardId, targetColumnId, targetPosition, sourceColumnId, sourcePosition } = pendingBlockedMove.value
  board.moveCard(cardId, targetColumnId, targetPosition)
  try {
    const { error } = await api.POST('/api/projects/{projectId}/Cards/{cardId}/move', {
      params: { path: { projectId, cardId } },
      body: { targetColumnId, targetPosition, confirmBlockedMove: true }
    })
    if (error) board.rollbackMove(cardId, sourceColumnId, sourcePosition)
  }
  catch {
    board.rollbackMove(cardId, sourceColumnId, sourcePosition)
  }
  pendingBlockedMove.value = null
}
```

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: create blocked card → try to move → warning modal → confirm → moves

### Step 5: Commit

```bash
git add src/web-ui/app/components/board/BoardCard.vue src/web-ui/app/components/card/BlockedMoveWarning.vue src/web-ui/app/pages/projects/[id]/board.vue
git commit -m "feat: add blocked card indicators and move warning modal"
```

---

## Task 24: Archive Card with Dependents Warning (Restore in Plan 3 Task 13)

**Files:**
- Create: `src/web-ui/app/components/card/ArchiveCardWarning.vue`
- Modify: `src/web-ui/app/components/card/CardModal.vue` (add archive button + warning)

### Step 1: Create ArchiveCardWarning component

Create `src/web-ui/app/components/card/ArchiveCardWarning.vue`:

```vue
<script setup lang="ts">
defineProps<{
  dependents: { id: string; title: string; type: string }[]
}>()

const emit = defineEmits<{
  confirm: []
  cancel: []
}>()
</script>

<template>
  <UModal :open="true" @close="emit('cancel')">
    <UCard>
      <template #header>
        <div class="flex items-center gap-2">
          <UIcon name="i-lucide-alert-triangle" class="size-5 text-warning" />
          <h3 class="font-semibold">Archive Card</h3>
        </div>
      </template>

      <div class="space-y-3">
        <p class="text-sm">Archiving this card will affect:</p>
        <ul class="space-y-1">
          <li v-for="dep in dependents" :key="dep.id" class="text-sm flex items-center gap-2">
            <UBadge variant="subtle" size="xs">{{ dep.type }}</UBadge>
            {{ dep.title }}
          </li>
        </ul>
        <p class="text-sm text-muted">This action cannot be undone. Continue?</p>
      </div>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton variant="outline" @click="emit('cancel')">Cancel</UButton>
          <UButton color="error" @click="emit('confirm')">Archive</UButton>
        </div>
      </template>
    </UCard>
  </UModal>
</template>
```

### Step 2: Add archive button to CardModal

In `CardModal.vue`, add archive button in header and wire the warning:

```vue
<script setup lang="ts">
// ... existing ...

const showArchiveWarning = ref(false)
const archiveDependents = ref<{ id: string; title: string; type: string }[]>([])
const archiving = ref(false)

async function handleArchiveClick() {
  // Fetch dependents first
  try {
    const { data } = await api.GET('/api/projects/{projectId}/Cards/{cardId}/Relationships', {
      params: { path: { projectId: props.projectId, cardId: props.cardId } }
    })
    const rels = (data as components['schemas']['CardRelationshipResponse'][]) ?? []
    archiveDependents.value = rels
      .filter(r => r.type === 'BlockedBy' || r.type === 'Precedes')
      .map(r => ({ id: r.relatedCardId, title: r.relatedCardTitle, type: r.type }))
    showArchiveWarning.value = true
  }
  catch { /* proceed without warning */ }
}

async function confirmArchive() {
  archiving.value = true
  try {
    const { error } = await api.POST('/api/projects/{projectId}/Cards/{cardId}/archive', {
      params: { path: { projectId: props.projectId, cardId: props.cardId } }
    })
    if (error) throw error
    board.removeCard(props.cardId)
    emit('close')
  }
  catch { /* toast */ }
  finally {
    archiving.value = false
    showArchiveWarning.value = false
  }
}
</script>

<template>
  <!-- In header, add archive button -->
  <UButton
    icon="i-lucide-archive"
    variant="ghost"
    size="sm"
    color="neutral"
    @click="handleArchiveClick"
  />

  <ArchiveCardWarning
    v-if="showArchiveWarning"
    :dependents="archiveDependents"
    @confirm="confirmArchive"
    @cancel="showArchiveWarning = false"
  />
</template>
```

### Step 3: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open card → click archive → see dependents warning → confirm → card removed from board

### Step 4: Commit

```bash
git add src/web-ui/app/components/card/ArchiveCardWarning.vue src/web-ui/app/components/card/CardModal.vue
git commit -m "feat: add archive card with dependents warning modal"
```

---

## Task 25: ARIA Labels + Focus Management + Color Contrast

**Files:**
- Modify: `src/web-ui/app/components/board/BoardCard.vue`
- Modify: `src/web-ui/app/components/board/BoardColumn.vue`
- Modify: `src/web-ui/app/components/card/CardModal.vue`
- Modify: `src/web-ui/app/components/shared/MarkdownEditor.vue`
- Modify: `src/web-ui/app/pages/login.vue`

### Step 1: Add ARIA labels to interactive elements

**BoardCard.vue** — add `role="button"`, `tabindex="0"`, `aria-label`:
```vue
<UCard
  role="button"
  :tabindex="0"
  :aria-label="`Card: ${card.title}`"
  class="cursor-pointer hover:ring-2 hover:ring-primary/50 transition-shadow"
  @click="emit('select', card.id)"
  @keydown.enter="emit('select', card.id)"
  @keydown.space.prevent="emit('select', card.id)"
>
```

**BoardColumn.vue** — add `role="list"`, `aria-label`:
```vue
<div ref="el" role="list" :aria-label="`${column.name} column`" class="flex-1 overflow-y-auto space-y-2 p-1 min-h-0">
```

**CardModal.vue** — add `role="dialog"`, `aria-modal="true"`, `aria-label`, focus trap:
```vue
<UModal
  :open="true"
  @close="emit('close')"
  :ui="{ width: 'sm:max-w-4xl' }"
  aria-label="Card details"
  role="dialog"
  aria-modal="true"
>
```

**MarkdownEditor.vue** — add `aria-label` to editor:
```vue
<div :aria-label="placeholder" role="textbox" aria-multiline="true">
```

**Login page** — add `aria-label` to form fields:
```vue
<UInput v-model="username" autocomplete="username" required aria-label="Username" />
<UInput v-model="password" type="password" autocomplete="current-password" required aria-label="Password" />
```

### Step 2: Add focus management

In `CardModal.vue`, trap focus inside modal:
```ts
function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') {
    emit('close')
    return
  }
  if (e.key === 'Tab') {
    const modal = (e.currentTarget as HTMLElement).closest('[role="dialog"]')
    if (!modal) return
    const focusable = modal.querySelectorAll<HTMLElement>(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
    )
    const first = focusable[0]
    const last = focusable[focusable.length - 1]
    if (e.shiftKey && document.activeElement === first) {
      e.preventDefault()
      last?.focus()
    }
    else if (!e.shiftKey && document.activeElement === last) {
      e.preventDefault()
      first?.focus()
    }
  }
}
```

### Step 3: Verify color contrast

- Use browser DevTools → CSS Overview → contrast issues
- Ensure all text meets WCAG AA (4.5:1 for normal text, 3:1 for large text)
- Nuxt UI v4 components already meet AA by default — verify custom components only

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: keyboard navigate entire board → all interactive elements focusable → screen reader announces roles

### Step 5: Commit

```bash
git add src/web-ui/app/components/board/BoardCard.vue src/web-ui/app/components/board/BoardColumn.vue src/web-ui/app/components/card/CardModal.vue src/web-ui/app/components/shared/MarkdownEditor.vue src/web-ui/app/pages/login.vue
git commit -m "feat: add ARIA labels, focus management, and color contrast"
```

---

## Task 26: Tablet Responsive Pass

**Files:**
- Modify: `src/web-ui/app/layouts/default.vue` (collapsible sidebar)
- Modify: `src/web-ui/app/components/board/BoardView.vue` (compact columns)
- Modify: `src/web-ui/app/components/board/BoardCard.vue` (compact mode)
- Create: `src/web-ui/app/components/project/ProjectSidebar.vue`

### Step 1: Create ProjectSidebar component

Create `src/web-ui/app/components/project/ProjectSidebar.vue`:

```vue
<script setup lang="ts">
const props = defineProps<{
  projectName: string
  projectId: string
}>()

const isOpen = ref(false)
</script>

<template>
  <!-- Mobile: drawer -->
  <USlideover v-model:open="isOpen" class="lg:hidden">
    <UCard class="h-full">
      <template #header>
        <h3 class="font-semibold">{{ projectName }}</h3>
      </template>
      <nav class="space-y-2">
        <UButton to="/projects" variant="ghost" block>All Projects</UButton>
        <UButton :to="`/projects/${projectId}/board`" variant="ghost" block>Board</UButton>
      </nav>
    </UCard>
  </USlideover>

  <!-- Desktop: persistent sidebar -->
  <aside class="hidden lg:flex flex-col w-56 border-r h-full p-4">
    <h3 class="font-semibold mb-4 truncate">{{ projectName }}</h3>
    <nav class="space-y-1 flex-1">
      <UButton to="/projects" variant="ghost" block size="sm">All Projects</UButton>
      <UButton :to="`/projects/${projectId}/board`" variant="ghost" block size="sm">Board</UButton>
    </nav>
  </aside>

  <!-- Tablet: hamburger toggle -->
  <UButton
    icon="i-lucide-menu"
    variant="ghost"
    size="sm"
    class="lg:hidden fixed top-16 left-2 z-10"
    @click="isOpen = true"
  />
</template>
```

### Step 2: Update default layout with sidebar

Modify `src/web-ui/app/layouts/default.vue` to include `ProjectSidebar` when on a project route:

```vue
<script setup lang="ts">
const route = useRoute()
const isProjectRoute = computed(() => route.params.id != null)
</script>

<template>
  <UApp>
    <UHeader>
      <!-- ... existing header ... -->
    </UHeader>

    <div class="flex h-[calc(100vh-4rem)]">
      <ProjectSidebar
        v-if="isProjectRoute"
        project-name="Project"
        :project-id="route.params.id as string"
      />
      <UMain class="flex-1 overflow-hidden">
        <slot />
      </UMain>
    </div>
  </UApp>
</template>
```

### Step 3: Add compact mode to BoardCard

In `BoardCard.vue`, add a `compact` prop for tablet view:

```vue
<script setup lang="ts">
const props = defineProps<{
  card: CardResponse
  compact?: boolean
}>()
</script>

<template>
  <UCard ...>
    <div class="space-y-1">
      <div class="flex items-center gap-2">
        <UBadge :color="typeBadgeColor" variant="subtle" size="xs">
          {{ card.type }}
        </UBadge>
        <span v-if="card.isBlocked" class="text-warning">
          <UIcon name="i-lucide-lock" class="size-3" />
        </span>
      </div>
      <p class="text-sm font-medium line-clamp-2">{{ card.title }}</p>
      <!-- Only show assignees on non-compact -->
      <div v-if="!compact && card.assignees?.length" class="flex -space-x-1">
        ...
      </div>
    </div>
  </UCard>
</template>
```

### Step 4: Update BoardView for tablet

In `BoardView.vue`, pass `compact` prop and adjust column width:

```vue
<BoardColumn
  v-for="column in columns"
  :key="column.id"
  :column="column"
  :cards="cardsByColumn.get(column.id) ?? []"
  :compact="true"
  class="lg:w-72 md:w-60"
  @card-select="emit('cardSelect', $event)"
  @card-move="onCardMove"
/>
```

### Step 5: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: resize to 768-1199px → sidebar collapses to hamburger → board columns compact → 2-3 visible
- Manual: resize to <768px → mobile list view (already built)

### Step 6: Commit

```bash
git add src/web-ui/app/components/project/ProjectSidebar.vue src/web-ui/app/layouts/default.vue src/web-ui/app/components/board/BoardView.vue src/web-ui/app/components/board/BoardCard.vue
git commit -m "feat: add tablet responsive pass with collapsible sidebar and compact board"
```

---

## Task 27: PWA Manifest + Installable

**Files:**
- Create: `src/web-ui/public/manifest.json` (or use @vite-pwa/nuxt)
- Modify: `src/web-ui/nuxt.config.ts` (add @vite-pwa/nuxt)

### Step 1: Install @vite-pwa/nuxt

```bash
cd src/web-ui && pnpm add -D @vite-pwa/nuxt
```

### Step 2: Configure PWA in nuxt.config.ts

Add to `modules` and add `pwa` config:

```ts
export default defineNuxtConfig({
  modules: [
    '@nuxt/eslint',
    '@nuxt/ui',
    '@pinia/nuxt',
    '@vite-pwa/nuxt'
  ],

  pwa: {
    registerType: 'autoUpdate',
    manifest: {
      name: 'HydraForge',
      short_name: 'HydraForge',
      description: 'Self-hosted AI workspace + project management',
      theme_color: '#00C16A',
      background_color: '#ffffff',
      display: 'standalone',
      icons: [
        {
          src: '/favicon.ico',
          sizes: '64x64',
          type: 'image/x-icon'
        }
      ]
    },
    workbox: {
      navigateFallback: '/',
      globPatterns: ['**/*.{js,css,html,png,svg,ico}']
    }
  },

  // ... rest unchanged
})
```

### Step 3: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- `cd src/web-ui && pnpm build` — successful production build
- Manual: open in Chrome → "Install" button appears in address bar → install → opens as standalone app

### Step 4: Commit

```bash
git add src/web-ui/package.json src/web-ui/pnpm-lock.yaml src/web-ui/nuxt.config.ts
git commit -m "feat: add PWA manifest and installable support"
```

---

## Task 28: Polish & Hardening Component Tests

**Files:**
- Create: `src/web-ui/app/composables/__tests__/useKeyboard.test.ts`
- Create: `src/web-ui/app/composables/__tests__/useErrorToast.test.ts`
- Create: `src/web-ui/app/components/card/__tests__/BlockedMoveWarning.test.ts`
- Create: `src/web-ui/app/components/card/__tests__/ArchiveCardWarning.test.ts`
- Create: `src/web-ui/app/components/shared/__tests__/KeyboardShortcutOverlay.test.ts`

### Step 1: Write useKeyboard composable test

Create `src/web-ui/app/composables/__tests__/useKeyboard.test.ts`:

```ts
import { describe, it, expect, vi } from 'vitest'
import { useKeyboard } from '~/composables/useKeyboard'

describe('useKeyboard', () => {
  it('registers and retrieves shortcuts by scope', () => {
    const kb = useKeyboard()
    const handler = vi.fn()

    kb.register('Board', 'j', handler, 'Next card')
    kb.register('Board', 'k', handler, 'Previous card')
    kb.register('Modal', 'Escape', handler, 'Close modal')

    const boardShortcuts = kb.getShortcuts('Board')
    expect(boardShortcuts.length).toBe(2)
    expect(boardShortcuts[0].description).toBe('Next card')
    expect(boardShortcuts[1].description).toBe('Previous card')
  })

  it('unregister removes all shortcuts for scope', () => {
    const kb = useKeyboard()
    kb.register('Board', 'j', vi.fn(), 'Next card')
    kb.register('Modal', 'Escape', vi.fn(), 'Close')

    kb.unregister('Board')
    expect(kb.getShortcuts('Board').length).toBe(0)
    expect(kb.getShortcuts('Modal').length).toBe(1)
  })

  it('getAllShortcuts returns all registered shortcuts', () => {
    const kb = useKeyboard()
    kb.register('Board', 'j', vi.fn(), 'Next')
    kb.register('Modal', 'Escape', vi.fn(), 'Close')

    expect(kb.getAllShortcuts().length).toBe(2)
  })
})
```

### Step 2: Write useErrorToast composable test

Create `src/web-ui/app/composables/__tests__/useErrorToast.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { useErrorToast } from '~/composables/useErrorToast'

describe('useErrorToast', () => {
  it('returns showError, showWarning, and showSuccess functions', () => {
    const toast = useErrorToast()
    expect(typeof toast.showError).toBe('function')
    expect(typeof toast.showWarning).toBe('function')
    expect(typeof toast.showSuccess).toBe('function')
  })
})
```

### Step 3: Write BlockedMoveWarning component test

Create `src/web-ui/app/components/card/__tests__/BlockedMoveWarning.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import BlockedMoveWarning from '~/components/card/BlockedMoveWarning.vue'

describe('BlockedMoveWarning', () => {
  it('renders blocked card warning', async () => {
    const wrapper = await mountSuspended(BlockedMoveWarning, {
      props: {
        blockedByCards: [
          { id: 'c1', title: 'Blocker Card' }
        ]
      }
    })
    expect(wrapper.text()).toContain('Blocked Card')
    expect(wrapper.text()).toContain('Blocker Card')
    expect(wrapper.text()).toContain('Move Anyway')
  })

  it('emits confirm on Move Anyway click', async () => {
    const wrapper = await mountSuspended(BlockedMoveWarning, {
      props: { blockedByCards: [{ id: 'c1', title: 'Blocker' }] }
    })
    const buttons = wrapper.findAll('button')
    const moveButton = buttons.find(b => b.text().includes('Move Anyway'))
    await moveButton?.trigger('click')
    expect(wrapper.emitted('confirm')).toBeTruthy()
  })

  it('emits cancel on Cancel click', async () => {
    const wrapper = await mountSuspended(BlockedMoveWarning, {
      props: { blockedByCards: [{ id: 'c1', title: 'Blocker' }] }
    })
    const buttons = wrapper.findAll('button')
    const cancelButton = buttons.find(b => b.text().includes('Cancel'))
    await cancelButton?.trigger('click')
    expect(wrapper.emitted('cancel')).toBeTruthy()
  })
})
```

### Step 4: Write ArchiveCardWarning component test

Create `src/web-ui/app/components/card/__tests__/ArchiveCardWarning.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ArchiveCardWarning from '~/components/card/ArchiveCardWarning.vue'

describe('ArchiveCardWarning', () => {
  it('renders archive warning with dependents', async () => {
    const wrapper = await mountSuspended(ArchiveCardWarning, {
      props: {
        dependents: [
          { id: 'c1', title: 'Child Card', type: 'BlockedBy' },
          { id: 'c2', title: 'Related Card', type: 'Precedes' }
        ]
      }
    })
    expect(wrapper.text()).toContain('Archive Card')
    expect(wrapper.text()).toContain('Child Card')
    expect(wrapper.text()).toContain('Related Card')
    expect(wrapper.text()).toContain('Archive')
  })

  it('emits confirm on Archive click', async () => {
    const wrapper = await mountSuspended(ArchiveCardWarning, {
      props: { dependents: [{ id: 'c1', title: 'Child', type: 'BlockedBy' }] }
    })
    const buttons = wrapper.findAll('button')
    const archiveButton = buttons.find(b => b.text().includes('Archive'))
    await archiveButton?.trigger('click')
    expect(wrapper.emitted('confirm')).toBeTruthy()
  })

  it('emits cancel on Cancel click', async () => {
    const wrapper = await mountSuspended(ArchiveCardWarning, {
      props: { dependents: [] }
    })
    const buttons = wrapper.findAll('button')
    const cancelButton = buttons.find(b => b.text().includes('Cancel'))
    await cancelButton?.trigger('click')
    expect(wrapper.emitted('cancel')).toBeTruthy()
  })
})
```

### Step 5: Write KeyboardShortcutOverlay component test

Create `src/web-ui/app/components/shared/__tests__/KeyboardShortcutOverlay.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import KeyboardShortcutOverlay from '~/components/shared/KeyboardShortcutOverlay.vue'

describe('KeyboardShortcutOverlay', () => {
  it('renders keyboard shortcuts title', async () => {
    const wrapper = await mountSuspended(KeyboardShortcutOverlay)
    expect(wrapper.text()).toContain('Keyboard Shortcuts')
  })
})
```

### Step 6: Verify

- `cd src/web-ui && pnpm test` — all tests pass
- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors

### Step 7: Commit

```bash
git add src/web-ui/app/composables/__tests__/useKeyboard.test.ts src/web-ui/app/composables/__tests__/useErrorToast.test.ts src/web-ui/app/components/card/__tests__/BlockedMoveWarning.test.ts src/web-ui/app/components/card/__tests__/ArchiveCardWarning.test.ts src/web-ui/app/components/shared/__tests__/KeyboardShortcutOverlay.test.ts
git commit -m "feat: add polish and hardening component tests (keyboard, error toast, warnings)"
```

---

## Verification (Plan 6 Complete)

Reference `nuxt-verification` skill:
1. `cd src/web-ui && pnpm typecheck` — zero errors
2. `cd src/web-ui && pnpm lint` — zero errors
3. `cd src/web-ui && pnpm build` — successful production build
4. `cd src/web-ui && pnpm test` — all tests pass
5. Manual: keyboard shortcuts work. Error toasts show. Blocked card warning. Archive warning. ARIA labels present. Tablet responsive. PWA installable.

---

## Task 29: Archive/Restore Project

**Files:**
- Modify: `src/web-ui/app/pages/projects/index.vue` (add archive/restore actions + filter toggle)
- Modify: `src/web-ui/app/components/project/ProjectList.vue` (add action buttons)

### Step 1: Add archive/restore to project list page

In `projects/index.vue`, add a toggle to show/hide archived projects:

```vue
<div class="flex items-center gap-2 mb-4">
  <UToggle v-model="showArchived" />
  <span class="text-sm">Show archived</span>
</div>
```

API call with filter:
```ts
const showArchived = ref(false)

async function fetchProjects() {
  const url = showArchived.value
    ? '/api/Projects?includeArchived=true'
    : '/api/Projects'
  const { data } = await api.GET(url)
  // ...
}
```

### Step 2: Add archive/restore buttons to ProjectList rows

In `ProjectList.vue`, add per-row actions:

```vue
<UButton
  v-if="!project.archivedAt"
  variant="ghost"
  size="sm"
  icon="i-lucide-archive"
  @click="emit('archive', project.id)"
>
  Archive
</UButton>
<UButton
  v-else
  variant="ghost"
  size="sm"
  icon="i-lucide-archive-restore"
  @click="emit('restore', project.id)"
>
  Restore
</UButton>
```

Wire in `projects/index.vue`:
```ts
async function handleArchive(projectId: string) {
  await api.POST('/api/Projects/{projectId}/archive', { params: { path: { projectId } } })
  fetchProjects()
}

async function handleRestore(projectId: string) {
  await api.POST('/api/Projects/{projectId}/restore', { params: { path: { projectId } } })
  fetchProjects()
}
```

### Step 3: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open project list → archive a project → it disappears from default view → enable "show archived" → project appears with restore option

### Step 4: Commit

```bash
git add src/web-ui/app/pages/projects/index.vue src/web-ui/app/components/project/ProjectList.vue
git commit -m "feat: add archive/restore project with filter toggle"
```

---

## Final Phase 3 Verification

After all 6 plans complete:

1. `cd src/web-ui && pnpm typecheck` — zero errors
2. `cd src/web-ui && pnpm lint` — zero errors
3. `cd src/web-ui && pnpm build` — successful production build
4. `cd src/web-ui && pnpm test` — all tests pass
5. Manual full flow: login → create project → add columns → create cards → move cards → open card → edit description → add checklist → comment → upload attachment → add dependency → archive card → verify real-time sync across two browser tabs
6. Mobile: all above on <768px viewport
7. Tablet: all above on 768-1199px viewport
8. Keyboard: navigate board, open card, close modal, move card — all via keyboard only
9. WCAG AA: color contrast audit on all components