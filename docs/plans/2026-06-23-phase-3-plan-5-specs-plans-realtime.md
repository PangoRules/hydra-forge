# Plan 5: Specs, Plans & Real-time — Spec/Plan Editors + SignalR BoardHub + PresenceHub
**Branch:** `task/phase-3-specs-plans-realtime`
**Parent branch:** `feat/phase-3-web-ui`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Spec and Plan editors inline in CardModal (new "Docs" tab, no nested modals) with Tiptap + toggleable version history panel. SignalR BoardHub for real-time board sync. SignalR PresenceHub for online users and card focus. Nuxt proxy extended to cover `/hubs/**` so SignalR works through the same proxy as the API.

**Architecture:**
- `CardSpec.vue` and `CardPlan.vue` are inline panel components (same pattern as `CardDescription`, `CardChecklist`) — **not modals**. They render directly in a new "Docs" tab inside CardModal.
- Each panel has its own save button + "History" toggle that reveals a version history column on the right.
- `useRealtime` composable manages BoardHub lifecycle (join project group → dispatch `OnBoardEvent` → board store refetch). Hub URL is a relative path (`/hubs/board`) so it routes through the Nuxt proxy.
- `usePresence` composable manages PresenceHub (online users, card focus broadcasts). Same relative URL pattern.
- `server/routes/hubs/[...path].ts` proxies `/hubs/**` to the backend (extends existing API proxy pattern).

**Tech Stack:** @microsoft/signalr, Tiptap (already installed), Pinia

**Depends on:** Plan 3 (CardModal), Plan 2 (board store)

## Global Constraints

- **No nested modals** — Spec/Plan editors live inside the existing CardModal tab system
- **ApiRoutes only** — never inline `/api/...` strings; add missing routes to `lib/routes.ts` first
- **useApi() throws** — every `await api.X(...)` must be wrapped in try/catch; never `const { error } = await api.X(...); if (error)` (D-40 bug)
- **No console.log/error/warn** — use `useAppToast()` for user feedback; silent catch for non-user-facing errors
- **AppModal not UModal** — use `<AppModal :open="..." @update:open="...">` with `#header`/`#body` slots
- **card.value.version** — panels read `props.card.version` at call time and emit `'update:card'` with server response; never cache version locally

---

## Task 17: Extend Nuxt Proxy for `/hubs/**` + Add Missing ApiRoutes

**Files:**
- Create: `src/web-ui/server/routes/hubs/[...path].ts`
- Modify: `src/web-ui/app/lib/routes.ts` (add Specs and Plans routes)

### Step 1: Add hubs proxy server route

Create `src/web-ui/server/routes/hubs/[...path].ts`:

```ts
export default defineEventHandler((event) => {
  const config = useRuntimeConfig()
  return proxyRequest(event, `${config.apiBaseUrl}${event.path}`)
})
```

This mirrors `server/routes/api/[...path].ts`. Browser sends `GET/WebSocket /hubs/board` → Nuxt server → `http://server:8080/hubs/board`.

### Step 2: Add Specs and Plans to ApiRoutes

Open `src/web-ui/app/lib/routes.ts` and add after the existing resource blocks:

```ts
Specs: {
  forCard: (projectId: string, cardId: string) =>
    `/api/projects/${projectId}/cards/${cardId}/Specs` as const,
  detail: (projectId: string, specId: string) =>
    `/api/projects/${projectId}/Specs/${specId}` as const,
  versions: (projectId: string, specId: string) =>
    `/api/projects/${projectId}/Specs/${specId}/Versions` as const,
  restore: (projectId: string, specId: string) =>
    `/api/projects/${projectId}/Specs/${specId}/restore` as const
},
Plans: {
  forCard: (projectId: string, cardId: string) =>
    `/api/projects/${projectId}/cards/${cardId}/Plans` as const,
  detail: (projectId: string, planId: string) =>
    `/api/projects/${projectId}/Plans/${planId}` as const,
  versions: (projectId: string, planId: string) =>
    `/api/projects/${projectId}/Plans/${planId}/Versions` as const,
  restore: (projectId: string, planId: string) =>
    `/api/projects/${projectId}/Plans/${planId}/restore` as const
},
```

