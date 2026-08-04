# Chat UX Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add rename session, copy message, export chat, and find-in-conversation to the Web UI chat feature.

**Architecture:** All four features are Web UI only — no backend/API changes (rename reuses the existing, currently-unused `PATCH /api/chat/sessions/{id}` endpoint; copy/export/find-in are pure client-side operating on already-loaded `session.value.messages`). Changes land in three existing components: `ChatSessionView.vue` (rename, export, find-in), `ChatMessageBubble.vue` (copy, plus a `highlighted` prop for find-in), `ChatMessageList.vue` (one-line prop passthrough for find-in's highlight).

**Tech Stack:** Vue 3 `<script setup>`, Nuxt 4, Nuxt UI v4, Vitest + `@nuxt/test-utils/runtime` (`mountSuspended`, `mockNuxtImport`).

## Global Constraints

- No `console.log`/`console.error`/`console.warn` — use `useAppToast()` for user-facing feedback (CLAUDE.md).
- No inline API path strings — use `ApiRoutes.*` from `~/lib/routes.ts` (already has everything needed: `ApiRoutes.Chat.sessions.update`).
- `useApi()` throws — every call site wraps in try/catch (CLAUDE.md D-40).
- Test pattern: `mountSuspended` + `mockNuxtImport` for composables, matching the existing `pages/chats/__tests__/index.test.ts` and `components/layout/__tests__/AppTopbar.test.ts` conventions — do not introduce a different mocking approach.
- `vue/no-v-html`, `pnpm lint`, `pnpm typecheck` must stay at 0 errors/warnings after every task.

---

### Task 1: Rename session + `ChatSessionView` test harness

This is the first test file for `ChatSessionView.vue` — it sets up the full mock harness (`useApi`, `useToast`, `useChatStream`) that Tasks 3 and 4 reuse.

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatSessionView.vue`
- Create: `src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts`

**Interfaces:**
- Consumes: `ApiRoutes.Chat.sessions.update(sessionId)` (existing, `~/lib/routes.ts`), `ChatSessionDto` type (existing, `~/types/chat.ts`)
- Produces: no new exports — internal refs/functions only (`isEditingTitle`, `editedTitle`, `titleInputRef`, `startEditTitle()`, `cancelTitleEdit()`, `submitTitleEdit()`)

- [ ] **Step 1: Write the failing tests**

Create `src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts`:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ChatSessionView from '~/components/chat/ChatSessionView.vue'
import { ChatSessionStatus, AiEditMode } from '~/types/chat'

const mockGET = vi.fn()
const mockPATCH = vi.fn()
const mockPOST = vi.fn()
const mockDELETE = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  PATCH: mockPATCH,
  POST: mockPOST,
  DELETE: mockDELETE
}))

mockNuxtImport('useToast', () => () => ({ add: vi.fn() }))

const mockChatStream = {
  connect: vi.fn().mockResolvedValue(undefined),
  disconnect: vi.fn().mockResolvedValue(undefined),
  join: vi.fn().mockResolvedValue(undefined),
  leave: vi.fn().mockResolvedValue(undefined),
  send: vi.fn(),
  resend: vi.fn(),
  cancel: vi.fn().mockResolvedValue(undefined),
  streamingMessage: ref(null),
  isStreaming: ref(false),
  isConnected: ref(false),
  isReconnecting: ref(false),
  onStreamStart: vi.fn(),
  onStreamDelta: vi.fn(),
  onStreamDone: vi.fn(),
  onStreamError: vi.fn(),
  onReconnected: vi.fn(),
  clearStreaming: vi.fn()
}

mockNuxtImport('useChatStream', () => () => mockChatStream)

const baseSession = {
  id: 's1',
  title: 'Original Title',
  folderId: null,
  projectId: null,
  openCardId: null,
  personalityId: null,
  personalityArchived: false,
  status: ChatSessionStatus.Active,
  aiEditMode: AiEditMode.PerMutation,
  searchAllMyDocs: false,
  summary: null,
  createdAt: '2026-08-01T00:00:00Z',
  updatedAt: '2026-08-01T00:00:00Z',
  archivedAt: null,
  ownerId: 'u1',
  isShared: false,
  closedAt: null,
  messages: []
}

const stubs = { ChatMessageList: true, ChatInput: true, ConfirmDialog: true }

async function mountView() {
  const wrapper = await mountSuspended(ChatSessionView, {
    props: { sessionId: 's1' },
    global: { stubs }
  })
  await flushPromises()
  return wrapper
}

describe('ChatSessionView — rename', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockPATCH.mockReset()
    mockGET.mockResolvedValue({ data: { ...baseSession }, error: undefined })
  })

  it('renders the fetched session title', async () => {
    const wrapper = await mountView()
    expect(wrapper.text()).toContain('Original Title')
  })

  it('clicking the rename button shows an editable input pre-filled with the current title', async () => {
    const wrapper = await mountView()
    await wrapper.find('[title="Rename chat"]').trigger('click')
    const input = wrapper.find('input[data-testid="title-input"]')
    expect(input.exists()).toBe(true)
    expect((input.element as HTMLInputElement).value).toBe('Original Title')
  })

  it('submitting a new title PATCHes the full request shape and updates the displayed title', async () => {
    mockPATCH.mockResolvedValue({
      data: { ...baseSession, title: 'New Title' },
      error: undefined
    })
    const wrapper = await mountView()
    await wrapper.find('[title="Rename chat"]').trigger('click')
    const input = wrapper.find('input[data-testid="title-input"]')
    await input.setValue('New Title')
    await input.trigger('blur')
    await flushPromises()

    expect(mockPATCH).toHaveBeenCalledWith('/api/chat/sessions/s1', {
      body: {
        title: 'New Title',
        folderId: null,
        personalityId: null,
        aiEditMode: AiEditMode.PerMutation,
        searchAllMyDocs: false
      }
    })
    expect(wrapper.text()).toContain('New Title')
  })

  it('reverts the title and shows an error toast when the rename PATCH fails', async () => {
    mockPATCH.mockRejectedValue(new Error('boom'))
    const wrapper = await mountView()
    await wrapper.find('[title="Rename chat"]').trigger('click')
    const input = wrapper.find('input[data-testid="title-input"]')
    await input.setValue('New Title')
    await input.trigger('blur')
    await flushPromises()

    expect(wrapper.text()).toContain('Original Title')
    expect(wrapper.text()).not.toContain('New Title')
  })

  it('does not PATCH when the title is unchanged', async () => {
    const wrapper = await mountView()
    await wrapper.find('[title="Rename chat"]').trigger('click')
    const input = wrapper.find('input[data-testid="title-input"]')
    await input.trigger('blur')
    await flushPromises()

    expect(mockPATCH).not.toHaveBeenCalled()
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/web-ui && pnpm test app/components/chat/__tests__/ChatSessionView.test.ts`
Expected: FAIL — `[title="Rename chat"]` doesn't exist yet (no rename button in the current template).

- [ ] **Step 3: Implement rename in `ChatSessionView.vue`**

Add the `ChatSessionDto` type to the existing type import (line 6):

```typescript
import type { ChatMessageDto, ChatSessionDetailDto, ChatSessionDto } from '~/types/chat'
```

Add new refs after the existing `rollbackDiscardCount` ref (after line 47):

```typescript
const isEditingTitle = ref(false)
const editedTitle = ref('')
const titleInputRef = ref<HTMLInputElement | null>(null)
```

Add new functions after `handleCancel` (after line 208):

```typescript
function startEditTitle() {
  if (!session.value) return
  editedTitle.value = session.value.title
  isEditingTitle.value = true
  nextTick(() => titleInputRef.value?.focus())
}

function cancelTitleEdit() {
  isEditingTitle.value = false
}

async function submitTitleEdit() {
  if (!isEditingTitle.value || !session.value) return
  isEditingTitle.value = false

  const newTitle = editedTitle.value.trim()
  if (!newTitle || newTitle === session.value.title) return

  const previousTitle = session.value.title
  session.value.title = newTitle
  try {
    const { data } = await api.PATCH<ChatSessionDto>(
      ApiRoutes.Chat.sessions.update(props.sessionId),
      {
        body: {
          title: newTitle,
          folderId: session.value.folderId,
          personalityId: session.value.personalityId,
          aiEditMode: session.value.aiEditMode,
          searchAllMyDocs: session.value.searchAllMyDocs
        }
      }
    )
    if (data && session.value) {
      session.value.title = data.title
      emit('sessionRefreshed', session.value.id, data.title, session.value.status)
    }
  } catch (err) {
    if (session.value) session.value.title = previousTitle
    toast.error(err instanceof Error ? err.message : 'Failed to rename chat')
  }
}
```

Replace the header block (lines 293-297):

```html
    <div class="shrink-0 border-b border-gray-200 dark:border-gray-700 px-4 py-3 flex items-center gap-2">
      <input
        v-if="isEditingTitle"
        ref="titleInputRef"
        v-model="editedTitle"
        data-testid="title-input"
        class="flex-1 min-w-0 font-semibold bg-transparent border-b border-primary focus-visible:outline-none"
        @keydown.enter="(e: KeyboardEvent) => (e.target as HTMLInputElement).blur()"
        @keydown.esc="cancelTitleEdit"
        @blur="submitTitleEdit"
      >
      <template v-else>
        <h2 class="font-semibold truncate flex-1 min-w-0">
          {{ session?.title ?? 'Chat' }}
        </h2>
        <UButton
          icon="i-lucide-pencil"
          variant="ghost"
          color="neutral"
          size="xs"
          title="Rename chat"
          @click="startEditTitle"
        />
      </template>
    </div>
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/web-ui && pnpm test app/components/chat/__tests__/ChatSessionView.test.ts`
Expected: PASS — all 5 tests green.

- [ ] **Step 5: Typecheck and lint**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: 0 errors, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/components/chat/ChatSessionView.vue src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts
git commit -m "feat(chat): add session rename"
```

---

### Task 2: Copy message button

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatMessageBubble.vue`
- Create: `src/web-ui/app/components/chat/__tests__/ChatMessageBubble.test.ts`

**Interfaces:**
- Consumes: nothing new
- Produces: no new exports — internal `copied` ref and `copyMessage()` function only. (`highlighted` prop is added in Task 4, not here — keep this task's diff scoped to copy.)

- [ ] **Step 1: Write the failing tests**

Create `src/web-ui/app/components/chat/__tests__/ChatMessageBubble.test.ts`:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import ChatMessageBubble from '~/components/chat/ChatMessageBubble.vue'
import { MessageRole } from '~/types/chat'

mockNuxtImport('useAuthStore', () => () => ({
  user: { username: 'testuser' }
}))

const mockToastAdd = vi.fn()
mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

const baseMessage = {
  id: 'm1',
  sessionId: 's1',
  role: MessageRole.Assistant,
  content: 'Hello **world**',
  inputTokens: 0,
  outputTokens: 0,
  cachedTokens: 0,
  modelName: null,
  imagesJson: null,
  createdAt: '2026-08-01T00:00:00Z'
}

function mockClipboard(writeText: ReturnType<typeof vi.fn>) {
  // navigator.clipboard is a getter-only property in happy-dom — Object.assign
  // throws "Cannot set property clipboard of [object Object] which has only a
  // getter". defineProperty with a fresh descriptor is required instead.
  Object.defineProperty(navigator, 'clipboard', {
    value: { writeText },
    configurable: true,
    writable: true
  })
}

describe('ChatMessageBubble — copy', () => {
  beforeEach(() => {
    mockToastAdd.mockReset()
    mockClipboard(vi.fn().mockResolvedValue(undefined))
  })

  it('copies the raw (unrendered) message content to the clipboard', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: { message: baseMessage }
    })
    await wrapper.find('[title="Copy message"]').trigger('click')
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('Hello **world**')
  })

  it('shows an error toast when the clipboard write fails', async () => {
    mockClipboard(vi.fn().mockRejectedValue(new Error('denied')))
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: { message: baseMessage }
    })
    await wrapper.find('[title="Copy message"]').trigger('click')
    await new Promise(resolve => setTimeout(resolve, 0))
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error' })
    )
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/web-ui && pnpm test app/components/chat/__tests__/ChatMessageBubble.test.ts`
Expected: FAIL — `[title="Copy message"]` doesn't exist yet.

- [ ] **Step 3: Implement copy in `ChatMessageBubble.vue`**

Add `useAppToast` after the existing `useAuthStore` line (line 23):

```typescript
const authStore = useAuthStore()
const toast = useAppToast()
```

Add a `copied` ref and `copyMessage` function after `getImageSrc` (after line 72, before `</script>`):

```typescript
const copied = ref(false)

