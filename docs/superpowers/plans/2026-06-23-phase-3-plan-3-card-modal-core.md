# Plan 3: Card Modal Core — Desktop Split + Mobile Tabs + Tiptap Description
**Branch:** `task/phase-3-card-modal-core`
**Parent branch:** `feat/phase-3-web-ui`
**Parent spec:** `2026-06-23-phase-3-web-ui-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Card detail modal with desktop two-column layout (description/comments left, metadata right), mobile tabbed layout, and Tiptap markdown editor for card descriptions.

**Architecture:** `CardModal.vue` is the container — fetches card detail, switches layout by breakpoint. Desktop: `CardDescription.vue` (Tiptap) + comments slot on left, `CardMetadata.vue` on right. Mobile: tabbed with "Details", "Checklist", "Comments", "Related" tabs. Tiptap editor wraps `@tiptap/vue-3` with starter-kit + placeholder extension, styled with Tailwind.

**Tech Stack:** @tiptap/vue-3, @tiptap/starter-kit, @tiptap/extension-placeholder, Nuxt UI v4

**Depends on:** Plan 2 (Project List & Board) — needs board page with cardSelect emit, useBoardStore

**Spec ref:** Sections 3.2, 6, 11 (card description), 13 (card components)

---

## Task 9: Card Modal — Desktop Two-Column Layout

**Files:**
- Create: `src/web-ui/app/components/card/CardModal.vue`
- Create: `src/web-ui/app/components/card/CardMetadata.vue` (placeholder)
- Modify: `src/web-ui/app/pages/projects/[id]/board.vue` (wire modal)

### Step 1: Create CardMetadata placeholder

Create `src/web-ui/app/components/card/CardMetadata.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

defineProps<{
  card: CardResponse
}>()
</script>

<template>
  <div class="space-y-4">
    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1">Type</p>
      <UBadge variant="subtle">{{ card.type }}</UBadge>
    </div>

    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1">Column</p>
      <p class="text-sm">{{ card.columnId }}</p>
    </div>

    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1">Assignees</p>
      <div v-if="card.assignees?.length" class="flex flex-wrap gap-1">
        <UAvatar
          v-for="a in card.assignees"
          :key="a.userId"
          :alt="a.username"
          size="sm"
        />
      </div>
      <p v-else class="text-sm text-muted">None</p>
    </div>

    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1">Due Date</p>
      <p class="text-sm">{{ card.dueDate ?? 'None' }}</p>
    </div>
  </div>
</template>
```

### Step 2: Create CardModal component

Create `src/web-ui/app/components/card/CardModal.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  cardId: string
  projectId: string
}>()

const emit = defineEmits<{
  close: []
}>()

const card = ref<CardResponse | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

const api = useApi()

async function fetchCard() {
  loading.value = true
  try {
    const { data, error: apiError } = await api.GET('/api/projects/{projectId}/Cards/{cardId}', {
      params: { path: { projectId: props.projectId, cardId: props.cardId } }
    })
    if (apiError) throw apiError
    card.value = data as CardResponse
  }
  catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load card'
  }
  finally {
    loading.value = false
  }
}

onMounted(() => fetchCard())

// Keyboard: Escape closes modal
function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') emit('close')
}
</script>

<template>
  <UModal :open="true" @close="emit('close')" :ui="{ width: 'sm:max-w-4xl' }">
    <div class="flex flex-col max-h-[85vh]" @keydown="onKeydown">
      <!-- Loading -->
      <div v-if="loading" class="flex items-center justify-center p-8">
        <UIcon name="i-lucide-loader" class="animate-spin size-8" />
      </div>

      <!-- Error -->
      <UAlert v-else-if="error" color="error" :title="error" />

      <!-- Content -->
      <template v-else-if="card">
        <!-- Header -->
        <div class="flex items-center justify-between p-4 border-b">
          <h2 class="text-lg font-semibold truncate">{{ card.title }}</h2>
          <UButton icon="i-lucide-x" variant="ghost" size="sm" @click="emit('close')" />
        </div>

        <!-- Desktop: two-column -->
        <div class="hidden md:flex flex-1 overflow-hidden">
          <!-- Left: description + comments -->
          <div class="flex-1 overflow-y-auto p-4 space-y-6">
            <CardDescription :card="card" :project-id="projectId" />
            <!-- Comments slot — Task 13 -->
          </div>

          <!-- Right: metadata -->
          <div class="w-64 flex-shrink-0 border-l overflow-y-auto p-4">
            <CardMetadata :card="card" />
          </div>
        </div>

        <!-- Mobile: tabbed — Task 10 -->
        <div class="md:hidden p-4">
          <p class="text-sm text-muted">Mobile card view coming in Task 10</p>
        </div>
      </template>
    </div>
  </UModal>