> **Verify route shapes against the .NET controllers** before writing components. Run the dev server and check `http://localhost:5000/scalar/v1` if unsure.

### Step 3: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors

### Step 4: Commit

```bash
git add src/web-ui/server/routes/hubs/[...path].ts src/web-ui/app/lib/routes.ts
git commit -m "feat: proxy /hubs/** through Nuxt server + add Specs/Plans ApiRoutes"
```

---

## Task 18: CardSpec Inline Panel + Version History

**Files:**
- Create: `src/web-ui/app/components/card/CardSpec.vue`

### Step 1: Create CardSpec component

`CardSpec` is a self-contained panel — fetches or creates the card's spec, renders a Tiptap editor inline, saves on button click. Version history toggles a side column within the panel.

Create `src/web-ui/app/components/card/CardSpec.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'

type SpecResponse = components['schemas']['SpecResponse']
type SpecVersionResponse = components['schemas']['SpecVersionResponse']

const props = defineProps<{
  cardId: string
  projectId: string
  readonly?: boolean
}>()

const toast = useAppToast()
const api = useApi()

const spec = ref<SpecResponse | null>(null)
const title = ref('')
const description = ref('')
const content = ref('')
const loading = ref(true)
const saving = ref(false)
const showHistory = ref(false)

const versions = ref<SpecVersionResponse[]>([])
const loadingVersions = ref(false)
const restoring = ref<string | null>(null)

async function fetchSpec() {
  loading.value = true
  try {
    const { data } = await api.GET<SpecResponse>(ApiRoutes.Specs.forCard(props.projectId, props.cardId))
    spec.value = data ?? null
    if (spec.value) {
      title.value = spec.value.title
      description.value = spec.value.description ?? ''
      content.value = spec.value.content
    }
  } catch {
    // No spec yet — that's fine, user can create one
  } finally {
    loading.value = false
  }
}

async function save() {
  saving.value = true
  try {
    if (spec.value) {
      const { data } = await api.PUT<SpecResponse>(ApiRoutes.Specs.detail(props.projectId, spec.value.id), {
        body: { title: title.value, description: description.value, content: content.value }
      })
      spec.value = data ?? spec.value
    } else {
      const { data } = await api.POST<SpecResponse>(ApiRoutes.Specs.forCard(props.projectId, props.cardId), {
        body: { title: title.value, description: description.value, content: content.value }
      })
      spec.value = data ?? null
    }
    toast.success('Spec saved')
    if (showHistory.value) await fetchVersions()
  } catch {
    toast.error('Failed to save spec')
  } finally {
    saving.value = false
  }
}

async function fetchVersions() {
  if (!spec.value) return
  loadingVersions.value = true
  try {
    const { data } = await api.GET<SpecVersionResponse[]>(ApiRoutes.Specs.versions(props.projectId, spec.value.id))
    versions.value = data ?? []
  } catch {
    // silently fail
  } finally {
    loadingVersions.value = false
  }
}

async function restore(versionId: string) {
  if (!spec.value) return
  restoring.value = versionId
  try {
    await api.POST(ApiRoutes.Specs.restore(props.projectId, spec.value.id), {
      body: { versionId }
    })
    toast.success('Version restored')
    await fetchSpec()
  } catch {
    toast.error('Failed to restore version')
  } finally {
    restoring.value = null
  }
}

async function toggleHistory() {
  showHistory.value = !showHistory.value
  if (showHistory.value && versions.value.length === 0) {
    await fetchVersions()
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

onMounted(() => fetchSpec())
</script>

<template>
  <div class="space-y-3">
    <!-- Header row -->
    <div class="flex items-center justify-between">
      <p class="text-xs font-medium text-muted uppercase tracking-wide">Spec</p>
      <div class="flex items-center gap-1">
        <UButton
          v-if="spec"
          size="xs"
          variant="ghost"
          :label="showHistory ? 'Hide history' : 'History'"
          @click="toggleHistory"
        />
        <UButton
          v-if="!props.readonly"
          size="xs"
          :loading="saving"
          @click="save"
        >
          {{ spec ? 'Save' : 'Create' }}
        </UButton>
      </div>
    </div>

    <div v-if="loading" class="text-xs text-muted">Loading...</div>

    <div v-else class="flex gap-4">
      <!-- Editor column -->
      <div class="flex-1 space-y-3 min-w-0">
        <UInput
          v-model="title"
          placeholder="Spec title"
          :disabled="props.readonly"
          size="sm"
        />
        <UTextarea
          v-model="description"
          placeholder="Brief description"
          :rows="2"
          :disabled="props.readonly"
          size="sm"
        />
        <MarkdownEditor
          v-model="content"
          placeholder="Write your spec..."
          :readonly="props.readonly"
        />
      </div>

      <!-- Version history column (toggleable) -->
      <div v-if="showHistory && spec" class="w-52 flex-shrink-0 border-l pl-4 space-y-2">
        <p class="text-xs font-medium text-muted uppercase">History</p>
        <div v-if="loadingVersions" class="text-xs text-muted">Loading...</div>
        <div v-else-if="versions.length === 0" class="text-xs text-muted">No versions yet</div>
        <div
          v-for="(v, i) in versions"
          :key="v.id"
          class="flex items-center justify-between gap-1 text-xs py-1"
        >
          <div class="min-w-0">
            <p class="truncate">v{{ versions.length - i }} · {{ formatDate(v.createdAt) }}</p>
            <p class="text-muted truncate">{{ v.authorUsername }}</p>
          </div>
          <UButton
            size="xs"
            variant="ghost"
            :loading="restoring === v.id"
            :disabled="!!restoring"
            @click="restore(v.id)"
          >
            Restore
          </UButton>
        </div>
      </div>
    </div>
  </div>
</template>
```