async function copyMessage() {
  try {
    await navigator.clipboard.writeText(props.message.content)
    copied.value = true
    setTimeout(() => { copied.value = false }, 1500)
  } catch {
    toast.error('Failed to copy message')
  }
}
```

Add the copy button in the timestamp row, right before the existing edit/regenerate `UButton` (before line 140):

```html
        <UButton
          :icon="copied ? 'i-lucide-check' : 'i-lucide-copy'"
          title="Copy message"
          variant="ghost"
          color="neutral"
          size="xs"
          class="opacity-0 group-hover:opacity-100 transition-opacity"
          @click="copyMessage"
        />
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/web-ui && pnpm test app/components/chat/__tests__/ChatMessageBubble.test.ts`
Expected: PASS — both tests green.

- [ ] **Step 5: Typecheck and lint**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: 0 errors, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/components/chat/ChatMessageBubble.vue src/web-ui/app/components/chat/__tests__/ChatMessageBubble.test.ts
git commit -m "feat(chat): add copy-message button"
```

---

### Task 3: Export chat as markdown

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatSessionView.vue`
- Modify: `src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts`

**Interfaces:**
- Consumes: nothing new
- Produces: no new exports — internal `exportChat()` function only

- [ ] **Step 1: Write the failing test**

Append to `src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts` (new `describe` block, same file):

```typescript
describe('ChatSessionView — export', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({
      data: {
        ...baseSession,
        messages: [
          { id: 'm1', sessionId: 's1', role: 'User', content: 'Hi', inputTokens: 0, outputTokens: 0, cachedTokens: 0, modelName: null, imagesJson: null, createdAt: '2026-08-01T00:00:00Z' },
          { id: 'm2', sessionId: 's1', role: 'Assistant', content: 'Hello!', inputTokens: 0, outputTokens: 0, cachedTokens: 0, modelName: null, imagesJson: null, createdAt: '2026-08-01T00:01:00Z' }
        ]
      },
      error: undefined
    })
  })

  it('builds a markdown blob with role headers and triggers a download named after the session', async () => {
    const createObjectURL = vi.fn().mockReturnValue('blob:mock-url')
    const revokeObjectURL = vi.fn()
    Object.assign(URL, { createObjectURL, revokeObjectURL })

    const clickSpy = vi.fn()
    const originalCreateElement = document.createElement.bind(document)
    vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
      const el = originalCreateElement(tag)
      if (tag === 'a') el.click = clickSpy
      return el
    })

    const wrapper = await mountView()
    await wrapper.find('[title="Export chat"]').trigger('click')

    expect(createObjectURL).toHaveBeenCalled()
    const blob = createObjectURL.mock.calls[0][0] as Blob
    const text = await blob.text()
    expect(text).toContain('## User\n\nHi')
    expect(text).toContain('## Assistant\n\nHello!')
    expect(clickSpy).toHaveBeenCalled()
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:mock-url')

    vi.restoreAllMocks()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test app/components/chat/__tests__/ChatSessionView.test.ts`
Expected: FAIL — `[title="Export chat"]` doesn't exist yet.

- [ ] **Step 3: Implement export in `ChatSessionView.vue`**

Add `exportChat` after `submitTitleEdit` (from Task 1):

```typescript
function exportChat() {
  if (!session.value) return
  const markdown = session.value.messages
    .map((m) => {
      const heading = m.role === MessageRole.User
        ? '## User'
        : m.role === MessageRole.Assistant
          ? '## Assistant'
          : `## ${m.role}`
      return `${heading}\n\n${m.content}`
    })
    .join('\n\n')

  const safeTitle = session.value.title.replace(/[/\\?%*:|"<>]/g, '-') || 'chat'
  const blob = new Blob([markdown], { type: 'text/markdown' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${safeTitle}.md`
  a.click()
  URL.revokeObjectURL(url)
}
```

Add the export button in the header, after the rename button/`</template>` of the `v-else` block (after the rename `UButton`'s closing tag, still inside the header `<div>`):

```html
      <UButton
        icon="i-lucide-download"
        variant="ghost"
        color="neutral"
        size="xs"
        title="Export chat"
        @click="exportChat"
      />
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm test app/components/chat/__tests__/ChatSessionView.test.ts`
Expected: PASS — all tests in the file green (Task 1's 5 + this task's 1).

- [ ] **Step 5: Typecheck and lint**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: 0 errors, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/components/chat/ChatSessionView.vue src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts
git commit -m "feat(chat): add markdown chat export"
```

---

### Task 4: Find in conversation

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatSessionView.vue`
- Modify: `src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts`
- Modify: `src/web-ui/app/components/chat/ChatMessageList.vue`
- Modify: `src/web-ui/app/components/chat/ChatMessageBubble.vue`
- Modify: `src/web-ui/app/components/chat/__tests__/ChatMessageBubble.test.ts`

**Interfaces:**
- Consumes: nothing new
- Produces:
  - `ChatMessageBubble` gains prop `highlighted?: boolean` (default `false`) and renders `:id="\`chat-message-${message.id}\`"` on its root element
  - `ChatMessageList` gains prop `highlightMessageId?: string | null` (default `null`), passed to each `ChatMessageBubble` as `:highlighted="message.id === highlightMessageId"`
  - `ChatSessionView` gains internal `findOpen`, `findQuery`, `findIndex` refs and `findMatches`/`highlightMessageId` computed values — not exposed outside the component

- [ ] **Step 1: Write the failing tests**

Add to `src/web-ui/app/components/chat/__tests__/ChatMessageBubble.test.ts` (new `describe` block):

```typescript
describe('ChatMessageBubble — highlight', () => {
  it('applies a highlight ring class when highlighted is true', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: { message: baseMessage, highlighted: true }
    })
    expect(wrapper.find(`#chat-message-${baseMessage.id}`).classes()).toContain('ring-2')
  })

  it('does not apply the highlight ring class by default', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: { message: baseMessage }
    })
    expect(wrapper.find(`#chat-message-${baseMessage.id}`).classes()).not.toContain('ring-2')
  })
})
```

Add to `src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts` (new `describe` block). This uses a local stub for `ChatMessageList` (instead of `true`) so the test can assert what prop `ChatSessionView` passes down:

```typescript
describe('ChatSessionView — find in conversation', () => {
  const ChatMessageListStub = {
    props: ['messages', 'streamingMessage', 'streamError', 'awaitingReply', 'rollbackDisabled', 'highlightMessageId'],
    template: '<div data-testid="message-list" :data-highlight="highlightMessageId ?? \'\'" />'
  }

  const messages = [
    { id: 'm1', sessionId: 's1', role: 'User', content: 'find the needle here', inputTokens: 0, outputTokens: 0, cachedTokens: 0, modelName: null, imagesJson: null, createdAt: '2026-08-01T00:00:00Z' },
    { id: 'm2', sessionId: 's1', role: 'Assistant', content: 'no match', inputTokens: 0, outputTokens: 0, cachedTokens: 0, modelName: null, imagesJson: null, createdAt: '2026-08-01T00:01:00Z' },
    { id: 'm3', sessionId: 's1', role: 'User', content: 'another needle', inputTokens: 0, outputTokens: 0, cachedTokens: 0, modelName: null, imagesJson: null, createdAt: '2026-08-01T00:02:00Z' }
  ]

  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: { ...baseSession, messages }, error: undefined })
  })

  async function mountWithFindStub() {
    const wrapper = await mountSuspended(ChatSessionView, {
      props: { sessionId: 's1' },
      global: { stubs: { ...stubs, ChatMessageList: ChatMessageListStub } }
    })
    await flushPromises()
    return wrapper
  }

  it('opens a search input when the find button is clicked', async () => {
    const wrapper = await mountWithFindStub()
    await wrapper.find('[title="Find in conversation"]').trigger('click')
    expect(wrapper.find('input[data-testid="find-input"]').exists()).toBe(true)
  })

  it('typing a query highlights the first match and shows a 1 of N counter', async () => {
    const wrapper = await mountWithFindStub()
    await wrapper.find('[title="Find in conversation"]').trigger('click')
    await wrapper.find('input[data-testid="find-input"]').setValue('needle')
    await flushPromises()

    expect(wrapper.find('[data-testid="find-count"]').text()).toBe('1/2')
    expect(wrapper.find('[data-testid="message-list"]').attributes('data-highlight')).toBe('m1')
  })

  it('next/prev cycle through matches and wrap around', async () => {
    const wrapper = await mountWithFindStub()
    await wrapper.find('[title="Find in conversation"]').trigger('click')
    await wrapper.find('input[data-testid="find-input"]').setValue('needle')
    await flushPromises()

    await wrapper.find('[title="Next match"]').trigger('click')
    expect(wrapper.find('[data-testid="message-list"]').attributes('data-highlight')).toBe('m3')

    await wrapper.find('[title="Next match"]').trigger('click')
    expect(wrapper.find('[data-testid="message-list"]').attributes('data-highlight')).toBe('m1')

    await wrapper.find('[title="Previous match"]').trigger('click')
    expect(wrapper.find('[data-testid="message-list"]').attributes('data-highlight')).toBe('m3')
  })

  it('closing find clears the query and highlight', async () => {
    const wrapper = await mountWithFindStub()
    await wrapper.find('[title="Find in conversation"]').trigger('click')
    await wrapper.find('input[data-testid="find-input"]').setValue('needle')
    await flushPromises()

    await wrapper.find('[title="Close find"]').trigger('click')
    expect(wrapper.find('input[data-testid="find-input"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="message-list"]').attributes('data-highlight')).toBe('')
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/web-ui && pnpm test app/components/chat/__tests__/ChatMessageBubble.test.ts app/components/chat/__tests__/ChatSessionView.test.ts`
Expected: FAIL — no `highlighted` prop, no `#chat-message-*` id, no `[title="Find in conversation"]` button yet.

- [ ] **Step 3: Add `highlighted` prop to `ChatMessageBubble.vue`**

Add to the `defineProps` block (extend the existing `withDefaults`):

```typescript
const props = withDefaults(
  defineProps<{
    message: ChatMessageDto
    isStreaming?: boolean
    rollbackDisabled?: boolean
    highlighted?: boolean
  }>(),
  {
    isStreaming: false,
    rollbackDisabled: false,
    highlighted: false
  }
)
```

Change the root `<div>` (the outermost element, currently `<div class="group flex gap-3" :class="isUser ? ... ">`) to add the id and a conditional ring class:

```html
  <div
    :id="`chat-message-${message.id}`"
    class="group flex gap-3 rounded-lg"
    :class="[isUser ? 'flex-row-reverse' : 'flex-row', highlighted ? 'ring-2 ring-primary' : '']"
  >
```

- [ ] **Step 4: Add `highlightMessageId` prop to `ChatMessageList.vue`**

Add to the `withDefaults` props block:

```typescript
const props = withDefaults(
  defineProps<{
    messages: ChatMessageDto[]
    streamingMessage?: StreamingMessage | null
    streamError?: string | null
    awaitingReply?: boolean
    rollbackDisabled?: boolean
    highlightMessageId?: string | null
  }>(),
  {
    streamingMessage: null,
    streamError: null,
    awaitingReply: false,
    rollbackDisabled: false,
    highlightMessageId: null
  }
)
```

Update the `ChatMessageBubble` in the template to pass it through:

```html
        <ChatMessageBubble
          v-for="message in visibleMessages"
          :key="message.id"
          :message="message"
          :is-streaming="streamingMessage?.messageId === message.id"
          :rollback-disabled="rollbackDisabled"
          :highlighted="message.id === highlightMessageId"
          @rollback="emit('rollback', $event)"
        />
```

- [ ] **Step 5: Implement find-in-conversation in `ChatSessionView.vue`**

Add refs and computed values after `titleInputRef` (from Task 1):

```typescript
const findOpen = ref(false)
const findQuery = ref('')
const findIndex = ref(0)
const findInputRef = ref<HTMLInputElement | null>(null)

const findMatches = computed(() => {
  const q = findQuery.value.trim().toLowerCase()
  if (!q || !session.value) return []
  return session.value.messages.filter(m => m.content.toLowerCase().includes(q))
})

const highlightMessageId = computed(() => findMatches.value[findIndex.value]?.id ?? null)

watch(findQuery, () => {
  findIndex.value = 0
})

watch(findMatches, () => {
  scrollToCurrentMatch()
})

function scrollToCurrentMatch() {
  const id = highlightMessageId.value
  if (!id) return
  nextTick(() => {
    document.getElementById(`chat-message-${id}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
  })
}

