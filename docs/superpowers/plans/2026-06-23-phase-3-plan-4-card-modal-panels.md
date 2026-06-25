# Plan 4: Card Modal Panels — Checklist, Comments, Attachments, Dependencies, Metadata
**Branch:** `task/phase-3-card-modal-panels`
**Parent branch:** `feat/phase-3-web-ui`
**Parent spec:** `2026-06-23-phase-3-web-ui-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete all card modal panels: checklist with toggle, comments with input, file attachments with upload/download, dependency graph panel, and full metadata editor (type, column, assignees, due date, parent epic).

**Architecture:** Each panel is a standalone component that calls the API directly. `CardModal.vue` wires them into desktop right sidebar and mobile "Related" tab. Checklist uses `vue-draggable-plus` for item reorder. Comments use Tiptap for input. Attachments use native `<input type="file">` + FormData upload. Dependencies show linked cards with relationship type badges.

**Tech Stack:** vue-draggable-plus, Tiptap, Nuxt UI v4

**Depends on:** Plan 3 (Card Modal Core) — needs CardModal container, CardMetadata placeholder

**Spec ref:** Sections 6 (checklist, comments, attachments, dependencies, metadata), 13 (card components)

---

> **Pre-execution note (added 2026-06-25, see `docs/DECISIONS.md` D-40/D-41):** This plan was drafted before three conventions settled on `feat/phase-3-web-ui`. Whoever executes a task below must adapt the snippet to current code, not copy it verbatim:
>
> 1. **API calls use the pre-`ApiRoutes` style.** Every `api.GET/POST/PUT/DELETE(...)` call in this document uses openapi-fetch's raw `params: { path: {...} }` form against a literal path template. The settled convention (`CLAUDE.md` → Web UI Conventions) is `ApiRoutes.<Resource>.<action>(...)` from `app/lib/routes.ts` — add any missing route helper to `routes.ts` rather than inlining a path string.
> 2. **`useApi()` throws — it does not resolve `{ error }`.** Every snippet below that does `const { error } = await api.X(...); if (error) throw error` inside a `try { ... } catch { /* toast */ }` happens to work by accident (the `await` itself throws and lands in the `catch`), but the `if (error) throw error` line is dead code and the `/* toast */` placeholder comments are not real implementations — replace them with an actual `toast.add({ title: '...', color: 'error' })` call. See D-40.
> 3. **`vue-draggable-plus` (the Tech Stack line and Task 12's `useDraggable` import) was removed project-wide in D-37**, the same day this plan was drafted — it is SSR-incompatible with Nuxt 4. Use plain `v-for` for checklist item rendering; reorder can ship without drag-and-drop (e.g. up/down buttons) until native HTML5 drag-and-drop lands per D-37's follow-up.
> 4. **Task 16 (Card Metadata Editor) below has been rewritten** to fix three additional issues found when `task/phase-3-card-modal-hardening` was scoped: stale field names (`card.dueDate`/`card.parentEpicId` do not exist — the real `CardResponse` fields are `dueAt`/`parentCardId`), a `PUT` body missing the required `title`/`description`/`version` fields (`UpdateCardRequest` is a full replace, not a partial patch), and a stale "assignee management deferred" note (the member-list endpoint it was waiting on already exists — see `ApiRoutes.Projects.members`, used by `CardCreateModal.vue`). Execute Task 16 as written below, not as a fresh design.
> 5. **Card version ownership lives on `CardModal.vue`'s `card` ref** (D-41, implemented in `2026-06-25-phase-3-card-modal-hardening.md` Task 2). Every panel below that mutates a `Card` field (Task 16 only — Checklist/Comments/Attachments/Dependencies mutate their own sub-resources, not `Card.Version`) must emit `update:card` with the server's response and let `CardModal.vue` update `card.value`, exactly like `CardDescription.vue` does after that hardening plan lands. Do not reintroduce a locally-cached version.

---

## Task 12: Card Checklist

**Files:**
- Create: `src/web-ui/app/components/card/CardChecklist.vue`
- Modify: `src/web-ui/app/components/card/CardModal.vue` (wire checklist into desktop + mobile)

### Step 1: Create CardChecklist component

Create `src/web-ui/app/components/card/CardChecklist.vue`:

```vue
<script setup lang="ts">
import { useDraggable } from 'vue-draggable-plus'
import type { components } from '~/types/api'

type ChecklistItemResponse = components['schemas']['ChecklistItemResponse']

const props = defineProps<{
  cardId: string
  projectId: string
}>()

const items = ref<ChecklistItemResponse[]>([])
const loading = ref(true)
const newItemText = ref('')
const adding = ref(false)

const api = useApi()
const listEl = ref<HTMLElement>()

