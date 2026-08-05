# Chat List UX Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix four UX bugs in the chat session list (history button in draft mode, missing temp title on dock-created sessions, missing loading spinner + "All caught up" footer on /chats page) and extract a shared `ChatSessionList.vue` component to eliminate duplicated list-rendering code between `/chats/index.vue` and `ChatDockHistory.vue`.

**Architecture:** Extract a shared presentational component that owns the IntersectionObserver sentinel, loading spinner, "All caught up" footer, and empty state. Both `/chats/index.vue` and `ChatDockHistory.vue` consume it via a scoped `#item` slot for per-row rendering. Bug fixes are applied to the shared component (spinner, footer) and the dock store (temp title, history button).

**Tech Stack:** Nuxt 4, Vue 3, TypeScript, Nuxt UI v4, Vitest + @nuxt/test-utils

---

### Task 1: Extract shared `ChatSessionList.vue` component

**Files:**
- Create: `src/web-ui/app/components/shared/ChatSessionList.vue`
- Create: `src/web-ui/app/components/shared/__tests__/ChatSessionList.test.ts`

**What:** Create a shared presentational component that owns the IntersectionObserver sentinel, loading spinner, "All caught up" footer, and empty state. Both consumers will use it via a scoped `#item` slot.

- [ ] **Step 1: Create the shared component**

```vue
<!-- src/web-ui/app/components/shared/ChatSessionList.vue -->
<script setup lang="ts">
import type { ChatSessionDto } from '~/types/chat'

const props = defineProps<{
  sessions: ChatSessionDto[]
  loading: boolean
  hasMore: boolean
  activeSessionId?: string | null
}>()

const emit = defineEmits<{
  select: [sessionId: string]
  loadMore: []
}>()

const sentinel = ref<HTMLElement | null>(null)
const observer = ref<IntersectionObserver | null>(null)

onMounted(() => {
  observer.value = new IntersectionObserver(
    ([entry]) => {
      if (entry?.isIntersecting && !props.loading && props.hasMore) {
        emit('loadMore')
      }
    },
    { rootMargin: '100px' }
  )
})

// watch the sentinel ref reactively — the sentinel may render after mount
// (v-if block), so onMounted's direct observe may miss it
watch(sentinel, (el) => {
  if (el && observer.value) {
    observer.value.observe(el)
  }
})

onUnmounted(() => {
  observer.value?.disconnect()
})
</script>

<template>
  <div class="flex-1 overflow-y-auto">
    <!-- Initial loading state (no items yet) -->
    <div
      v-if="loading && sessions.length === 0"
      class="flex items-center justify-center py-8"
    >
      <UIcon name="i-lucide-loader-2" class="animate-spin text-muted size-5" />
    </div>

    <!-- Empty state -->
    <div
      v-else-if="!loading && sessions.length === 0"
      class="px-4 py-8 text-sm text-muted text-center"
    >
      <slot name="empty">No conversations yet.</slot>
    </div>

    <!-- Session items + sentinel + footer -->
    <template v-else>
      <template
        v-for="session in sessions"
        :key="session.id"
      >
        <slot
          name="item"
          :session="session"
          :active="session.id === activeSessionId"
        />
      </template>

      <!-- Sentinel for infinite scroll -->
      <div
        v-if="hasMore"
        ref="sentinel"
        class="h-4 shrink-0"
      />

      <!-- Loading more spinner -->
      <div
        v-if="loading"
        class="flex items-center justify-center py-3"
      >
        <UIcon name="i-lucide-loader-2" class="animate-spin text-muted size-5" />
      </div>

      <!-- All caught up footer -->
      <div
        v-if="!hasMore && sessions.length > 0"
        class="px-4 py-3 text-sm text-muted text-center"
      >
        All caught up
      </div>
    </template>
  </div>
</template>
```

- [ ] **Step 2: Write tests for the shared component**

