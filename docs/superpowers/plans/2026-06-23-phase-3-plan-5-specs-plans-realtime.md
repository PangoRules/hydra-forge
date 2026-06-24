# Plan 5: Specs, Plans & Real-time — Spec/Plan Editors + SignalR BoardHub + PresenceHub
**Branch:** `task/phase-3-specs-plans-realtime`
**Parent branch:** `feat/phase-3-web-ui`
**Parent spec:** `2026-06-23-phase-3-web-ui-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Spec and plan editors with Tiptap + version history sidebar + restore. SignalR BoardHub integration for real-time board events. SignalR PresenceHub for online users and card focus indicators.

**Architecture:** Spec/Plan editors are modals with Tiptap editor (left) and collapsible version history sidebar (right). Save creates new version. Restore reverts title, description, content. `useRealtime` composable manages BoardHub connection lifecycle — joins project group on mount, dispatches `OnBoardEvent` to board store. `usePresence` composable manages PresenceHub — tracks online users per project, broadcasts card focus.

**Tech Stack:** @microsoft/signalr, Tiptap, Pinia

**Depends on:** Plan 3 (Card Modal Core) — needs CardModal for wiring spec/plan links. Plan 2 (Board) — needs board store for SignalR event dispatch.

**Spec ref:** Sections 3.5, 7, 9 (usePresenceStore), 10 (useRealtime, usePresence), 15

---

## Task 17: Spec Editor + Version History

**Files:**
- Create: `src/web-ui/app/components/spec/SpecEditor.vue`
- Create: `src/web-ui/app/components/spec/SpecVersionHistory.vue`
- Modify: `src/web-ui/app/components/card/CardModal.vue` (add specs section to related tab)

### Step 1: Create SpecVersionHistory component

Create `src/web-ui/app/components/spec/SpecVersionHistory.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type SpecVersionResponse = components['schemas']['SpecVersionResponse']

const props = defineProps<{
  specId: string
  projectId: string
}>()

const emit = defineEmits<{
  restore: [versionId: string]
}>()

const versions = ref<SpecVersionResponse[]>([])
const loading = ref(true)
const restoring = ref<string | null>(null)

const api = useApi()

async function fetchVersions() {
  loading.value = true
  try {
    const { data } = await api.GET('/api/projects/{projectId}/Specs/{specId}/Versions', {
      params: { path: { projectId: props.projectId, specId: props.specId } }
    })
    versions.value = (data as SpecVersionResponse[]) ?? []
  }
  catch { /* silently fail */ }
  finally { loading.value = false }
}

async function handleRestore(versionId: string) {
  restoring.value = versionId
  try {
    const { error } = await api.POST('/api/projects/{projectId}/Specs/{specId}/Versions/{versionId}/restore', {
      params: { path: { projectId: props.projectId, specId: props.specId, versionId } }
    })
    if (error) throw error
    emit('restore', versionId)
  }
  catch { /* toast */ }
  finally { restoring.value = null }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

onMounted(() => fetchVersions())
</script>

<template>
  <div class="space-y-2">
    <p class="text-xs font-medium text-muted uppercase">Version History</p>

    <div class="space-y-1 max-h-64 overflow-y-auto">
      <div v-if="loading" class="text-xs text-muted">Loading...</div>
      <div v-else-if="versions.length === 0" class="text-xs text-muted">No versions</div>

      <div
        v-for="(v, i) in versions"
        :key="v.id"
        class="flex items-center justify-between text-sm py-1"
      >
        <div class="min-w-0">
          <p class="truncate text-xs">v{{ versions.length - i }} — {{ formatDate(v.createdAt) }}</p>
          <p class="text-xs text-muted truncate">{{ v.authorUsername }}</p>
        </div>
        <UButton
          size="xs"
          variant="ghost"
          :loading="restoring === v.id"
          @click="handleRestore(v.id)"
        >
          Restore
        </UButton>
      </div>
    </div>
  </div>
</template>
```

### Step 2: Create SpecEditor component

Create `src/web-ui/app/components/spec/SpecEditor.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type SpecResponse = components['schemas']['SpecResponse']

const props = defineProps<{
  specId: string | null // null = create new
  cardId: string
  projectId: string
}>()

const emit = defineEmits<{
  close: []
  saved: []
}>()

const spec = ref<SpecResponse | null>(null)
const title = ref('')
const description = ref('')
const content = ref('')
const loading = ref(false)
const saving = ref(false)
const error = ref<string | null>(null)
const showHistory = ref(false)

const api = useApi()

async function fetchSpec() {
  if (!props.specId) return
  loading.value = true
  try {
    const { data } = await api.GET('/api/projects/{projectId}/Specs/{specId}', {
      params: { path: { projectId: props.projectId, specId: props.specId } }
    })
    spec.value = data as SpecResponse
    title.value = spec.value.title
    description.value = spec.value.description ?? ''
    content.value = spec.value.content
  }
  catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load spec'
  }
  finally { loading.value = false }
}