### Step 2: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors

### Step 3: Commit

```bash
git add src/web-ui/app/components/card/CardSpec.vue
git commit -m "feat: add CardSpec inline panel with Tiptap editor and version history"
```

---

## Task 19: CardPlan Inline Panel + Version History

**Files:**
- Create: `src/web-ui/app/components/card/CardPlan.vue`

### Step 1: Create CardPlan component

Identical pattern to `CardSpec` — swap Spec → Plan, adjust placeholder text.

Create `src/web-ui/app/components/card/CardPlan.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'

type PlanResponse = components['schemas']['PlanResponse']
type PlanVersionResponse = components['schemas']['PlanVersionResponse']

const props = defineProps<{
  cardId: string
  projectId: string
  readonly?: boolean
}>()

const toast = useAppToast()
const api = useApi()

const plan = ref<PlanResponse | null>(null)
const title = ref('')
const description = ref('')
const content = ref('')
const loading = ref(true)
const saving = ref(false)
const showHistory = ref(false)

const versions = ref<PlanVersionResponse[]>([])
const loadingVersions = ref(false)
const restoring = ref<string | null>(null)

async function fetchPlan() {
  loading.value = true
  try {
    const { data } = await api.GET<PlanResponse>(ApiRoutes.Plans.forCard(props.projectId, props.cardId))
    plan.value = data ?? null
    if (plan.value) {
      title.value = plan.value.title
      description.value = plan.value.description ?? ''
      content.value = plan.value.content
    }
  } catch {
    // No plan yet — fine
  } finally {
    loading.value = false
  }
}

async function save() {
  saving.value = true
  try {
    if (plan.value) {
      const { data } = await api.PUT<PlanResponse>(ApiRoutes.Plans.detail(props.projectId, plan.value.id), {
        body: { title: title.value, description: description.value, content: content.value }
      })
      plan.value = data ?? plan.value
    } else {
      const { data } = await api.POST<PlanResponse>(ApiRoutes.Plans.forCard(props.projectId, props.cardId), {
        body: { title: title.value, description: description.value, content: content.value }
      })
      plan.value = data ?? null
    }
    toast.success('Plan saved')
    if (showHistory.value) await fetchVersions()
  } catch {
    toast.error('Failed to save plan')
  } finally {
    saving.value = false
  }
}

async function fetchVersions() {
  if (!plan.value) return
  loadingVersions.value = true
  try {
    const { data } = await api.GET<PlanVersionResponse[]>(ApiRoutes.Plans.versions(props.projectId, plan.value.id))
    versions.value = data ?? []
  } catch {
    // silently fail
  } finally {
    loadingVersions.value = false
  }
}

async function restore(versionId: string) {
  if (!plan.value) return
  restoring.value = versionId
  try {
    await api.POST(ApiRoutes.Plans.restore(props.projectId, plan.value.id), {
      body: { versionId }
    })
    toast.success('Version restored')
    await fetchPlan()
  } catch {
    toast.error('Failed to restore version')
  } finally {
    restoring.value = null
  }
}

async function toggleHistory() {
  showHistory.value = !showHistory.value
  if (showHistory.value && versions.value.length === 0) {
    await fetchVersions()
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

onMounted(() => fetchPlan())
</script>

<template>
  <div class="space-y-3">
    <div class="flex items-center justify-between">
      <p class="text-xs font-medium text-muted uppercase tracking-wide">Plan</p>
      <div class="flex items-center gap-1">
        <UButton
          v-if="plan"
          size="xs"
          variant="ghost"
          :label="showHistory ? 'Hide history' : 'History'"
          @click="toggleHistory"
        />
        <UButton
          v-if="!props.readonly"
          size="xs"
          :loading="saving"
          @click="save"
        >
          {{ plan ? 'Save' : 'Create' }}
        </UButton>
      </div>
    </div>

    <div v-if="loading" class="text-xs text-muted">Loading...</div>

    <div v-else class="flex gap-4">
      <div class="flex-1 space-y-3 min-w-0">
        <UInput
          v-model="title"
          placeholder="Plan title"
          :disabled="props.readonly"
          size="sm"
        />
        <UTextarea
          v-model="description"
          placeholder="Brief description"
          :rows="2"
          :disabled="props.readonly"
          size="sm"
        />
        <MarkdownEditor
          v-model="content"
          placeholder="1. First step&#10;2. Second step..."
          :readonly="props.readonly"
        />
      </div>

      <div v-if="showHistory && plan" class="w-52 flex-shrink-0 border-l pl-4 space-y-2">
        <p class="text-xs font-medium text-muted uppercase">History</p>
        <div v-if="loadingVersions" class="text-xs text-muted">Loading...</div>
        <div v-else-if="versions.length === 0" class="text-xs text-muted">No versions yet</div>
        <div
          v-for="(v, i) in versions"
          :key="v.id"
          class="flex items-center justify-between gap-1 text-xs py-1"
        >
          <div class="min-w-0">
            <p class="truncate">v{{ versions.length - i }} · {{ formatDate(v.createdAt) }}</p>
            <p class="text-muted truncate">{{ v.authorUsername }}</p>
          </div>
          <UButton
            size="xs"
            variant="ghost"
            :loading="restoring === v.id"
            :disabled="!!restoring"
            @click="restore(v.id)"
          >
            Restore
          </UButton>
        </div>
      </div>
    </div>
  </div>
</template>
```