```typescript
// src/web-ui/app/components/shared/__tests__/ChatSessionList.test.ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'

// Stub IntersectionObserver — not available in jsdom
const mockObserve = vi.fn()
const mockDisconnect = vi.fn()
const MockObserver = vi.fn(() => ({
  observe: mockObserve,
  disconnect: mockDisconnect,
  unobserve: vi.fn()
}))
vi.stubGlobal('IntersectionObserver', MockObserver)

import ChatSessionList from '~/components/shared/ChatSessionList.vue'

function makeSession(id: string, title: string) {
  return {
    id,
    title,
    folderId: null,
    projectId: null,
    openCardId: null,
    personalityId: null,
    personalityArchived: false,
    status: 'Active' as const,
    summary: null,
    updatedAt: '2026-01-01T00:00:00Z',
    createdAt: '2026-01-01T00:00:00Z'
  }
}

describe('ChatSessionList', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders loading spinner when loading and no sessions', async () => {
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions: [], loading: true, hasMore: true }
    })
    expect(wrapper.find('.animate-spin').exists()).toBe(true)
  })

  it('renders empty state when not loading and no sessions', async () => {
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions: [], loading: false, hasMore: false }
    })
    expect(wrapper.text()).toContain('No conversations yet')
  })

  it('renders custom empty slot content', async () => {
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions: [], loading: false, hasMore: false },
      slots: { empty: 'Custom empty message' }
    })
    expect(wrapper.text()).toContain('Custom empty message')
  })

  it('renders session items via scoped slot', async () => {
    const sessions = [makeSession('1', 'Chat One'), makeSession('2', 'Chat Two')]
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions, loading: false, hasMore: false },
      slots: {
        item: `<template #item="{ session, active }">
          <div :data-testid="session.id" :data-active="active">{{ session.title }}</div>
        </template>`
      }
    })
    expect(wrapper.find('[data-testid="1"]').text()).toBe('Chat One')
    expect(wrapper.find('[data-testid="2"]').text()).toBe('Chat Two')
  })

  it('marks active session via scoped slot prop', async () => {
    const sessions = [makeSession('1', 'Chat One')]
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions, loading: false, hasMore: false, activeSessionId: '1' },
      slots: {
        item: `<template #item="{ session, active }">
          <div :data-testid="session.id" :data-active="active">{{ session.title }}</div>
        </template>`
      }
    })
    expect(wrapper.find('[data-testid="1"]').attributes('data-active')).toBe('true')
  })

  it('renders sentinel when hasMore is true', async () => {
    const sessions = [makeSession('1', 'Chat One')]
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions, loading: false, hasMore: true }
    })
    // Sentinel is a div with h-4 class
    expect(wrapper.find('.h-4.shrink-0').exists()).toBe(true)
  })

  it('renders loading spinner when loading and sessions exist', async () => {
    const sessions = [makeSession('1', 'Chat One')]
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions, loading: true, hasMore: true }
    })
    // Two spinners: initial loading (hidden by v-else) + loading-more
    const spinners = wrapper.findAll('.animate-spin')
    expect(spinners.length).toBeGreaterThanOrEqual(1)
  })

  it('renders "All caught up" when no more items and sessions exist', async () => {
    const sessions = [makeSession('1', 'Chat One')]
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions, loading: false, hasMore: false }
    })
    expect(wrapper.text()).toContain('All caught up')
  })

  it('does not render "All caught up" when sessions is empty', async () => {
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions: [], loading: false, hasMore: false }
    })
    expect(wrapper.text()).not.toContain('All caught up')
  })

  it('creates IntersectionObserver on mount', async () => {
    await mountSuspended(ChatSessionList, {
      props: { sessions: [], loading: false, hasMore: true }
    })
    expect(MockObserver).toHaveBeenCalled()
  })

  it('disconnects observer on unmount', async () => {
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions: [], loading: false, hasMore: true }
    })
    wrapper.unmount()
    expect(mockDisconnect).toHaveBeenCalled()
  })
})
```

- [ ] **Step 3: Run tests to verify**