async function save() {
  saving.value = true
  error.value = null
  try {
    if (props.specId) {
      const { error: apiError } = await api.PUT('/api/projects/{projectId}/Specs/{specId}', {
        params: { path: { projectId: props.projectId, specId: props.specId } },
        body: { title: title.value, description: description.value, content: content.value }
      })
      if (apiError) throw apiError
    }
    else {
      const { error: apiError } = await api.POST('/api/projects/{projectId}/Specs', {
        params: { path: { projectId: props.projectId } },
        body: { title: title.value, description: description.value, content: content.value, cardId: props.cardId }
      })
      if (apiError) throw apiError
    }
    emit('saved')
  }
  catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to save spec'
  }
  finally { saving.value = false }
}

function onVersionRestored() {
  fetchSpec()
}

onMounted(() => fetchSpec())
</script>

<template>
  <UModal :open="true" @close="emit('close')" :ui="{ width: 'sm:max-w-4xl' }">
    <div class="flex flex-col max-h-[85vh]">
      <!-- Header -->
      <div class="flex items-center justify-between p-4 border-b">
        <h2 class="text-lg font-semibold">
          {{ props.specId ? 'Edit Spec' : 'New Spec' }}
        </h2>
        <div class="flex items-center gap-2">
          <UButton
            size="sm"
            variant="ghost"
            :label="showHistory ? 'Hide History' : 'History'"
            @click="showHistory = !showHistory"
          />
          <UButton size="sm" :loading="saving" @click="save">Save</UButton>
          <UButton icon="i-lucide-x" variant="ghost" size="sm" @click="emit('close')" />
        </div>
      </div>

      <div v-if="loading" class="flex items-center justify-center p-8">
        <UIcon name="i-lucide-loader" class="animate-spin size-8" />
      </div>

      <UAlert v-else-if="error" color="error" :title="error" />

      <div v-else class="flex flex-1 overflow-hidden">
        <!-- Editor -->
        <div class="flex-1 overflow-y-auto p-4 space-y-4">
          <UFormField label="Title">
            <UInput v-model="title" placeholder="Spec title" />
          </UFormField>

          <UFormField label="Description">
            <UTextarea v-model="description" placeholder="Brief description" :rows="2" />
          </UFormField>

          <div>
            <p class="text-xs font-medium text-muted uppercase mb-1">Content</p>
            <MarkdownEditor v-model="content" placeholder="Write your spec..." />
          </div>
        </div>

        <!-- Version History Sidebar -->
        <div v-if="showHistory && props.specId" class="w-56 flex-shrink-0 border-l overflow-y-auto p-4">
          <SpecVersionHistory
            :spec-id="props.specId"
            :project-id="projectId"
            @restore="onVersionRestored"
          />
        </div>
      </div>
    </div>
  </UModal>
</template>
```

### Step 3: Wire specs into CardModal related tab

In `CardModal.vue`, add a specs section to the mobile "Related" tab and desktop right sidebar:

```vue
<!-- Desktop right sidebar, after CardDependencies -->
<USeparator />
<div class="space-y-2">
  <div class="flex items-center justify-between">
    <p class="text-xs font-medium text-muted uppercase">Specs</p>
    <UButton size="xs" variant="ghost" @click="showSpecEditor = true">+</UButton>
  </div>
  <!-- Spec list — fetch and render linked specs -->
</div>
```

Add `showSpecEditor` ref and conditional `SpecEditor` modal.

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open card → create spec → edit content → save → version history → restore version

### Step 5: Commit

```bash
git add src/web-ui/app/components/spec/SpecEditor.vue src/web-ui/app/components/spec/SpecVersionHistory.vue src/web-ui/app/components/card/CardModal.vue
git commit -m "feat: add spec editor with Tiptap and version history"
```

---

## Task 18: Plan Editor + Version History

**Files:**
- Create: `src/web-ui/app/components/plan/PlanEditor.vue`
- Create: `src/web-ui/app/components/plan/PlanVersionHistory.vue`
- Modify: `src/web-ui/app/components/card/CardModal.vue` (add plans section)

### Step 1: Create PlanVersionHistory component

Create `src/web-ui/app/components/plan/PlanVersionHistory.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type PlanVersionResponse = components['schemas']['PlanVersionResponse']