async function fetchItems() {
  loading.value = true
  try {
    const { data } = await api.GET('/api/projects/{projectId}/Cards/{cardId}/Checklist', {
      params: { path: { projectId: props.projectId, cardId: props.cardId } }
    })
    items.value = (data as ChecklistItemResponse[]) ?? []
  }
  catch { /* silently fail */ }
  finally { loading.value = false }
}

async function addItem() {
  if (!newItemText.value.trim()) return
  adding.value = true
  try {
    const { data, error } = await api.POST('/api/projects/{projectId}/Cards/{cardId}/Checklist', {
      params: { path: { projectId: props.projectId, cardId: props.cardId } },
      body: { text: newItemText.value.trim() }
    })
    if (error) throw error
    items.value.push(data as ChecklistItemResponse)
    newItemText.value = ''
  }
  catch { /* toast handled by useApi */ }
  finally { adding.value = false }
}

async function toggleItem(item: ChecklistItemResponse) {
  try {
    const { error } = await api.PUT('/api/projects/{projectId}/Cards/{cardId}/Checklist/{itemId}', {
      params: { path: { projectId: props.projectId, cardId: props.cardId, itemId: item.id } },
      body: { isCompleted: !item.isCompleted }
    })
    if (error) throw error
    item.isCompleted = !item.isCompleted
  }
  catch { /* toast */ }
}

async function deleteItem(itemId: string) {
  try {
    const { error } = await api.DELETE('/api/projects/{projectId}/Cards/{cardId}/Checklist/{itemId}', {
      params: { path: { projectId: props.projectId, cardId: props.cardId, itemId } }
    })
    if (error) throw error
    items.value = items.value.filter(i => i.id !== itemId)
  }
  catch { /* toast */ }
}

const completedCount = computed(() => items.value.filter(i => i.isCompleted).length)

useDraggable(listEl, items, {
  animation: 150,
  handle: '.checklist-drag-handle'
})

onMounted(() => fetchItems())
</script>

<template>
  <div class="space-y-2">
    <div class="flex items-center justify-between">
      <p class="text-xs font-medium text-muted uppercase">Checklist</p>
      <span class="text-xs text-muted">{{ completedCount }}/{{ items.length }}</span>
    </div>

    <!-- Progress bar -->
    <UProgress v-if="items.length > 0" :value="completedCount" :max="items.length" size="xs" />

    <!-- Items -->
    <div ref="listEl" class="space-y-1 max-h-48 overflow-y-auto">
      <div
        v-for="item in items"
        :key="item.id"
        class="flex items-center gap-2 group"
      >
        <UIcon name="i-lucide-grip-vertical" class="checklist-drag-handle size-3 text-muted cursor-grab opacity-0 group-hover:opacity-100" />
        <UCheckbox :model-value="item.isCompleted" @change="toggleItem(item)" />
        <span class="text-sm flex-1" :class="{ 'line-through text-muted': item.isCompleted }">
          {{ item.text }}
        </span>
        <UButton
          icon="i-lucide-trash-2"
          variant="ghost"
          size="xs"
          color="neutral"
          class="opacity-0 group-hover:opacity-100"
          @click="deleteItem(item.id)"
        />
      </div>
    </div>

    <!-- Add item -->
    <form class="flex gap-2" @submit.prevent="addItem">
      <UInput v-model="newItemText" placeholder="Add item..." size="xs" class="flex-1" />
      <UButton type="submit" size="xs" :loading="adding">Add</UButton>
    </form>
  </div>
</template>
```

### Step 2: Wire checklist into CardModal

In `CardModal.vue`, add `CardChecklist` to both desktop right sidebar and mobile checklist tab:

Desktop sidebar (after `CardMetadata`):
```vue
<div class="w-64 flex-shrink-0 border-l overflow-y-auto p-4 space-y-6">
  <CardMetadata :card="card" />
  <USeparator />
  <CardChecklist :card-id="card.id" :project-id="projectId" />
</div>
```

Mobile checklist tab (replace placeholder):
```vue
<div v-else-if="activeTab === 'checklist'">
  <CardChecklist :card-id="card.id" :project-id="projectId" />
</div>
```

### Step 3: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open card → add checklist items → toggle complete → drag reorder → delete item

### Step 4: Commit

```bash
git add src/web-ui/app/components/card/CardChecklist.vue src/web-ui/app/components/card/CardModal.vue
git commit -m "feat: add card checklist with drag reorder and toggle"
```

---

## Task 13: Card Comments

**Files:**
- Create: `src/web-ui/app/components/card/CardComments.vue`
- Modify: `src/web-ui/app/components/card/CardModal.vue` (wire comments)

### Step 1: Create CardComments component

Create `src/web-ui/app/components/card/CardComments.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type CommentResponse = components['schemas']['CommentResponse']