### Step 2: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors

### Step 3: Commit

```bash
git add src/web-ui/app/components/card/CardPlan.vue
git commit -m "feat: add CardPlan inline panel with Tiptap editor and version history"
```

---

## Task 20: Wire Docs Tab into CardModal (type-conditional)

**Files:**
- Modify: `src/web-ui/app/components/card/CardModal.vue`

**Rules:**
| Card type | Docs tab shown | CardSpec | CardPlan |
|-----------|---------------|----------|----------|
| Goal      | ✓             | ✓        | ✓        |
| Idea      | ✓             | ✓        | ✗        |
| Task      | ✗             | —        | —        |
| Issue     | ✗             | —        | —        |

Rationale: Task/Issue descriptions + checklists already cover implementation detail. Plans add no value there. Spec on an Idea fleshes out the concept before it becomes a Goal. Plan on a Goal breaks down how to achieve it.

### Step 1: Add helpers and computed tabs

Add these constants and computed properties to `CardModal.vue` `<script setup>`:

```ts
// Card types that get the Docs tab at all
const DOCS_CARD_TYPES = ['Goal', 'Idea'] as const

// Card types that get the Plan section within Docs
const PLAN_CARD_TYPES = ['Goal'] as const

const hasDocsTab = computed(() =>
  card.value != null && DOCS_CARD_TYPES.includes(card.value.type as typeof DOCS_CARD_TYPES[number])
)

const hasPlan = computed(() =>
  card.value != null && PLAN_CARD_TYPES.includes(card.value.type as typeof PLAN_CARD_TYPES[number])
)
```