Run: `cd src/web-ui && pnpm vitest run src/web-ui/app/components/shared/__tests__/ChatSessionList.test.ts`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/web-ui/app/components/shared/ChatSessionList.vue src/web-ui/app/components/shared/__tests__/ChatSessionList.test.ts
git commit -m "feat: extract shared ChatSessionList component with spinner, sentinel, and footer"
```

---

### Task 2: Update `/chats/index.vue` to use shared `ChatSessionList`

**Files:**
- Modify: `src/web-ui/app/pages/chats/index.vue`

**What:** Replace the inline session list rendering, IntersectionObserver, and sentinel with the shared `ChatSessionList` component. Remove the local `sentinel`, `observer`, `listEl` refs and the `onMounted`/`watch`/`onUnmounted` observer wiring. The shared component handles all of that.

- [ ] **Step 1: Rewrite the sidebar list section**

Replace lines 22-24 (refs), lines 28-54 (observer setup), and lines 167-221 (template list) with the shared component.

**Remove these lines from `<script setup>`:**
```typescript
const sentinel = ref<HTMLElement | null>(null)       // line 22
const observer = ref<IntersectionObserver | null>(null) // line 23
const listEl = ref<HTMLElement | null>(null)           // line 24
```

**Remove the entire `onMounted` block (lines 28-44) and replace with:**
```typescript
onMounted(() => {
  if (dock.activeSessionId) {
    activeSessionId.value = dock.activeSessionId
  } else {
    const saved = localStorage.getItem('hydraforge:chat:activeSessionId')
    if (saved) activeSessionId.value = saved
  }
})
```

**Remove the `watch(sentinel, ...)` block (lines 46-50) and `onUnmounted` block (lines 52-54).**

**Replace the template sidebar list (lines 167-221) with:**

```vue
        <ChatSessionList
          :sessions="sessions"
          :loading="loading"
          :has-more="hasMore"
          :active-session-id="activeSessionId"
          @select="selectSession"
          @load-more="loadMore"
        >
          <template #empty>
            No chats yet — type below to start one.
          </template>
          <template #item="{ session, active }">
            <li class="group relative">
              <button
                class="w-full text-left px-4 py-3 pr-9 border-b border-gray-100 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800"
                :class="active ? 'bg-gray-100 dark:bg-gray-800' : ''"
                @click="selectSession(session.id)"
              >
                <p class="truncate text-sm font-medium">
                  {{ session.title }}
                </p>
                <p
                  v-if="session.status !== 'Active'"
                  class="text-xs text-muted"
                >
                  {{ session.status }}
                </p>
              </button>
              <UButton
                icon="i-lucide-archive"
                variant="ghost"
                color="neutral"
                size="xs"
                title="Archive chat"
                class="absolute right-1 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity"
                @click.stop="requestArchive(session.id)"
              />
            </li>
          </template>
        </ChatSessionList>
```

**Also remove the initial loading text block (lines 168-172) — the shared component handles it.**

**Also remove the `listEl` ref usage in `startNewChat` (line 111):**
```typescript
// Remove this line:
if (listEl.value) listEl.value.scrollTop = 0
```
The shared component's overflow-y-auto container handles scroll naturally.

**Add the import at the top:**
```typescript
import ChatSessionList from '~/components/shared/ChatSessionList.vue'
```

- [ ] **Step 2: Run typecheck and lint**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: No errors.

- [ ] **Step 3: Commit**

```bash
git add src/web-ui/app/pages/chats/index.vue
git commit -m "refactor: use shared ChatSessionList on /chats page"
```

---

### Task 3: Update `ChatDockHistory.vue` to use shared `ChatSessionList`

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatDockHistory.vue`
- Modify: `src/web-ui/app/components/chat/__tests__/ChatDockHistory.test.ts`

**What:** Replace the inline session list rendering, IntersectionObserver, and sentinel with the shared `ChatSessionList` component. Remove the local `sentinel`, `observer` refs and the `onMounted`/`onUnmounted` observer wiring.

- [ ] **Step 1: Rewrite ChatDockHistory.vue**