const props = defineProps<{
  cardId: string
  projectId: string
}>()

const comments = ref<CommentResponse[]>([])
const loading = ref(true)
const newComment = ref('')
const posting = ref(false)

const api = useApi()

async function fetchComments() {
  loading.value = true
  try {
    const { data } = await api.GET('/api/projects/{projectId}/Cards/{cardId}/Comments', {
      params: { path: { projectId: props.projectId, cardId: props.cardId } }
    })
    comments.value = (data as CommentResponse[]) ?? []
  }
  catch { /* silently fail */ }
  finally { loading.value = false }
}

async function postComment() {
  if (!newComment.value.trim()) return
  posting.value = true
  try {
    const { data, error } = await api.POST('/api/projects/{projectId}/Cards/{cardId}/Comments', {
      params: { path: { projectId: props.projectId, cardId: props.cardId } },
      body: { content: newComment.value.trim() }
    })
    if (error) throw error
    comments.value.push(data as CommentResponse)
    newComment.value = ''
  }
  catch { /* toast */ }
  finally { posting.value = false }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

onMounted(() => fetchComments())
</script>

<template>
  <div class="space-y-4">
    <p class="text-xs font-medium text-muted uppercase">Comments</p>

    <!-- Comment list -->
    <div class="space-y-3 max-h-64 overflow-y-auto">
      <div v-if="loading" class="text-sm text-muted">Loading...</div>
      <div v-else-if="comments.length === 0" class="text-sm text-muted">No comments yet</div>

      <div v-for="comment in comments" :key="comment.id" class="flex gap-2">
        <UAvatar :alt="comment.authorUsername" size="sm" />
        <div class="flex-1 min-w-0">
          <div class="flex items-center gap-2">
            <span class="text-sm font-medium">{{ comment.authorUsername }}</span>
            <span class="text-xs text-muted">{{ formatDate(comment.createdAt) }}</span>
          </div>
          <div class="text-sm prose prose-sm max-w-none mt-1" v-html="comment.content" />
        </div>
      </div>
    </div>

    <!-- Comment input -->
    <form class="flex gap-2" @submit.prevent="postComment">
      <UTextarea v-model="newComment" placeholder="Write a comment..." :rows="2" class="flex-1" />
      <UButton type="submit" size="sm" :loading="posting">Post</UButton>
    </form>
  </div>
</template>
```

### Step 2: Wire comments into CardModal

Desktop left column (after `CardDescription`):
```vue
<div class="flex-1 overflow-y-auto p-4 space-y-6">
  <CardDescription :card="card" :project-id="projectId" />
  <USeparator />
  <CardComments :card-id="card.id" :project-id="projectId" />
</div>
```

Mobile comments tab (replace placeholder):
```vue
<div v-else-if="activeTab === 'comments'">
  <CardComments :card-id="card.id" :project-id="projectId" />
</div>
```

### Step 3: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open card → post comment → see comment in list → author avatar + timestamp

### Step 4: Commit

```bash
git add src/web-ui/app/components/card/CardComments.vue src/web-ui/app/components/card/CardModal.vue
git commit -m "feat: add card comments with post and list"
```

---

## Task 14: Card Attachments — Upload + Download

**Files:**
- Create: `src/web-ui/app/components/card/CardAttachments.vue`
- Modify: `src/web-ui/app/components/card/CardModal.vue` (wire attachments)

### Step 1: Create CardAttachments component

Create `src/web-ui/app/components/card/CardAttachments.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type AttachmentResponse = components['schemas']['AttachmentResponse']

const props = defineProps<{
  cardId: string
  projectId: string
}>()

const attachments = ref<AttachmentResponse[]>([])
const loading = ref(true)
const uploading = ref(false)

const api = useApi()
const config = useRuntimeConfig()
const fileInput = ref<HTMLInputElement>()

async function fetchAttachments() {
  loading.value = true
  try {
    const { data } = await api.GET('/api/projects/{projectId}/Cards/{cardId}/Attachments', {
      params: { path: { projectId: props.projectId, cardId: props.cardId } }
    })
    attachments.value = (data as AttachmentResponse[]) ?? []
  }
  catch { /* silently fail */ }
  finally { loading.value = false }
}

async function handleUpload(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('file', file)

    // openapi-fetch doesn't handle multipart well; use raw fetch
    const token = useAuthToken().getToken()
    const res = await fetch(`${config.public.apiBaseUrl}/api/projects/${props.projectId}/Cards/${props.cardId}/Attachments`, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: formData
    })
    if (!res.ok) throw new Error('Upload failed')
    const data = await res.json() as AttachmentResponse
    attachments.value.push(data)
  }
  catch { /* toast */ }
  finally {
    uploading.value = false
    input.value = ''
  }
}