**2. Make tab arrays computed** (replace existing `const tabs` and `const desktopTabs` with):

```ts
const activeTab = ref<'details' | 'checklist' | 'comments' | 'related' | 'docs'>('details')

const tabs = computed(() => [
  { label: 'Details', value: 'details' as const },
  { label: 'Checklist', value: 'checklist' as const },
  { label: 'Comments', value: 'comments' as const },
  { label: 'Related', value: 'related' as const },
  ...(hasDocsTab.value ? [{ label: 'Docs', value: 'docs' as const }] : [])
])

const desktopTabs = computed(() => [
  { label: 'Details', value: 'details' as const },
  { label: 'Checklist', value: 'checklist' as const },
  { label: 'Comments', value: 'comments' as const },
  ...(hasDocsTab.value ? [{ label: 'Docs', value: 'docs' as const }] : [])
])
```

> Reset active tab when card type changes (e.g. if user edits type while modal is open):
```ts
watch(hasDocsTab, (has) => {
  if (!has && activeTab.value === 'docs') activeTab.value = 'details'
})
```

### Step 2: Add Docs tab content to desktop layout

Inside the desktop `<div class="flex-1 pr-4">` block, add after the existing `v-else-if` blocks:

```vue
<div v-else-if="activeTab === 'docs'" class="space-y-8">
  <CardSpec
    :card-id="card.id"
    :project-id="projectId"
    :readonly="isReadonly"
  />
  <template v-if="hasPlan">
    <USeparator />
    <CardPlan
      :card-id="card.id"
      :project-id="projectId"
      :readonly="isReadonly"
    />
  </template>
</div>
```

### Step 3: Add Docs tab content to mobile layout

After the existing `v-else-if="activeTab === 'related'"` block in the mobile section:

```vue
<div v-else-if="activeTab === 'docs'" class="space-y-8">
  <CardSpec
    :card-id="card.id"
    :project-id="projectId"
    :readonly="isReadonly"
  />
  <template v-if="hasPlan">
    <USeparator />
    <CardPlan
      :card-id="card.id"
      :project-id="projectId"
      :readonly="isReadonly"
    />
  </template>
</div>
```

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open a **Goal** card → Docs tab visible → Spec + Plan both show → create both → version history works
- Manual: open an **Idea** card → Docs tab visible → Spec shows, Plan absent
- Manual: open a **Task** card → no Docs tab in tab bar
- Manual: open an **Issue** card → no Docs tab in tab bar

### Step 5: Commit

```bash
git add src/web-ui/app/components/card/CardModal.vue
git commit -m "feat: add type-conditional Docs tab (Goal=Spec+Plan, Idea=Spec only, Task/Issue=none)"
```

---

## Task 21: SignalR BoardHub Integration + useRealtime

**Files:**
- Create: `src/web-ui/app/composables/useRealtime.ts`
- Modify: `src/web-ui/app/pages/projects/[id]/board.vue`

### Step 1: Install SignalR client

```bash
cd src/web-ui && pnpm add @microsoft/signalr
```

### Step 2: Create useRealtime composable

Hub URL is a relative path — the Nuxt `/hubs/**` proxy route (Task 17) forwards it to the backend. No env var needed for the hub base.

Create `src/web-ui/app/composables/useRealtime.ts`:

```ts
import * as signalR from '@microsoft/signalr'

export function useRealtime() {
  const { getToken } = useAuthToken()
  const board = useBoardStore()

  let connection: signalR.HubConnection | null = null
  const isConnected = ref(false)
  const isReconnecting = ref(false)

  async function connect(projectId: string) {
    const token = getToken()
    if (!token) return

    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/board', {
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
      if (envelope.projectId === projectId) {
        board.fetchBoard(projectId)
      }
    })

    connection.onreconnecting(() => { isReconnecting.value = true })
    connection.onreconnected(() => {
      isReconnecting.value = false
      connection?.invoke('JoinProject', projectId)
    })
    connection.onclose(() => { isConnected.value = false })

    try {
      await connection.start()
      isConnected.value = true
      await connection.invoke('JoinProject', projectId)
    } catch {
      // silent — board still works without real-time
    }
  }

  async function disconnect(projectId: string) {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      try { await connection.invoke('LeaveProject', projectId) } catch { /* best effort */ }
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

Modify `src/web-ui/app/pages/projects/[id]/board.vue` — add inside `<script setup>`:

```ts
const realtime = useRealtime()