```vue
<!-- src/web-ui/app/components/chat/ChatDockHistory.vue -->
<script setup lang="ts">
import { useChatSessionList } from '~/composables/useChatSessionList'
import ChatSessionList from '~/components/shared/ChatSessionList.vue'

const emit = defineEmits<{
  select: [sessionId: string]
  back: []
}>()

const { sessions, loading, hasMore, loadMore } = useChatSessionList()
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <div class="flex items-center justify-between px-4 py-2 border-b border-gray-200 dark:border-gray-700">
      <UButton
        icon="i-lucide-chevron-left"
        variant="ghost"
        size="xs"
        @click="emit('back')"
      >
        Back
      </UButton>
      <span class="text-sm font-medium">History</span>
      <div class="w-8" />
    </div>
    <ChatSessionList
      :sessions="sessions"
      :loading="loading"
      :has-more="hasMore"
      @select="emit('select', $event)"
      @load-more="loadMore"
    >
      <template #item="{ session }">
        <div
          class="px-4 py-3 border-b border-gray-100 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800 cursor-pointer"
          @click="emit('select', session.id)"
        >
          <div class="text-sm font-medium truncate">
            {{ session.title || 'New Chat' }}
          </div>
          <div class="text-xs text-muted truncate mt-0.5">
            {{ session.summary || 'No messages' }}
          </div>
        </div>
      </template>
    </ChatSessionList>
  </div>
</template>
```

- [ ] **Step 2: Update ChatDockHistory tests**

The existing tests mock `useChatSessionList` and check for text content. They should still pass since the shared component renders the same text. Update the import to also stub `ChatSessionList` if needed — but since `ChatSessionList` is a presentational component that renders slots, the existing mock data should flow through fine.

Run: `cd src/web-ui && pnpm vitest run src/web-ui/app/components/chat/__tests__/ChatDockHistory.test.ts`
Expected: Both tests pass (empty state text "No conversations yet" and session title "Test Chat" still render).

If the test fails because `ChatSessionList` is auto-imported but not resolved in test, add a stub:
```typescript
// In ChatDockHistory.test.ts, add after the mockUseChatSessionList setup:
vi.mock('~/components/shared/ChatSessionList.vue', () => ({
  default: defineComponent({
    props: ['sessions', 'loading', 'hasMore', 'activeSessionId'],
    emits: ['select', 'loadMore'],
    template: `<div>
      <div v-if="loading && sessions.length === 0">Loading spinner</div>
      <div v-else-if="!loading && sessions.length === 0">No conversations yet</div>
      <template v-else>
        <div v-for="s in sessions" :key="s.id" @click="$emit('select', s.id)">
          {{ s.title }}
        </div>
        <div v-if="!hasMore && sessions.length > 0">All caught up</div>
      </template>
    </div>`
  })
}))
```

- [ ] **Step 3: Run typecheck and lint**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: No errors.

- [ ] **Step 4: Commit**

```bash
git add src/web-ui/app/components/chat/ChatDockHistory.vue src/web-ui/app/components/chat/__tests__/ChatDockHistory.test.ts
git commit -m "refactor: use shared ChatSessionList in ChatDockHistory"
```

---

### Task 4: Fix Bug 1 — History button in dock draft mode

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatDock.vue`
- Modify: `src/web-ui/app/components/chat/__tests__/ChatDock.test.ts`

**What:** The history back-arrow button is `v-if="dock.mode === 'session'"` (line 130). In draft mode, there's no way to reach the history panel. Change the condition so the history button is visible whenever `dock.mode !== 'history'`.

- [ ] **Step 1: Change the v-if condition**

In `ChatDock.vue` line 130, change:
```vue
v-if="dock.mode === 'session'"
```
to:
```vue
v-if="dock.mode !== 'history'"
```

- [ ] **Step 2: Add a test for draft mode history button**

In `ChatDock.test.ts`, add this test:

```typescript
it('draft mode: shows history button', async () => {
  storeState.mode = 'draft'
  storeState.activeSessionId = undefined as unknown as string
  const wrapper = await mountSuspended(ChatDock)
  // The history button has title="History"
  expect(wrapper.find('button[title="History"]').exists()).toBe(true)
})
```

- [ ] **Step 3: Run tests**

Run: `cd src/web-ui && pnpm vitest run src/web-ui/app/components/chat/__tests__/ChatDock.test.ts`
Expected: All tests pass, including the new one.

- [ ] **Step 4: Commit**

```bash
git add src/web-ui/app/components/chat/ChatDock.vue src/web-ui/app/components/chat/__tests__/ChatDock.test.ts
git commit -m "fix: show history button in dock draft mode"
```

---

### Task 5: Fix Bug 2 — Temp title on dock-created sessions

**Files:**
- Modify: `src/web-ui/app/stores/chatDock.ts`
- Modify: `src/web-ui/app/stores/__tests__/chatDock.test.ts`

**What:** The dock's `startNewChat` creates sessions with `title: ''`. The `/chats` page uses `deriveTitle(content)` to crop the first message to ≤60 chars. Add the same logic to the dock store so the session list shows a meaningful temp title immediately.

- [ ] **Step 1: Add deriveTitle and use it in startNewChat**

In `chatDock.ts`, add the constant and function after the `LS_ACTIVE_SESSION_KEY` line (after line 6):

```typescript
const TITLE_MAX_LENGTH = 60