async function deleteAttachment(attachmentId: string) {
  try {
    const { error } = await api.DELETE('/api/projects/{projectId}/Cards/{cardId}/Attachments/{attachmentId}', {
      params: { path: { projectId: props.projectId, cardId: props.cardId, attachmentId } }
    })
    if (error) throw error
    attachments.value = attachments.value.filter(a => a.id !== attachmentId)
  }
  catch { /* toast */ }
}

function downloadUrl(attachment: AttachmentResponse) {
  return `${config.public.apiBaseUrl}/api/projects/${props.projectId}/Cards/${props.cardId}/Attachments/${attachment.id}/download`
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

onMounted(() => fetchAttachments())
</script>

<template>
  <div class="space-y-2">
    <div class="flex items-center justify-between">
      <p class="text-xs font-medium text-muted uppercase">Attachments</p>
      <UButton size="xs" variant="outline" :loading="uploading" @click="fileInput?.click()">
        Upload
      </UButton>
      <input ref="fileInput" type="file" class="hidden" @change="handleUpload" />
    </div>

    <div class="space-y-1 max-h-48 overflow-y-auto">
      <div v-if="loading" class="text-xs text-muted">Loading...</div>
      <div v-else-if="attachments.length === 0" class="text-xs text-muted">No attachments</div>

      <div
        v-for="att in attachments"
        :key="att.id"
        class="flex items-center gap-2 group text-sm"
      >
        <UIcon name="i-lucide-paperclip" class="size-3 text-muted" />
        <a :href="downloadUrl(att)" class="flex-1 truncate hover:underline" download>
          {{ att.fileName }}
        </a>
        <span class="text-xs text-muted">{{ formatSize(att.fileSize) }}</span>
        <UButton
          icon="i-lucide-trash-2"
          variant="ghost"
          size="xs"
          color="neutral"
          class="opacity-0 group-hover:opacity-100"
          @click="deleteAttachment(att.id)"
        />
      </div>
    </div>
  </div>
</template>
```

### Step 2: Wire attachments into CardModal

Desktop right sidebar (after `CardChecklist`):
```vue
<USeparator />
<CardAttachments :card-id="card.id" :project-id="projectId" />
```

Mobile related tab — add to the related tab content.

### Step 3: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open card → upload file → see in list → click to download → delete

### Step 4: Commit

```bash
git add src/web-ui/app/components/card/CardAttachments.vue src/web-ui/app/components/card/CardModal.vue
git commit -m "feat: add card attachments with upload and download"
```

---

## Task 15: Card Dependencies Panel

**Files:**
- Create: `src/web-ui/app/components/card/CardDependencies.vue`
- Modify: `src/web-ui/app/components/card/CardModal.vue` (wire dependencies)

### Step 1: Create CardDependencies component

Create `src/web-ui/app/components/card/CardDependencies.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type CardRelationshipResponse = components['schemas']['CardRelationshipResponse']

const props = defineProps<{
  cardId: string
  projectId: string
}>()

const relationships = ref<CardRelationshipResponse[]>([])
const loading = ref(true)

const api = useApi()

async function fetchRelationships() {
  loading.value = true
  try {
    const { data } = await api.GET('/api/projects/{projectId}/Cards/{cardId}/Relationships', {
      params: { path: { projectId: props.projectId, cardId: props.cardId } }
    })
    relationships.value = (data as CardRelationshipResponse[]) ?? []
  }
  catch { /* silently fail */ }
  finally { loading.value = false }
}

const relationshipLabel: Record<string, string> = {
  BlockedBy: 'Blocked by',
  Precedes: 'Precedes',
  Relates: 'Relates to'
}

const relationshipColor: Record<string, string> = {
  BlockedBy: 'error',
  Precedes: 'warning',
  Relates: 'neutral'
}

onMounted(() => fetchRelationships())
</script>

<template>
  <div class="space-y-2">
    <p class="text-xs font-medium text-muted uppercase">Dependencies</p>

    <div class="space-y-1 max-h-48 overflow-y-auto">
      <div v-if="loading" class="text-xs text-muted">Loading...</div>
      <div v-else-if="relationships.length === 0" class="text-xs text-muted">No dependencies</div>

      <div
        v-for="rel in relationships"
        :key="rel.id"
        class="flex items-center gap-2 text-sm"
      >
        <UBadge :color="relationshipColor[rel.type] ?? 'neutral'" variant="subtle" size="xs">
          {{ relationshipLabel[rel.type] ?? rel.type }}
        </UBadge>
        <span class="truncate">{{ rel.relatedCardTitle }}</span>
      </div>
    </div>
  </div>
</template>
```

### Step 2: Wire dependencies into CardModal

Desktop right sidebar (after `CardAttachments`):
```vue
<USeparator />
<CardDependencies :card-id="card.id" :project-id="projectId" />
```

Mobile related tab — add to the related tab content.

### Step 3: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open card with dependencies → see relationship badges → BlockedBy shows error color

### Step 4: Commit

```bash
git add src/web-ui/app/components/card/CardDependencies.vue src/web-ui/app/components/card/CardModal.vue
git commit -m "feat: add card dependencies panel with relationship badges"
```

---

## Task 16: Card Metadata Editor — Type, Column, Assignees, Due Date

**Files:**
- Modify: `src/web-ui/app/components/card/CardMetadata.vue` (replace the read-only placeholder with an editor)
- Modify: `src/web-ui/app/components/card/CardModal.vue` (wire `@update:card` for `CardMetadata`, same as `CardDescription`)
- Modify: `src/web-ui/app/components/card/__tests__/CardMetadata.test.ts`

**Depends on:**
- `2026-06-25-phase-3-card-modal-hardening.md` Task 2 (`CardModal.vue` owns `card`/`version`, children emit `update:card`) — run that plan's Task 2 before this task, or `applyCardUpdate` will not exist on `CardModal.vue` yet.
- `2026-06-25-phase-3-card-modal-hardening.md` Task 3 (`lib/card-type.ts`, `lib/date.ts`) — gives this task `CARD_TYPE_OPTIONS`, `cardTypeOption`, `cardTypeToApiString`, `formatDueDate`, `isOverdue`.

**Interfaces:**
- Consumes: `CARD_TYPE_OPTIONS`, `cardTypeOption(type)`, `cardTypeToApiString(type)` from `~/lib/card-type`; `formatDueDate(dueAt)`, `isOverdue(dueAt)` from `~/lib/date`; `board.members` (`useBoardStore()`, already populated by `board.vue`'s `fetchMembers` call from Plan 2).
- Produces: `CardMetadata` emits `'update:card': [card: CardResponse]`, same contract as `CardDescription`.

There is no Column or Parent Epic editor in this task — column changes already happen via drag-and-drop on the board (`BoardCard`/`BoardColumn`, Plan 2), and `parentCardId` (Epic linking) has no dedicated UI anywhere yet; introducing one here would be new scope, not a fix. Both stay as the existing read-only "Column" row; "Parent Epic" is dropped from this task entirely (it was never wired to a real field — `card.parentEpicId` does not exist on `CardResponse`).

### Step 1: Rewrite `CardMetadata.vue`

Replace `src/web-ui/app/components/card/CardMetadata.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import { CARD_TYPE_OPTIONS, cardTypeOption, cardTypeToApiString } from '~/lib/card-type'
import { formatDueDate, isOverdue } from '~/lib/date'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  card: CardResponse
  projectId: string
  isArchived?: boolean
}>()