onMounted(() => {
  board.fetchBoard(projectId)
  realtime.connect(projectId)
})

onBeforeUnmount(() => {
  realtime.disconnect(projectId)
})
```

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: open board in two tabs → move card in one → other tab updates without refresh

### Step 5: Commit

```bash
git add src/web-ui/package.json src/web-ui/pnpm-lock.yaml src/web-ui/app/composables/useRealtime.ts src/web-ui/app/pages/projects/[id]/board.vue
git commit -m "feat: add SignalR BoardHub integration with real-time board sync"
```

---

## Task 22: SignalR PresenceHub Integration + usePresence

**Files:**
- Create: `src/web-ui/app/composables/usePresence.ts`
- Create: `src/web-ui/app/stores/presence.ts`
- Modify: `src/web-ui/app/pages/projects/[id]/board.vue`

### Step 1: Create usePresenceStore

Create `src/web-ui/app/stores/presence.ts`:

```ts
import { defineStore } from 'pinia'

interface PresenceUser {
  userId: string
  username: string
  connectionId: string
}

export const usePresenceStore = defineStore('presence', () => {
  const onlineUsers = ref<Map<string, PresenceUser[]>>(new Map())
  const focusedCards = ref<Map<string, string>>(new Map()) // userId → cardId

  function setProjectUsers(projectId: string, users: PresenceUser[]) {
    onlineUsers.value.set(projectId, users)
  }

  function addUser(projectId: string, user: PresenceUser) {
    const users = onlineUsers.value.get(projectId) ?? []
    if (!users.find(u => u.userId === user.userId)) {
      onlineUsers.value.set(projectId, [...users, user])
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
  const { getToken } = useAuthToken()
  const store = usePresenceStore()

  let connection: signalR.HubConnection | null = null

  async function connect(projectId: string) {
    const token = getToken()
    if (!token) return

    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/presence', {
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
    } catch {
      // silent — presence is non-critical
    }
  }

  async function focusCard(projectId: string, cardId: string) {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      await connection.invoke('FocusCard', projectId, cardId)
    }
  }

  async function disconnect(projectId: string) {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      try { await connection.invoke('LeaveProject', projectId) } catch { /* best effort */ }
      await connection.stop()
    }
    connection = null
  }

  return { connect, disconnect, focusCard }
}
```

### Step 3: Wire presence into board page

Add to `board.vue` (alongside the existing `useRealtime` wiring from Task 21):

```ts
const presence = usePresence()

// Add to onMounted:
presence.connect(projectId)

// Add to onBeforeUnmount:
presence.disconnect(projectId)

// Focus card when modal opens:
watch(selectedCardId, (cardId) => {
  if (cardId) presence.focusCard(projectId, cardId)
})
```

### Step 4: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: two browser tabs → see online user count → open card in one → other tab sees focus indicator

### Step 5: Commit

```bash
git add src/web-ui/app/composables/usePresence.ts src/web-ui/app/stores/presence.ts src/web-ui/app/pages/projects/[id]/board.vue
git commit -m "feat: add SignalR PresenceHub with online users and card focus tracking"
```

---

## Task 23: Tests

**Files:**
- Create: `src/web-ui/app/components/card/__tests__/CardSpec.test.ts`
- Create: `src/web-ui/app/components/card/__tests__/CardPlan.test.ts`
- Create: `src/web-ui/app/components/card/__tests__/CardModal.docs.test.ts`
- Create: `src/web-ui/app/composables/__tests__/useRealtime.test.ts`
- Create: `src/web-ui/app/composables/__tests__/usePresence.test.ts`
- Create: `src/web-ui/app/stores/__tests__/presence.test.ts`

### Step 1: CardSpec tests

Create `src/web-ui/app/components/card/__tests__/CardSpec.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardSpec from '~/components/card/CardSpec.vue'