function deriveTitle(content: string): string {
  const firstLine = content.trim().split('\n')[0] ?? content.trim()
  return firstLine.length > TITLE_MAX_LENGTH
    ? `${firstLine.slice(0, TITLE_MAX_LENGTH)}…`
    : firstLine
}
```

Then change line 100 from:
```typescript
const body: Record<string, unknown> = { title: '' }
```
to:
```typescript
const body: Record<string, unknown> = { title: content ? deriveTitle(content) : '' }
```

- [ ] **Step 2: Update existing tests**

The existing test at line 71-73 expects `title: ''` when no content is passed:
```typescript
expect(mockPOST).toHaveBeenCalledWith('/api/chat/sessions', {
  body: { title: '', projectId: 'abc' }
})
```
This still passes — `content` is undefined when `startNewChat()` is called with no args, so `content ? deriveTitle(content) : ''` evaluates to `''`.

The existing test at line 106-108 expects `title: ''` even when content is passed:
```typescript
expect(mockPOST).toHaveBeenCalledWith('/api/chat/sessions', {
  body: { title: '', projectId: 'abc', preferredModelConfigId: 'model-1', preferredEffort: 'high' }
})
```
This needs updating — now `title` should be the derived title from `'hello'`.

Change line 107 to:
```typescript
  body: { title: 'hello', projectId: 'abc', preferredModelConfigId: 'model-1', preferredEffort: 'high' }
```

- [ ] **Step 3: Add a test for long content truncation**

```typescript
it('startNewChat derives temp title from first line of content', async () => {
  mockPOST.mockResolvedValue({ data: { id: 'new-session' }, error: undefined })
  const store = useChatDockStore()
  const longLine = 'A'.repeat(100) + '\nsecond line ignored'
  await store.startNewChat(longLine)
  expect(mockPOST).toHaveBeenCalledWith('/api/chat/sessions', {
    body: { title: 'A'.repeat(60) + '…', projectId: 'abc' }
  })
})
```

- [ ] **Step 4: Run tests**

Run: `cd src/web-ui && pnpm vitest run src/web-ui/app/stores/__tests__/chatDock.test.ts`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/stores/chatDock.ts src/web-ui/app/stores/__tests__/chatDock.test.ts
git commit -m "fix: derive temp title from first message in dock startNewChat"
```

---

### Task 6: Verification — full build and test suite

**Files:** None (verification only)

**What:** Run the full web UI verification pipeline and a backend build to confirm nothing is broken.

- [ ] **Step 1: Run web typecheck, lint, and tests**

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm vitest run
```
Expected: All pass, no errors.

- [ ] **Step 2: Run backend build**

```bash
dotnet build
```
Expected: Build succeeds (no backend changes in this plan, but verify nothing regressed).

- [ ] **Step 3: Manual smoke test checklist**

1. Open the dock in draft mode (no active session) — verify the history button (chevron-left) is visible and clicking it opens the history panel.
2. Create a new chat from the dock with a message — verify the session list shows the first line of the message as the title (not blank).
3. Open the /chats page — verify the loading spinner appears during initial load, then sessions render.
4. Scroll to the bottom of the /chats session list — verify the "All caught up" footer appears when all sessions are loaded.
5. Open the dock history — verify the loading spinner and "All caught up" footer work the same way.
6. Verify the /chats page sidebar toggle still works (chevron button to collapse/expand).
7. Verify the dock history "Back" button still returns to the previous mode.