const emit = defineEmits<{
  'update:card': [card: CardResponse]
}>()

const api = useApi()
const board = useBoardStore()
const toast = useToast()

const columnName = computed(() => {
  const col = board.columns.find(c => c.id === props.card.columnId)
  return col?.name ?? props.card.columnId.slice(0, 8)
})

const typeOption = computed(() => cardTypeOption(props.card.type))
const savingType = ref(false)

const editingDueDate = ref(false)
const savingDueDate = ref(false)
const dueDateInput = ref(toDateInputValue(props.card.dueAt))

function toDateInputValue(dueAt: string | null): string {
  return dueAt ? dueAt.slice(0, 10) : ''
}

watch(() => props.card.dueAt, (val) => {
  if (!editingDueDate.value) dueDateInput.value = toDateInputValue(val)
})

const availableMembers = computed(() =>
  board.members.filter(m => !props.card.assignees.some(a => a.userId === m.userId))
)

async function persistCardFields(fields: { type?: string, dueAt?: string | null }) {
  try {
    const { data } = await api.PUT(ApiRoutes.Cards.update(props.projectId, props.card.id), {
      body: {
        title: props.card.title,
        description: props.card.description,
        type: fields.type ?? cardTypeToApiString(props.card.type),
        version: props.card.version,
        parentCardId: props.card.parentCardId,
        dueAt: fields.dueAt !== undefined ? fields.dueAt : props.card.dueAt
      }
    })
    if (data) emit('update:card', data as CardResponse)
    return true
  } catch {
    toast.add({ title: 'Failed to update card', color: 'error' })
    return false
  }
}

async function handleTypeChange(value: string) {
  if (props.isArchived || value === cardTypeToApiString(props.card.type)) return
  savingType.value = true
  await persistCardFields({ type: value })
  savingType.value = false
}