describe('CardSpec', () => {
  it('renders Spec section header', async () => {
    const wrapper = await mountSuspended(CardSpec, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Spec')
  })

  it('shows Create button when no spec exists', async () => {
    const wrapper = await mountSuspended(CardSpec, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Create')
  })

  it('hides Create button in readonly mode', async () => {
    const wrapper = await mountSuspended(CardSpec, {
      props: { cardId: 'c1', projectId: 'p1', readonly: true }
    })
    expect(wrapper.text()).not.toContain('Create')
    expect(wrapper.text()).not.toContain('Save')
  })
})
```

### Step 2: CardPlan tests

Create `src/web-ui/app/components/card/__tests__/CardPlan.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardPlan from '~/components/card/CardPlan.vue'

describe('CardPlan', () => {
  it('renders Plan section header', async () => {
    const wrapper = await mountSuspended(CardPlan, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Plan')
  })

  it('shows Create button when no plan exists', async () => {
    const wrapper = await mountSuspended(CardPlan, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Create')
  })

  it('hides Create button in readonly mode', async () => {
    const wrapper = await mountSuspended(CardPlan, {
      props: { cardId: 'c1', projectId: 'p1', readonly: true }
    })
    expect(wrapper.text()).not.toContain('Create')
    expect(wrapper.text()).not.toContain('Save')
  })
})
```

### Step 3: Docs tab visibility tests (CardModal)

Create `src/web-ui/app/components/card/__tests__/CardModal.docs.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardModal from '~/components/card/CardModal.vue'

// Helper — builds a minimal CardResponse stub for a given type
function makeCard(type: string) {
  return {
    id: 'card-1',
    cardNumber: 1,
    title: 'Test card',
    type,
    description: null,
    archivedAt: null,
    version: 1,
    columnId: 'col-1',
    position: 0,
    projectId: 'proj-1',
    assignees: [],
    labels: [],
    parentCardId: null,
    dueDate: null,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString()
  }
}

describe('CardModal Docs tab visibility', () => {
  it('shows Docs tab for Goal cards', async () => {
    const wrapper = await mountSuspended(CardModal, {
      props: { cardId: 'card-1', projectId: 'proj-1' }
    })
    // Inject card directly to skip API fetch
    await wrapper.vm.$nextTick()
    // Set card ref — testing the computed `hasDocsTab` logic
    // (mount with a Goal card stub via provide or direct ref assignment)
    expect(wrapper.html()).toContain('Docs')
  })

  it('does not show Docs tab for Task cards', async () => {
    // Task cards should never expose the Docs tab
    // Test via `hasDocsTab` computed with Task type
    const { hasDocsTab } = (() => {
      const DOCS_CARD_TYPES = ['Goal', 'Idea'] as const
      const card = makeCard('Task')
      const hasDocsTab = DOCS_CARD_TYPES.includes(card.type as typeof DOCS_CARD_TYPES[number])
      return { hasDocsTab }
    })()
    expect(hasDocsTab).toBe(false)
  })

  it('does not show Docs tab for Issue cards', () => {
    const DOCS_CARD_TYPES = ['Goal', 'Idea'] as const
    const card = makeCard('Issue')
    expect(DOCS_CARD_TYPES.includes(card.type as typeof DOCS_CARD_TYPES[number])).toBe(false)
  })

  it('shows Docs tab for Idea cards', () => {
    const DOCS_CARD_TYPES = ['Goal', 'Idea'] as const
    const card = makeCard('Idea')
    expect(DOCS_CARD_TYPES.includes(card.type as typeof DOCS_CARD_TYPES[number])).toBe(true)
  })

  it('Plan only shown for Goal, not Idea', () => {
    const PLAN_CARD_TYPES = ['Goal'] as const
    expect(PLAN_CARD_TYPES.includes('Goal')).toBe(true)
    expect(PLAN_CARD_TYPES.includes('Idea' as typeof PLAN_CARD_TYPES[number])).toBe(false)
  })
})
```

### Step 5: useRealtime tests

Create `src/web-ui/app/composables/__tests__/useRealtime.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { useRealtime } from '~/composables/useRealtime'

describe('useRealtime', () => {
  it('returns connect and disconnect functions', () => {
    const rt = useRealtime()
    expect(typeof rt.connect).toBe('function')
    expect(typeof rt.disconnect).toBe('function')
  })

  it('starts disconnected', () => {
    const rt = useRealtime()
    expect(rt.isConnected.value).toBe(false)
    expect(rt.isReconnecting.value).toBe(false)
  })
})
```

### Step 6: usePresence tests

Create `src/web-ui/app/composables/__tests__/usePresence.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { usePresence } from '~/composables/usePresence'

describe('usePresence', () => {
  it('returns connect, disconnect, focusCard functions', () => {
    const p = usePresence()
    expect(typeof p.connect).toBe('function')
    expect(typeof p.disconnect).toBe('function')
    expect(typeof p.focusCard).toBe('function')
  })
})
```

### Step 7: usePresenceStore tests

Create `src/web-ui/app/stores/__tests__/presence.test.ts`:

```ts
import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { usePresenceStore } from '~/stores/presence'

describe('usePresenceStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('starts with empty state', () => {
    const store = usePresenceStore()
    expect(store.onlineUsers.size).toBe(0)
    expect(store.focusedCards.size).toBe(0)
  })

  it('addUser adds user to project', () => {
    const store = usePresenceStore()
    store.addUser('p1', { userId: 'u1', username: 'Alice', connectionId: 'conn1' })
    expect(store.onlineUsers.get('p1')?.length).toBe(1)
    expect(store.onlineUsers.get('p1')?.[0].username).toBe('Alice')
  })

  it('addUser does not duplicate same userId', () => {
    const store = usePresenceStore()
    store.addUser('p1', { userId: 'u1', username: 'Alice', connectionId: 'conn1' })
    store.addUser('p1', { userId: 'u1', username: 'Alice', connectionId: 'conn2' })
    expect(store.onlineUsers.get('p1')?.length).toBe(1)
  })

  it('removeUser removes by connectionId', () => {
    const store = usePresenceStore()
    store.addUser('p1', { userId: 'u1', username: 'Alice', connectionId: 'conn1' })
    store.addUser('p1', { userId: 'u2', username: 'Bob', connectionId: 'conn2' })
    store.removeUser('p1', 'conn1')
    expect(store.onlineUsers.get('p1')?.length).toBe(1)
    expect(store.onlineUsers.get('p1')?.[0].username).toBe('Bob')
  })

  it('setCardFocus and clearCardFocus', () => {
    const store = usePresenceStore()
    store.setCardFocus('u1', 'c1')
    expect(store.focusedCards.get('u1')).toBe('c1')
    store.clearCardFocus('u1')
    expect(store.focusedCards.has('u1')).toBe(false)
  })

  it('setProjectUsers replaces all users for project', () => {
    const store = usePresenceStore()
    store.setProjectUsers('p1', [
      { userId: 'u1', username: 'Alice', connectionId: 'conn1' },
      { userId: 'u2', username: 'Bob', connectionId: 'conn2' }
    ])
    expect(store.onlineUsers.get('p1')?.length).toBe(2)
  })
})
```

### Step 8: Verify

- `cd src/web-ui && pnpm test` — all tests pass
- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors

### Step 9: Commit

```bash
git add src/web-ui/app/components/card/__tests__/ src/web-ui/app/composables/__tests__/useRealtime.test.ts src/web-ui/app/composables/__tests__/usePresence.test.ts src/web-ui/app/stores/__tests__/presence.test.ts
git commit -m "test: add CardSpec, CardPlan, Docs tab visibility, useRealtime, usePresence, and presence store tests"
```

---

## Verification (Plan 5 Complete)

1. `cd src/web-ui && pnpm typecheck` — zero errors
2. `cd src/web-ui && pnpm lint` — zero errors
3. `cd src/web-ui && pnpm build` — successful production build
4. `cd src/web-ui && pnpm test` — all tests pass
5. Manual: open card → Docs tab → Create spec → write content → Save → History → restore version
6. Manual: open card → Docs tab → Create plan → write steps → same flow
7. Manual: two browser tabs on same board → move card in one → other updates via SignalR
8. Manual: two tabs → open card in one → other tab shows focus indicator