const props = defineProps<{
  planId: string
  projectId: string
}>()

const emit = defineEmits<{
  restore: [versionId: string]
}>()

const versions = ref<PlanVersionResponse[]>([])
const loading = ref(true)
const restoring = ref<string | null>(null)

const api = useApi()

async function fetchVersions() {
  loading.value = true
  try {
    const { data } = await api.GET('/api/projects/{projectId}/Plans/{planId}/Versions', {
      params: { path: { projectId: props.projectId, planId: props.planId } }
    })
    versions.value = (data as PlanVersionResponse[]) ?? []
  }
  catch { /* silently fail */ }
  finally { loading.value = false }
}

async function handleRestore(versionId: string) {
  restoring.value = versionId
  try {
    const { error } = await api.POST('/api/projects/{projectId}/Plans/{planId}/Versions/{versionId}/restore', {
      params: { path: { projectId: props.projectId, planId: props.planId, versionId } }
    })
    if (error) throw error
    emit('restore', versionId)
  }
  catch { /* toast */ }
  finally { restoring.value = null }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

onMounted(() => fetchVersions())
</script>

<template>
  <div class="space-y-2">
    <p class="text-xs font-medium text-muted uppercase">Version History</p>

    <div class="space-y-1 max-h-64 overflow-y-auto">
      <div v-if="loading" class="text-xs text-muted">Loading...</div>
      <div v-else-if="versions.length === 0" class="text-xs text-muted">No versions</div>

      <div
        v-for="(v, i) in versions"
        :key="v.id"
        class="flex items-center justify-between text-sm py-1"
      >
        <div class="min-w-0">
          <p class="truncate text-xs">v{{ versions.length - i }} — {{ formatDate(v.createdAt) }}</p>
          <p class="text-xs text-muted truncate">{{ v.authorUsername }}</p>
        </div>
        <UButton
          size="xs"
          variant="ghost"
          :loading="restoring === v.id"
          @click="handleRestore(v.id)"
        >
          Restore
        </UButton>
      </div>
    </div>
  </div>
</template>
```

### Step 2: Create PlanEditor component

Create `src/web-ui/app/components/plan/PlanEditor.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'

type PlanResponse = components['schemas']['PlanResponse']

const props = defineProps<{
  planId: string | null
  cardId: string
  projectId: string
}>()

const emit = defineEmits<{
  close: []
  saved: []
}>()

const plan = ref<PlanResponse | null>(null)
const title = ref('')
const description = ref('')
const content = ref('')
const loading = ref(false)
const saving = ref(false)
const error = ref<string | null>(null)
const showHistory = ref(false)

const api = useApi()

async function fetchPlan() {
  if (!props.planId) return
  loading.value = true
  try {
    const { data } = await api.GET('/api/projects/{projectId}/Plans/{planId}', {
      params: { path: { projectId: props.projectId, planId: props.planId } }
    })
    plan.value = data as PlanResponse
    title.value = plan.value.title
    description.value = plan.value.description ?? ''
    content.value = plan.value.content
  }
  catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load plan'
  }
  finally { loading.value = false }
}

async function save() {
  saving.value = true
  error.value = null
  try {
    if (props.planId) {
      const { error: apiError } = await api.PUT('/api/projects/{projectId}/Plans/{planId}', {
        params: { path: { projectId: props.projectId, planId: props.planId } },
        body: { title: title.value, description: description.value, content: content.value }
      })
      if (apiError) throw apiError
    }
    else {
      const { error: apiError } = await api.POST('/api/projects/{projectId}/Plans', {
        params: { path: { projectId: props.projectId } },
        body: { title: title.value, description: description.value, content: content.value, cardId: props.cardId }
      })
      if (apiError) throw apiError
    }
    emit('saved')
  }
  catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to save plan'
  }
  finally { saving.value = false }
}

function onVersionRestored() {
  fetchPlan()
}

onMounted(() => fetchPlan())
</script>