async function saveDueDate() {
  if (props.isArchived) return
  savingDueDate.value = true
  const dueAt = dueDateInput.value ? `${dueDateInput.value}T00:00:00Z` : null
  const ok = await persistCardFields({ dueAt })
  savingDueDate.value = false
  if (ok) editingDueDate.value = false
}

function cancelDueDateEdit() {
  dueDateInput.value = toDateInputValue(props.card.dueAt)
  editingDueDate.value = false
}

async function handleAssign(userId: string) {
  if (!userId || props.isArchived) return
  try {
    const { data } = await api.POST(ApiRoutes.Cards.assignees(props.projectId, props.card.id), {
      body: { assigneeUserId: userId }
    })
    if (data) emit('update:card', data as CardResponse)
  } catch {
    toast.add({ title: 'Failed to assign member', color: 'error' })
  }
}

async function handleUnassign(userId: string) {
  if (props.isArchived) return
  try {
    const { data } = await api.DELETE(ApiRoutes.Cards.removeAssignee(props.projectId, props.card.id, userId))
    if (data) emit('update:card', data as CardResponse)
  } catch {
    toast.add({ title: 'Failed to remove assignee', color: 'error' })
  }
}
</script>

<template>
  <div class="space-y-4">
    <!-- Type -->
    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1">Type</p>
      <USelect
        v-if="!isArchived"
        :model-value="typeOption.apiValue"
        :items="CARD_TYPE_OPTIONS.map(o => ({ label: o.label, value: o.apiValue }))"
        :loading="savingType"
        size="xs"
        class="w-32"
        @update:model-value="handleTypeChange"
      />
      <UBadge v-else :color="typeOption.color" :icon="typeOption.icon" variant="subtle">
        {{ typeOption.label }}
      </UBadge>
    </div>

    <!-- Column (read-only — moved via drag-and-drop on the board) -->
    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1">Column</p>
      <p class="text-sm">{{ columnName }}</p>
    </div>

    <!-- Assignees -->
    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1.5">Assignees</p>
      <div class="flex flex-wrap gap-1.5 mb-2">
        <span
          v-for="a in card.assignees"
          :key="a.userId"
          class="inline-flex items-center gap-1 px-2 py-0.5 text-xs rounded-full bg-primary/10 text-primary dark:bg-primary/20"
        >
          {{ a.username }}
          <button v-if="!isArchived" class="hover:text-red-500 leading-none" @click="handleUnassign(a.userId)">×</button>
        </span>
        <span v-if="!card.assignees?.length" class="text-xs text-muted">None</span>
      </div>
      <select
        v-if="!isArchived && availableMembers.length > 0"
        class="w-full px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
        @change="(e: Event) => { const s = e.target as HTMLSelectElement; if (s.value) handleAssign(s.value); s.value = '' }"
      >
        <option value="">+ Add assignee</option>
        <option v-for="m in availableMembers" :key="m.userId" :value="m.userId">{{ m.username }}</option>
      </select>
    </div>

    <!-- Due Date -->
    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1">Due Date</p>
      <div v-if="!isArchived && editingDueDate" class="flex items-center gap-1">
        <input
          v-model="dueDateInput"
          type="date"
          class="text-sm px-2 py-1 border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
        >
        <UButton icon="i-lucide-check" size="xs" variant="soft" :loading="savingDueDate" @click="saveDueDate" />
        <UButton icon="i-lucide-x" size="xs" variant="ghost" @click="cancelDueDateEdit" />
      </div>
      <button
        v-else
        type="button"
        class="text-sm text-left"
        :class="[isOverdue(card.dueAt) ? 'text-red-500 font-medium' : '', isArchived ? 'cursor-default' : 'hover:underline']"
        :disabled="isArchived"
        @click="editingDueDate = true"
      >
        {{ formatDueDate(card.dueAt) ?? 'None' }}
      </button>
    </div>
  </div>
</template>
```

### Step 2: Wire `CardModal.vue` to consume `CardMetadata`'s `update:card`

In `src/web-ui/app/components/card/CardModal.vue`, add `:is-archived="isArchived"` and `@update:card="applyCardUpdate"` to both `<CardMetadata>` instances (desktop sidebar and the mobile "Details" tab):

```vue
<CardMetadata
  :card="card"
  :project-id="projectId"
  :is-archived="isArchived"
  @update:card="applyCardUpdate"
/>
```

(`applyCardUpdate` already exists on `CardModal.vue` from the hardening plan's Task 2 — this task only adds the listener to the second consumer.)

### Step 3: Update the component test

Replace `src/web-ui/app/components/card/__tests__/CardMetadata.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CardMetadata from '~/components/card/CardMetadata.vue'
import { ApiRoutes } from '~/lib/routes'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