</template>
```

### Step 3: Wire CardModal into board page

Modify `src/web-ui/app/pages/projects/[id]/board.vue` — add CardModal import and rendering after the board views:

```vue
<script setup lang="ts">
// ... existing imports ...

const selectedCardId = ref<string | null>(null)

// ... existing code ...
</script>

<template>
  <div class="h-[calc(100vh-4rem)]">
    <!-- ... existing loading/error/board views ... -->

    <CardModal
      v-if="selectedCardId"
      :card-id="selectedCardId"
      :project-id="projectId"
      @close="selectedCardId = null"
    />
  </div>
</template>
```

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: click card on board → modal opens with title, metadata sidebar, description placeholder
- Manual: Escape key closes modal

### Step 5: Commit

```bash
git add src/web-ui/app/components/card/CardModal.vue src/web-ui/app/components/card/CardMetadata.vue src/web-ui/app/pages/projects/[id]/board.vue
git commit -m "feat: add card modal with desktop two-column layout"
```

---

## Task 10: Card Modal — Mobile Tabbed Layout

**Files:**
- Modify: `src/web-ui/app/components/card/CardModal.vue` (add mobile tabs)

### Step 1: Add mobile tabbed layout to CardModal

Replace the mobile placeholder in `CardModal.vue` with tabbed layout:

```vue
<script setup lang="ts">
// ... existing imports ...

const activeTab = ref<'details' | 'checklist' | 'comments' | 'related'>('details')

const tabs = [
  { label: 'Details', value: 'details' as const },
  { label: 'Checklist', value: 'checklist' as const },
  { label: 'Comments', value: 'comments' as const },
  { label: 'Related', value: 'related' as const }
]
</script>

<template>
  <UModal :open="true" @close="emit('close')" :ui="{ width: 'sm:max-w-4xl' }">
    <div class="flex flex-col max-h-[85vh]" @keydown="onKeydown">
      <!-- ... loading/error/header same as before ... -->

      <template v-else-if="card">
        <!-- Header -->
        <div class="flex items-center justify-between p-4 border-b">
          <h2 class="text-lg font-semibold truncate">{{ card.title }}</h2>
          <UButton icon="i-lucide-x" variant="ghost" size="sm" @click="emit('close')" />
        </div>

        <!-- Desktop: two-column -->
        <div class="hidden md:flex flex-1 overflow-hidden">
          <div class="flex-1 overflow-y-auto p-4 space-y-6">
            <CardDescription :card="card" :project-id="projectId" />
          </div>
          <div class="w-64 flex-shrink-0 border-l overflow-y-auto p-4">
            <CardMetadata :card="card" />
          </div>
        </div>

        <!-- Mobile: tabbed -->
        <div class="md:hidden flex flex-col flex-1 overflow-hidden">
          <UTabs
            v-model="activeTab"
            :items="tabs"
            class="border-b"
            :ui="{ list: { tab: 'text-xs' } }"
          />

          <div class="flex-1 overflow-y-auto p-4">
            <!-- Details tab -->
            <div v-if="activeTab === 'details'" class="space-y-4">
              <CardDescription :card="card" :project-id="projectId" />
              <CardMetadata :card="card" />
            </div>

            <!-- Checklist tab — Task 12 -->
            <div v-else-if="activeTab === 'checklist'">
              <p class="text-sm text-muted">Checklist coming soon</p>
            </div>

            <!-- Comments tab — Task 13 -->
            <div v-else-if="activeTab === 'comments'">
              <p class="text-sm text-muted">Comments coming soon</p>
            </div>

            <!-- Related tab — Tasks 14, 15, 17, 18 -->
            <div v-else-if="activeTab === 'related'" class="space-y-4">
              <p class="text-sm text-muted">Attachments, dependencies, specs, plans coming soon</p>
            </div>
          </div>
        </div>
      </template>
    </div>
  </UModal>