<template>
  <UModal :open="true" @close="emit('close')" :ui="{ width: 'sm:max-w-4xl' }">
    <div class="flex flex-col max-h-[85vh]">
      <div class="flex items-center justify-between p-4 border-b">
        <h2 class="text-lg font-semibold">
          {{ props.planId ? 'Edit Plan' : 'New Plan' }}
        </h2>
        <div class="flex items-center gap-2">
          <UButton
            size="sm"
            variant="ghost"
            :label="showHistory ? 'Hide History' : 'History'"
            @click="showHistory = !showHistory"
          />
          <UButton size="sm" :loading="saving" @click="save">Save</UButton>
          <UButton icon="i-lucide-x" variant="ghost" size="sm" @click="emit('close')" />
        </div>
      </div>

      <div v-if="loading" class="flex items-center justify-center p-8">
        <UIcon name="i-lucide-loader" class="animate-spin size-8" />
      </div>

      <UAlert v-else-if="error" color="error" :title="error" />

      <div v-else class="flex flex-1 overflow-hidden">
        <div class="flex-1 overflow-y-auto p-4 space-y-4">
          <UFormField label="Title">
            <UInput v-model="title" placeholder="Plan title" />
          </UFormField>

          <UFormField label="Description">
            <UTextarea v-model="description" placeholder="Brief description" :rows="2" />
          </UFormField>

          <div>
            <p class="text-xs font-medium text-muted uppercase mb-1">Steps</p>
            <MarkdownEditor v-model="content" placeholder="1. First step&#10;2. Second step..." />
          </div>
        </div>

        <div v-if="showHistory && props.planId" class="w-56 flex-shrink-0 border-l overflow-y-auto p-4">
          <PlanVersionHistory
            :plan-id="props.planId"
            :project-id="projectId"
            @restore="onVersionRestored"
          />
        </div>
      </div>
    </div>
  </UModal>
</template>
```

### Step 3: Wire plans into CardModal related tab

Same pattern as specs — add plans section to desktop right sidebar and mobile related tab.

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open card → create plan → edit steps → save → version history → restore

### Step 5: Commit

```bash
git add src/web-ui/app/components/plan/PlanEditor.vue src/web-ui/app/components/plan/PlanVersionHistory.vue src/web-ui/app/components/card/CardModal.vue
git commit -m "feat: add plan editor with Tiptap and version history"
```

---

## Task 19: SignalR BoardHub Integration + useRealtime

**Files:**
- Create: `src/web-ui/app/composables/useRealtime.ts`
- Modify: `src/web-ui/app/stores/board.ts` (add SignalR event handlers)
- Modify: `src/web-ui/app/pages/projects/[id]/board.vue` (connect on mount)

### Step 1: Install SignalR client

```bash
cd src/web-ui && pnpm add @microsoft/signalr
```

### Step 2: Create useRealtime composable

Create `src/web-ui/app/composables/useRealtime.ts`:

```ts
import * as signalR from '@microsoft/signalr'