function toggleFind() {
  findOpen.value = !findOpen.value
  if (findOpen.value) {
    nextTick(() => findInputRef.value?.focus())
  } else {
    findQuery.value = ''
    findIndex.value = 0
  }
}

function closeFind() {
  findOpen.value = false
  findQuery.value = ''
  findIndex.value = 0
}

function nextMatch() {
  if (!findMatches.value.length) return
  findIndex.value = (findIndex.value + 1) % findMatches.value.length
  scrollToCurrentMatch()
}

function prevMatch() {
  if (!findMatches.value.length) return
  findIndex.value = (findIndex.value - 1 + findMatches.value.length) % findMatches.value.length
  scrollToCurrentMatch()
}
```

Add the find button in the header, after the export button (from Task 3):

```html
      <UButton
        icon="i-lucide-search"
        variant="ghost"
        color="neutral"
        size="xs"
        title="Find in conversation"
        @click="toggleFind"
      />
```

Add a find bar as a new sibling `<div>` right after the header `<div>` (before the `v-if="loading"` block):

```html
    <div
      v-if="findOpen"
      class="shrink-0 border-b border-gray-200 dark:border-gray-700 px-4 py-2 flex items-center gap-2"
    >
      <input
        ref="findInputRef"
        v-model="findQuery"
        data-testid="find-input"
        placeholder="Find in conversation"
        class="flex-1 min-w-0 text-sm bg-transparent focus-visible:outline-none"
        @keydown.esc="closeFind"
      >
      <span
        v-if="findQuery"
        data-testid="find-count"
        class="text-xs text-muted shrink-0"
      >
        {{ findMatches.length ? `${findIndex + 1}/${findMatches.length}` : '0/0' }}
      </span>
      <UButton
        icon="i-lucide-chevron-up"
        variant="ghost"
        size="xs"
        title="Previous match"
        :disabled="!findMatches.length"
        @click="prevMatch"
      />
      <UButton
        icon="i-lucide-chevron-down"
        variant="ghost"
        size="xs"
        title="Next match"
        :disabled="!findMatches.length"
        @click="nextMatch"
      />
      <UButton
        icon="i-lucide-x"
        variant="ghost"
        size="xs"
        title="Close find"
        @click="closeFind"
      />
    </div>
```

Pass `highlight-message-id` to `ChatMessageList`:

```html
      <ChatMessageList
        :messages="session?.messages ?? []"
        :streaming-message="chatStream.streamingMessage.value"
        :stream-error="streamError"
        :awaiting-reply="awaitingReply"
        :rollback-disabled="isRollingBack || awaitingReply"
        :highlight-message-id="highlightMessageId"
        @rollback="handleRollbackRequest"
      />
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd src/web-ui && pnpm test app/components/chat/__tests__/ChatMessageBubble.test.ts app/components/chat/__tests__/ChatSessionView.test.ts`
Expected: PASS — all tests in both files green.

- [ ] **Step 7: Full verification**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test && pnpm build`
Expected: all green, 0 errors, 0 warnings.

- [ ] **Step 8: Commit**

```bash
git add src/web-ui/app/components/chat/ChatSessionView.vue src/web-ui/app/components/chat/ChatMessageList.vue src/web-ui/app/components/chat/ChatMessageBubble.vue src/web-ui/app/components/chat/__tests__/ChatSessionView.test.ts src/web-ui/app/components/chat/__tests__/ChatMessageBubble.test.ts
git commit -m "feat(chat): add find-in-conversation"
```