const mockPUT = vi.fn()
const mockPOST = vi.fn()
const mockDELETE = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: mockPUT,
  DELETE: mockDELETE
}))

function makeCard(overrides: Partial<CardResponse> = {}): CardResponse {
  return {
    id: 'c1', projectId: 'p1', columnId: 'col1', cardNumber: 1,
    title: 'Test', description: '', type: 0, position: 0,
    dueAt: null, version: 1,
    createdAt: '2024-01-01T00:00:00Z', updatedAt: '2024-01-01T00:00:00Z',
    movedAt: '2024-01-01T00:00:00Z', archivedAt: null, parentCardId: null,
    assignees: [], watchers: [],
    ...overrides
  }
}

describe('CardMetadata', () => {
  beforeEach(() => {
    mockPUT.mockReset()
    mockPOST.mockReset()
    mockDELETE.mockReset()
  })

  it('renders the type badge label for an archived card', async () => {
    const wrapper = await mountSuspended(CardMetadata, {
      props: { card: makeCard({ archivedAt: '2024-02-01T00:00:00Z' }), projectId: 'p1', isArchived: true }
    })
    expect(wrapper.text()).toContain('Task')
  })

  it('shows None for missing due date', async () => {
    const wrapper = await mountSuspended(CardMetadata, { props: { card: makeCard(), projectId: 'p1' } })
    expect(wrapper.text()).toContain('None')
  })

  it('sends the full update body including the current version on a type change', async () => {
    mockPUT.mockResolvedValue({ data: makeCard({ type: 'Bug', version: 2 }), error: undefined })
    const wrapper = await mountSuspended(CardMetadata, {
      props: { card: makeCard({ version: 5 }), projectId: 'p1' }
    })

    await wrapper.findComponent({ name: 'USelect' }).vm.$emit('update:model-value', 'Bug')
    await flushPromises()

    expect(mockPUT).toHaveBeenCalledWith(
      ApiRoutes.Cards.update('p1', 'c1'),
      expect.objectContaining({ body: expect.objectContaining({ title: 'Test', version: 5, type: 'Bug' }) })
    )
    expect(wrapper.emitted('update:card')![0]).toEqual([makeCard({ type: 'Bug', version: 2 })])
  })

  it('assigns a member and emits the updated card', async () => {
    mockPOST.mockResolvedValue({ data: makeCard({ assignees: [{ id: 'a1', userId: 'u1', username: 'Alice', assignedAt: '2024-01-01T00:00:00Z' }] }), error: undefined })
    const wrapper = await mountSuspended(CardMetadata, { props: { card: makeCard(), projectId: 'p1' } })

    await wrapper.find('select').setValue('u1')
    await flushPromises()

    expect(mockPOST).toHaveBeenCalledWith(ApiRoutes.Cards.assignees('p1', 'c1'), { body: { assigneeUserId: 'u1' } })
    expect(wrapper.emitted('update:card')).toBeTruthy()
  })
})
```

This test mounts the real `useBoardStore()` (not mocked) — `board.members`/`board.columns` default to empty arrays, which is exactly what makes the "Add assignee" `<select>` disappear when `availableMembers.length === 0`. The assignment test above needs at least one member in the store; add a `beforeEach` that does `useBoardStore().members = [{ userId: 'u1', username: 'Alice', role: 'Member', joinedAt: '2024-01-01T00:00:00Z' }]` (adjust to `MemberResponse`'s actual shape) before mounting, using `setActivePinia(createPinia())` first the same way `stores/__tests__/board.test.ts` does.

### Step 4: Verify

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test
```
Expected: zero errors, all tests pass.
Manual: open a card → change type → set a due date → assign/unassign a member → all persist after closing and reopening the modal; open the same card in two tabs and confirm editing type in one tab doesn't desync the other tab's due-date save (this is what Task 2 of the hardening plan fixes — exercise it here).

### Step 5: Commit

```bash
git add src/web-ui/app/components/card/CardMetadata.vue src/web-ui/app/components/card/CardModal.vue \
  src/web-ui/app/components/card/__tests__/CardMetadata.test.ts
git commit -m "feat: add card metadata editor for type, due date, and assignees"
```

---

## Task 17: Card Panel Component Tests

**Files:**
- Create: `src/web-ui/app/components/card/__tests__/CardChecklist.test.ts`
- Create: `src/web-ui/app/components/card/__tests__/CardComments.test.ts`
- Create: `src/web-ui/app/components/card/__tests__/CardAttachments.test.ts`
- Create: `src/web-ui/app/components/card/__tests__/CardDependencies.test.ts`

### Step 1: Write CardChecklist component test