export function useRealtime() {
  const config = useRuntimeConfig()
  const { getToken } = useAuthToken()
  const board = useBoardStore()

  let connection: signalR.HubConnection | null = null
  const isConnected = ref(false)
  const isReconnecting = ref(false)

  async function connect(projectId: string) {
    const token = getToken()
    if (!token) return

    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${config.public.apiBaseUrl}/hubs/board`, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build()

    connection.on('OnBoardEvent', (envelope: {
      eventId: string
      projectId: string
      entityType: string
      entityId: string
      action: string
      version: number
      occurredAt: string
      payload: unknown
    }) => {
      // Dispatch to board store — re-fetch board on any event
      if (envelope.projectId === projectId) {
        board.fetchBoard(projectId)
      }
    })

    connection.onreconnecting(() => {
      isReconnecting.value = true
    })

    connection.onreconnected(() => {
      isReconnecting.value = false
      connection?.invoke('JoinProject', projectId)
    })

    connection.onclose(() => {
      isConnected.value = false
    })

    try {
      await connection.start()
      isConnected.value = true
      await connection.invoke('JoinProject', projectId)
    }
    catch (e) {
      console.error('SignalR connection failed', e)
    }
  }

  async function disconnect(projectId: string) {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke('LeaveProject', projectId)
      }
      catch { /* best effort */ }
      await connection.stop()
    }
    connection = null
    isConnected.value = false
    isReconnecting.value = false
  }

  return { connect, disconnect, isConnected, isReconnecting }
}
```

### Step 3: Wire into board page

Modify `src/web-ui/app/pages/projects/[id]/board.vue`:

```vue
<script setup lang="ts">
// ... existing imports ...

const realtime = useRealtime()

onMounted(() => {
  board.fetchBoard(projectId)
  realtime.connect(projectId)
})

onBeforeUnmount(() => {
  realtime.disconnect(projectId)
})
</script>
```

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open board in two browser tabs → move card in one → other tab updates via SignalR

### Step 5: Commit

```bash
git add src/web-ui/package.json src/web-ui/pnpm-lock.yaml src/web-ui/app/composables/useRealtime.ts src/web-ui/app/stores/board.ts src/web-ui/app/pages/projects/[id]/board.vue
git commit -m "feat: add SignalR BoardHub integration with real-time board sync"
```

---

## Task 20: SignalR PresenceHub Integration + usePresence

**Files:**
- Create: `src/web-ui/app/composables/usePresence.ts`
- Create: `src/web-ui/app/stores/presence.ts`
- Modify: `src/web-ui/app/pages/projects/[id]/board.vue` (connect presence)

### Step 1: Create usePresenceStore

Create `src/web-ui/app/stores/presence.ts`:

```ts
import { defineStore } from 'pinia'

interface PresenceUser {
  userId: string
  username: string
  connectionId: string
}

interface CardFocus {
  userId: string
  cardId: string
}

export const usePresenceStore = defineStore('presence', () => {
  const onlineUsers = ref<Map<string, PresenceUser[]>>(new Map())
  const focusedCards = ref<Map<string, string>>(new Map()) // userId -> cardId

  function setProjectUsers(projectId: string, users: PresenceUser[]) {
    onlineUsers.value.set(projectId, users)
  }

  function addUser(projectId: string, user: PresenceUser) {
    const users = onlineUsers.value.get(projectId) ?? []
    if (!users.find(u => u.userId === user.userId)) {
      users.push(user)
      onlineUsers.value.set(projectId, users)
    }
  }

  function removeUser(projectId: string, connectionId: string) {
    const users = onlineUsers.value.get(projectId) ?? []
    onlineUsers.value.set(projectId, users.filter(u => u.connectionId !== connectionId))
  }

  function setCardFocus(userId: string, cardId: string) {
    focusedCards.value.set(userId, cardId)
  }

  function clearCardFocus(userId: string) {
    focusedCards.value.delete(userId)
  }

  return { onlineUsers, focusedCards, setProjectUsers, addUser, removeUser, setCardFocus, clearCardFocus }
})
```

### Step 2: Create usePresence composable

Create `src/web-ui/app/composables/usePresence.ts`:

```ts
import * as signalR from '@microsoft/signalr'

export function usePresence() {
  const config = useRuntimeConfig()
  const { getToken } = useAuthToken()
  const store = usePresenceStore()

  let connection: signalR.HubConnection | null = null

  async function connect(projectId: string) {
    const token = getToken()
    if (!token) return

    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${config.public.apiBaseUrl}/hubs/presence`, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build()

    connection.on('UserJoined', (user: { userId: string; username: string; connectionId: string }) => {
      store.addUser(projectId, user)
    })

    connection.on('UserLeft', (user: { userId: string; username: string; connectionId: string }) => {
      store.removeUser(projectId, user.connectionId)
    })

    connection.on('CardFocused', (data: { userId: string; cardId: string }) => {
      store.setCardFocus(data.userId, data.cardId)
    })

    try {
      await connection.start()
      await connection.invoke('JoinProject', projectId)
    }
    catch (e) {
      console.error('Presence connection failed', e)
    }
  }

  async function focusCard(projectId: string, cardId: string) {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      await connection.invoke('FocusCard', projectId, cardId)
    }
  }

  async function disconnect(projectId: string) {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke('LeaveProject', projectId)
      }
      catch { /* best effort */ }
      await connection.stop()
    }
    connection = null
  }

  return { connect, disconnect, focusCard }
}
```

### Step 3: Wire presence into board page

Modify `src/web-ui/app/pages/projects/[id]/board.vue`:

```vue
<script setup lang="ts">
// ... existing imports ...

const realtime = useRealtime()
const presence = usePresence()

onMounted(() => {
  board.fetchBoard(projectId)
  realtime.connect(projectId)
  presence.connect(projectId)
})

onBeforeUnmount(() => {
  realtime.disconnect(projectId)
  presence.disconnect(projectId)
})

// Focus card when modal opens
watch(selectedCardId, (cardId) => {
  if (cardId) {
    presence.focusCard(projectId, cardId)
  }
})
</script>
```

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open board in two tabs → see online users → open card → other tab sees focus indicator

### Step 5: Commit

```bash
git add src/web-ui/app/composables/usePresence.ts src/web-ui/app/stores/presence.ts src/web-ui/app/pages/projects/[id]/board.vue
git commit -m "feat: add SignalR PresenceHub with online users and card focus"
```

---

## Verification (Plan 5 Complete)

Reference `nuxt-verification` skill:
1. `cd src/web-ui && pnpm typecheck` — zero errors
2. `cd src/web-ui && pnpm lint` — zero errors
3. `cd src/web-ui && pnpm build` — successful production build
4. Manual: create/edit spec → version history → restore. Create/edit plan → same.
5. Manual: two browser tabs → real-time board sync. Presence shows online users.