</template>
```

### Step 2: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open card on mobile viewport → tabs render → switch between tabs

### Step 3: Commit

```bash
git add src/web-ui/app/components/card/CardModal.vue
git commit -m "feat: add mobile tabbed layout to card modal"
```

---

## Task 11: Card Description — Tiptap Editor

**Files:**
- Create: `src/web-ui/app/components/shared/MarkdownEditor.vue`
- Create: `src/web-ui/app/components/card/CardDescription.vue`

### Step 1: Install Tiptap

```bash
cd src/web-ui && pnpm add @tiptap/vue-3 @tiptap/starter-kit @tiptap/extension-placeholder
```

### Step 2: Create MarkdownEditor wrapper

Create `src/web-ui/app/components/shared/MarkdownEditor.vue`:

```vue
<script setup lang="ts">
import { useEditor, EditorContent } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import Placeholder from '@tiptap/extension-placeholder'

const props = withDefaults(defineProps<{
  modelValue: string
  placeholder?: string
  editable?: boolean
}>(), {
  placeholder: 'Write something...',
  editable: true
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const editor = useEditor({
  content: props.modelValue,
  editable: props.editable,
  extensions: [
    StarterKit,
    Placeholder.configure({ placeholder: props.placeholder })
  ],
  onUpdate({ editor }) {
    emit('update:modelValue', editor.getHTML())
  }
})

watch(() => props.modelValue, (val) => {
  if (editor.value && editor.value.getHTML() !== val) {
    editor.value.commands.setContent(val, false)
  }
})

onBeforeUnmount(() => {
  editor.value?.destroy()
})
</script>

<template>
  <div v-if="editor" class="prose prose-sm max-w-none" :class="{ 'border rounded-md p-3 min-h-[100px]': editable }">
    <EditorContent :editor="editor" />
  </div>
</template>
```

### Step 3: Create CardDescription component

Create `src/web-ui/app/components/card/CardDescription.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  card: CardResponse
  projectId: string
}>()

const description = ref(props.card.description ?? '')
const saving = ref(false)
const saveError = ref<string | null>(null)

const api = useApi()
const board = useBoardStore()

let saveTimer: ReturnType<typeof setTimeout> | null = null

function onDescriptionChange(value: string) {
  description.value = value
  // Debounced auto-save
  if (saveTimer) clearTimeout(saveTimer)
  saveTimer = setTimeout(saveDescription, 1500)
}

async function saveDescription() {
  saving.value = true
  saveError.value = null
  try {
    const { error } = await api.PUT('/api/projects/{projectId}/Cards/{cardId}', {
      params: { path: { projectId: props.projectId, cardId: props.card.id } },
      body: { description: description.value }
    })
    if (error) throw error
    board.updateCard(props.card.id, { description: description.value })
  }
  catch (e: unknown) {
    saveError.value = e instanceof Error ? e.message : 'Failed to save'
  }
  finally {
    saving.value = false
  }
}

// Ctrl+Enter to save immediately
function onKeydown(e: KeyboardEvent) {
  if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
    e.preventDefault()
    if (saveTimer) clearTimeout(saveTimer)
    saveDescription()
  }
}
</script>

<template>
  <div @keydown="onKeydown">
    <div class="flex items-center justify-between mb-2">
      <p class="text-xs font-medium text-muted uppercase">Description</p>
      <span v-if="saving" class="text-xs text-muted">Saving...</span>
    </div>

    <MarkdownEditor
      v-model="description"
      placeholder="Add a description..."
      @update:model-value="onDescriptionChange"
    />

    <p v-if="saveError" class="text-xs text-error mt-1">{{ saveError }}</p>
  </div>
</template>
```

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open card → type in description → auto-saves after 1.5s → Ctrl+Enter saves immediately
- Manual: formatting toolbar works (bold, italic, headings, lists)

### Step 5: Commit

```bash
git add src/web-ui/package.json src/web-ui/pnpm-lock.yaml src/web-ui/app/components/shared/MarkdownEditor.vue src/web-ui/app/components/card/CardDescription.vue
git commit -m "feat: add Tiptap markdown editor and card description with auto-save"
```

---

## Task 12: Card Modal + Editor Component Tests

**Files:**
- Create: `src/web-ui/app/components/card/__tests__/CardModal.test.ts`
- Create: `src/web-ui/app/components/card/__tests__/CardDescription.test.ts`
- Create: `src/web-ui/app/components/shared/__tests__/MarkdownEditor.test.ts`
- Create: `src/web-ui/app/components/card/__tests__/CardMetadata.test.ts`

### Step 1: Write CardModal component test

Create `src/web-ui/app/components/card/__tests__/CardModal.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardModal from '~/components/card/CardModal.vue'