Create `src/web-ui/app/components/card/__tests__/CardChecklist.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardChecklist from '~/components/card/CardChecklist.vue'

describe('CardChecklist', () => {
  it('renders checklist header', async () => {
    const wrapper = await mountSuspended(CardChecklist, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Checklist')
  })

  it('renders add item input', async () => {
    const wrapper = await mountSuspended(CardChecklist, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.find('input[placeholder="Add item..."]').exists()).toBe(true)
  })

  it('shows 0/0 count when no items', async () => {
    const wrapper = await mountSuspended(CardChecklist, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('0/0')
  })
})
```

### Step 2: Write CardComments component test

Create `src/web-ui/app/components/card/__tests__/CardComments.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardComments from '~/components/card/CardComments.vue'

describe('CardComments', () => {
  it('renders comments header', async () => {
    const wrapper = await mountSuspended(CardComments, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Comments')
  })

  it('renders comment input', async () => {
    const wrapper = await mountSuspended(CardComments, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.find('textarea[placeholder="Write a comment..."]').exists()).toBe(true)
  })

  it('shows empty state when no comments', async () => {
    const wrapper = await mountSuspended(CardComments, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('No comments yet')
  })
})
```

### Step 3: Write CardAttachments component test

Create `src/web-ui/app/components/card/__tests__/CardAttachments.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardAttachments from '~/components/card/CardAttachments.vue'

describe('CardAttachments', () => {
  it('renders attachments header', async () => {
    const wrapper = await mountSuspended(CardAttachments, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Attachments')
  })

  it('renders upload button', async () => {
    const wrapper = await mountSuspended(CardAttachments, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Upload')
  })

  it('shows empty state when no attachments', async () => {
    const wrapper = await mountSuspended(CardAttachments, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('No attachments')
  })
})
```

### Step 4: Write CardDependencies component test

Create `src/web-ui/app/components/card/__tests__/CardDependencies.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardDependencies from '~/components/card/CardDependencies.vue'

describe('CardDependencies', () => {
  it('renders dependencies header', async () => {
    const wrapper = await mountSuspended(CardDependencies, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Dependencies')
  })

  it('shows empty state when no dependencies', async () => {
    const wrapper = await mountSuspended(CardDependencies, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('No dependencies')
  })
})
```

### Step 5: Verify

- `cd src/web-ui && pnpm test` — all tests pass
- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors

### Step 6: Commit

```bash
git add src/web-ui/app/components/card/__tests__/CardChecklist.test.ts src/web-ui/app/components/card/__tests__/CardComments.test.ts src/web-ui/app/components/card/__tests__/CardAttachments.test.ts src/web-ui/app/components/card/__tests__/CardDependencies.test.ts
git commit -m "feat: add card panel component tests (checklist, comments, attachments, dependencies)"
```

---

## Task 18: Archive/Restore Project

**Files:**
- Modify: `src/web-ui/app/pages/projects/index.vue` (add archive/restore + filter toggle)
- Modify: `src/web-ui/app/components/project/ProjectList.vue` (add action buttons per row)

### Step 1: Add toggle + API calls in projects/index.vue

```vue
<div class="flex items-center gap-2 mb-4">
  <UToggle v-model="showArchived" />
  <span class="text-sm">Show archived</span>
</div>
```

```ts
const showArchived = ref(false)

async function fetchProjects() {
  const url = showArchived.value
    ? '/api/Projects?includeArchived=true'
    : '/api/Projects'
  const { data } = await api.GET(url)
  // ...
}

async function handleArchive(projectId: string) {
  await api.POST('/api/Projects/{projectId}/archive', { params: { path: { projectId } } })
  fetchProjects()
}

async function handleRestore(projectId: string) {
  await api.POST('/api/Projects/{projectId}/restore', { params: { path: { projectId } } })
  fetchProjects()
}
```

Wire to `ProjectList`:
```vue
<ProjectList
  :projects="projects"
  :loading="loading"
  @select="onProjectSelect"
  @archive="handleArchive"
  @restore="handleRestore"
/>
```

### Step 2: Add action buttons in ProjectList.vue

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

### Step 3: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open project list → archive a project → it disappears → toggle "show archived" → project appears with restore

### Step 4: Commit

```bash
git add src/web-ui/app/pages/projects/index.vue src/web-ui/app/components/project/ProjectList.vue
git commit -m "feat: add archive/restore project with filter toggle"
```

---

## Verification (Plan 4 Complete)

Reference `nuxt-verification` skill:
1. `cd src/web-ui && pnpm typecheck` — zero errors
2. `cd src/web-ui && pnpm lint` — zero errors
3. `cd src/web-ui && pnpm build` — successful production build
4. `cd src/web-ui && pnpm test` — all tests pass
5. Manual: open card → all panels render → checklist add/toggle → comment post → upload attachment → dependencies visible → metadata editable