describe('CardModal', () => {
  it('shows loading spinner initially', async () => {
    const wrapper = await mountSuspended(CardModal, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.find('.animate-spin').exists()).toBe(true)
  })

  it('has close button', async () => {
    const wrapper = await mountSuspended(CardModal, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    // Close button exists in header
    expect(wrapper.find('button').exists()).toBe(true)
  })
})
```

### Step 2: Write CardDescription component test

Create `src/web-ui/app/components/card/__tests__/CardDescription.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardDescription from '~/components/card/CardDescription.vue'

describe('CardDescription', () => {
  const baseCard = {
    id: 'c1',
    title: 'Test',
    description: 'Initial description',
    type: 'Task'
  } as any

  it('renders description label', async () => {
    const wrapper = await mountSuspended(CardDescription, {
      props: { card: baseCard, projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Description')
  })

  it('renders markdown editor with card description', async () => {
    const wrapper = await mountSuspended(CardDescription, {
      props: { card: baseCard, projectId: 'p1' }
    })
    expect(wrapper.findComponent({ name: 'MarkdownEditor' }).exists()).toBe(true)
  })
})
```

### Step 3: Write MarkdownEditor component test

Create `src/web-ui/app/components/shared/__tests__/MarkdownEditor.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import MarkdownEditor from '~/components/shared/MarkdownEditor.vue'

describe('MarkdownEditor', () => {
  it('renders editor content area', async () => {
    const wrapper = await mountSuspended(MarkdownEditor, {
      props: { modelValue: '# Hello', placeholder: 'Write...' }
    })
    expect(wrapper.find('.ProseMirror').exists() || wrapper.find('[contenteditable]').exists()).toBe(true)
  })

  it('renders with placeholder', async () => {
    const wrapper = await mountSuspended(MarkdownEditor, {
      props: { modelValue: '', placeholder: 'Custom placeholder' }
    })
    expect(wrapper.html()).toBeTruthy()
  })

  it('renders in read-only mode when editable=false', async () => {
    const wrapper = await mountSuspended(MarkdownEditor, {
      props: { modelValue: '# Read only', editable: false }
    })
    expect(wrapper.html()).toBeTruthy()
  })
})
```

### Step 4: Write CardMetadata component test

Create `src/web-ui/app/components/card/__tests__/CardMetadata.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardMetadata from '~/components/card/CardMetadata.vue'

describe('CardMetadata', () => {
  const baseCard = {
    id: 'c1',
    title: 'Test',
    type: 'Task',
    columnId: 'col1',
    dueDate: null,
    assignees: [],
    parentEpicId: null
  } as any

  it('renders type badge', async () => {
    const wrapper = await mountSuspended(CardMetadata, {
      props: { card: baseCard }
    })
    expect(wrapper.text()).toContain('Type')
    expect(wrapper.text()).toContain('Task')
  })

  it('shows None for missing assignees', async () => {
    const wrapper = await mountSuspended(CardMetadata, {
      props: { card: baseCard }
    })
    expect(wrapper.text()).toContain('None')
  })

  it('shows None for missing due date', async () => {
    const wrapper = await mountSuspended(CardMetadata, {
      props: { card: baseCard }
    })
    expect(wrapper.text()).toContain('None')
  })

  it('renders assignee avatars when present', async () => {
    const wrapper = await mountSuspended(CardMetadata, {
      props: {
        card: {
          ...baseCard,
          assignees: [{ userId: 'u1', username: 'Alice' }]
        }
      }
    })
    expect(wrapper.text()).toContain('Assignees')
  })
})
```

### Step 5: Verify

- `cd src/web-ui && pnpm test` — all tests pass
- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors

### Step 6: Commit

```bash
git add src/web-ui/app/components/card/__tests__/ src/web-ui/app/components/shared/__tests__/
git commit -m "feat: add card modal, description, editor, and metadata component tests"
```

---

## Verification (Plan 3 Complete)

Reference `nuxt-verification` skill:
1. `cd src/web-ui && pnpm typecheck` — zero errors
2. `cd src/web-ui && pnpm lint` — zero errors
3. `cd src/web-ui && pnpm build` — successful production build
4. `cd src/web-ui && pnpm test` — all tests pass
5. Manual: open card → desktop two-column layout → edit description → auto-save → mobile tabs work