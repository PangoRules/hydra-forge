# Chat Lifecycle Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplify the chat session lifecycle UI to a linear Active → Closed → Archived model: three-option filter (default Active), chat-type badges, close-button gating for project/card chats only, archive confirmation with deletion warning, and dock/full-view event symmetry.

**Architecture:** Frontend-only (Nuxt 4 web UI). Backend unchanged — `ChatSessionStatusFilter.ActiveAndClosed` stays for internal use. New shared lib `app/lib/chat-type.ts` computes chat type from `projectId`/`openCardId`. TUI parity is a follow-up (own plan), per the spec's deferral.

**Tech Stack:** Nuxt 4, Vue 3, Nuxt UI v4, Pinia, Vitest + `@nuxt/test-utils/runtime` (`mountSuspended`).

**Spec:** `docs/specs/2026-08-06-chat-lifecycle-simplification-design.md` (read first — authoritative).

**Branch:** execute on current `task/web-chat-amangers` branch

**Already done (committed on `task/web-chat-managers`, commit `3022253`):** `ChatSessionView` now emits `archiveSession(sessionId)` on archive (instead of `sessionRefreshed`), and `pages/chats/index.vue` consumes it via `archiveFromSession` (closes view + soft-removes from sidebar). Do not redo this — Tasks 5-6 build on it for the dock side.

**Verification commands (repo conventions):**
- Run web tests: `cd src/web-ui && pnpm test` (Vitest)
- Typecheck: `cd src/web-ui && pnpm typecheck`
- Lint: `cd src/web-ui && pnpm lint`

---

### Task 1: `app/lib/chat-type.ts` — chat type utility + badge metadata

**Files:**
- Create: `src/web-ui/app/lib/chat-type.ts`
- Test: `src/web-ui/app/lib/__tests__/chat-type.test.ts`

- [x] **Step 1: Write the failing test**

Create `src/web-ui/app/lib/__tests__/chat-type.test.ts`:

```typescript
import { describe, it, expect } from 'vitest'
import { getChatType, CHAT_TYPE_BADGE } from '~/lib/chat-type'

describe('getChatType', () => {
  it('returns "card" when both projectId and openCardId are set', () => {
    expect(getChatType({ projectId: 'p1', openCardId: 'c1' })).toBe('card')
  })

  it('returns "project" when only projectId is set', () => {
    expect(getChatType({ projectId: 'p1', openCardId: null })).toBe('project')
  })

  it('returns "normal" when neither is set', () => {
    expect(getChatType({ projectId: null, openCardId: null })).toBe('normal')
  })

  it('returns "normal" when only openCardId is set (impossible via API, but defensive)', () => {
    expect(getChatType({ projectId: null, openCardId: 'c1' })).toBe('normal')
  })
})

describe('CHAT_TYPE_BADGE', () => {
  it('has a label and color for every chat type', () => {
    expect(CHAT_TYPE_BADGE.normal).toEqual({ label: 'Chat', color: 'neutral' })
    expect(CHAT_TYPE_BADGE.project).toEqual({ label: 'Project', color: 'primary' })
    expect(CHAT_TYPE_BADGE.card).toEqual({ label: 'Card', color: 'success' })
  })
})
```

- [x] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test -- chat-type`
Expected: FAIL — `Cannot find module '~/lib/chat-type'`

- [x] **Step 3: Write the implementation**

Create `src/web-ui/app/lib/chat-type.ts`:

```typescript
/**
 * Chat type is never stored on the DTO — it is derived from which optional
 * scope FKs are set. A card chat always lives inside a project, so projectId
 * is implied when openCardId is set.
 */
export type ChatType = 'normal' | 'project' | 'card'

export function getChatType(session: { projectId: string | null, openCardId: string | null }): ChatType {
  if (session.projectId && session.openCardId) return 'card'
  if (session.projectId) return 'project'
  return 'normal'
}

/** Nuxt UI UBadge color + label per chat type. */
export const CHAT_TYPE_BADGE: Record<ChatType, { label: string, color: 'neutral' | 'primary' | 'success' }> = {
  normal: { label: 'Chat', color: 'neutral' },
  project: { label: 'Project', color: 'primary' },
  card: { label: 'Card', color: 'success' }
}
```

- [x] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm test -- chat-type`
Expected: PASS (5 tests)

- [x] **Step 5: Commit**

```bash
git add src/web-ui/app/lib/chat-type.ts src/web-ui/app/lib/__tests__/chat-type.test.ts
git commit -m "feat(chat): add chat-type util — getChatType + badge metadata"
```

---

### Task 2: `useChatSessionList` — default filter `Active`, drop `ActiveAndClosed`

**Files:**
- Modify: `src/web-ui/app/composables/useChatSessionList.ts:13,28-31`
- Test: `src/web-ui/app/composables/__tests__/useChatSessionList.test.ts`

- [x] **Step 1: Write the failing test**

Append to `src/web-ui/app/composables/__tests__/useChatSessionList.test.ts`, inside the existing `describe('useChatSessionList', ...)` block:

```typescript
  it('defaults to the Active filter and sends it as a query param', async () => {
    mockGET.mockResolvedValue({
      data: { items: [], totalCount: 0 },
      error: undefined
    })
    const { useChatSessionList } = await import('~/composables/useChatSessionList')
    const { statusFilter } = useChatSessionList()
    await flushMicrotasks()
    expect(statusFilter.value).toBe('Active')
    const calledUrl = mockGET.mock.calls[0]?.[0] as string
    expect(calledUrl).toContain('status=Active')
  })
```

- [x] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test -- useChatSessionList`
Expected: FAIL — `statusFilter.value` is `'ActiveAndClosed'` and the URL has no `status=` param

- [x] **Step 3: Update the composable**

In `src/web-ui/app/composables/useChatSessionList.ts`, replace line 13:

```typescript
  const statusFilter = ref<'ActiveAndClosed' | 'Active' | 'Closed' | 'Archived'>('ActiveAndClosed')
```

with:

```typescript
  // 'ActiveAndClosed' is deliberately NOT offered here — it stays in the
  // backend enum for internal use (folder cascade-archive), but the UI only
  // presents the linear lifecycle: Active (default) → Closed → Archived.
  const statusFilter = ref<'Active' | 'Closed' | 'Archived'>('Active')
```

Then replace the URL-building block (lines 28-31):

```typescript
      const statusParam = statusFilter.value !== 'ActiveAndClosed'
        ? `&status=${statusFilter.value}`
        : ''
      const url = `${base}${statusParam}`
```

with:

```typescript
      const url = `${base}&status=${statusFilter.value}`
```

- [x] **Step 4: Run tests to verify they pass**

Run: `cd src/web-ui && pnpm test -- useChatSessionList`
Expected: PASS (6 tests — 5 existing + 1 new)

- [x] **Step 5: Commit**

```bash
git add src/web-ui/app/composables/useChatSessionList.ts src/web-ui/app/composables/__tests__/useChatSessionList.test.ts
git commit -m "feat(chat): default session list filter to Active, drop ActiveAndClosed from UI type"
```

---

### Task 3: `pages/chats/index.vue` — filter dropdown, type badge, summary subtitle, archive dialog text

**Files:**
- Modify: `src/web-ui/app/pages/chats/index.vue:23-28` (filter items), `:210-249` (item slot), `:293-301` (archive ConfirmDialog)

Note: this page has no dedicated test file (page-level composition is covered by component tests on `ChatSessionHeader`/`ChatSessionList` and the composable test). Verification here is typecheck + lint + manual.

- [x] **Step 1: Filter dropdown — three options**

In `src/web-ui/app/pages/chats/index.vue`, replace the `statusFilterItems` declaration (lines 23-28):

```typescript
const statusFilterItems = [
  { label: 'Active & Closed', value: 'ActiveAndClosed' },
  { label: 'Active', value: 'Active' },
  { label: 'Closed', value: 'Closed' },
  { label: 'Archived', value: 'Archived' }
]
```

with:

```typescript
// Linear lifecycle only — 'ActiveAndClosed' stays in the backend enum for
// internal use but is not offered as a UI filter (see spec).
const statusFilterItems = [
  { label: 'Active', value: 'Active' },
  { label: 'Closed', value: 'Closed' },
  { label: 'Archived', value: 'Archived' }
]
```

The default comes from `useChatSessionList` (Task 2), so no other change is needed here — `statusFilter` now starts at `'Active'`.

- [x] **Step 2: Import the chat-type util**

Add to the imports at the top of the `<script setup>` block:

```typescript
import { getChatType, CHAT_TYPE_BADGE } from '~/lib/chat-type'
```

- [x] **Step 3: Type badge + summary subtitle in the sidebar item slot**

Replace the button content inside the `#item` template (lines 217-227 — the `<p class="truncate text-sm font-medium">` title and the status `<p>` below it):

```vue
                <p class="truncate text-sm font-medium">
                  {{ session.title }}
                </p>
                <!-- "Active" is every session's default state pre-close — showing it for
                     everything is just noise. Only surface the badge once it's meaningful. -->
                <p
                  v-if="session.status !== 'Active'"
                  class="text-xs text-muted"
                >
                  {{ session.status }}
                </p>
```

with:

```vue
                <div class="flex items-center gap-1.5 min-w-0">
                  <p class="truncate text-sm font-medium">
                    {{ session.title }}
                  </p>
                  <UBadge
                    :color="CHAT_TYPE_BADGE[getChatType(session)].color"
                    variant="subtle"
                    size="xs"
                    class="shrink-0"
                  >
                    {{ CHAT_TYPE_BADGE[getChatType(session)].label }}
                  </UBadge>
                </div>
                <!-- Closed chats preview their AI summary; other states keep the
                     old status line (meaningful only once not-Active). -->
                <p
                  v-if="session.status === 'Closed' && session.summary"
                  class="text-xs text-muted truncate"
                >
                  {{ session.summary }}
                </p>
                <p
                  v-else-if="session.status !== 'Active'"
                  class="text-xs text-muted"
                >
                  {{ session.status }}
                </p>
```

- [x] **Step 4: Archive ConfirmDialog — deletion-warning message**

Replace the page-level archive dialog (lines 293-301):

```vue
    <ConfirmDialog
      :open="archiveTargetId !== null"
      title="Archive chat"
      message="This chat will be archived and removed from your list. This can't be undone from here."
      confirm-text="Archive"
      confirm-color="error"
      @update:open="(v: boolean) => { if (!v) archiveTargetId = null }"
      @confirm="confirmArchive"
    />
```

with (retention days are admin-configurable — say "eventually deleted", never a number):

```vue
    <ConfirmDialog
      :open="archiveTargetId !== null"
      title="Archive chat"
      message="This chat will be archived and eventually deleted. You can unarchive it to restore it."
      confirm-text="Archive"
      confirm-color="warning"
      @update:open="(v: boolean) => { if (!v) archiveTargetId = null }"
      @confirm="confirmArchive"
    />
```

- [x] **Step 5: Typecheck + lint**

Run: `cd src/web-ui && pnpm typecheck && pnpm lint`
Expected: clean (no errors)

- [x] **Step 6: Commit**

```bash
git add src/web-ui/app/pages/chats/index.vue
git commit -m "feat(chat): three-option filter, type badge, summary subtitle, archive warning on /chats"
```

---

### Task 4: `ChatSessionHeader.vue` — gate Close on project chats, archive warning, type badge

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatSessionHeader.vue:102-108` (kebab Close item), `:203-211` (inline Close button), `:301-308` (archive ConfirmDialog), `:146-153` (title block — badge)
- Test: `src/web-ui/app/components/chat/__tests__/ChatSessionHeader.test.ts`

- [x] **Step 1: Write the failing tests**

Append to `src/web-ui/app/components/chat/__tests__/ChatSessionHeader.test.ts` as a new describe block (reuses the existing `makeSession` helper at the top of that file):

```typescript
describe('ChatSessionHeader — close button gating by chat type', () => {
  it('hides the inline Close button for normal chats (no projectId)', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ projectId: null }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(false)
  })

  it('shows the inline Close button for project chats', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ projectId: 'p1' }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(true)
  })

  it('shows the inline Close button for card chats', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ projectId: 'p1', openCardId: 'c1' }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(true)
  })

  it('excludes Close from the compact kebab menu for normal chats', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ projectId: null }), isOwner: true, compact: true }
    })
    await flushPromises()
    const items = (wrapper.vm as unknown as {
      compactMenuItems: Array<Array<{ label: string }>>
    }).compactMenuItems
    expect(items.flat().map(i => i.label)).not.toContain('Close chat')
  })

  it('includes Close in the compact kebab menu for project chats', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ projectId: 'p1' }), isOwner: true, compact: true }
    })
    await flushPromises()
    const items = (wrapper.vm as unknown as {
      compactMenuItems: Array<Array<{ label: string }>>
    }).compactMenuItems
    expect(items.flat().map(i => i.label)).toContain('Close chat')
  })
})
```

- [x] **Step 2: Run tests to verify they fail**

Run: `cd src/web-ui && pnpm test -- ChatSessionHeader`
Expected: FAIL — normal-chat cases currently show/offer Close

- [x] **Step 3: Gate the kebab Close item on `projectId`**

In `ChatSessionHeader.vue`, inside `compactMenuItems`, change the lifecycle block (lines 102-108) from:

```typescript
    if (isActive.value) {
      lifecycle.push({
        label: 'Close chat',
        icon: 'i-lucide-check-circle',
        onSelect: () => { showCloseConfirm.value = true }
      })
    }
```

to:

```typescript
    // Close is only meaningful for project/card chats — closing a normal
    // chat produces no summary and no card link, so archiving is the only
    // "finished" path there (see spec: Actions by chat type).
    if (isActive.value && props.session.projectId) {
      lifecycle.push({
        label: 'Close chat',
        icon: 'i-lucide-check-circle',
        onSelect: () => { showCloseConfirm.value = true }
      })
    }
```

- [x] **Step 4: Gate the inline Close button on `projectId`**

Change the inline button (line 204) from:

```vue
    <UButton
      v-if="!compact && isOwner && isActive"
      icon="i-lucide-check-circle"
```

to:

```vue
    <UButton
      v-if="!compact && isOwner && isActive && session.projectId"
      icon="i-lucide-check-circle"
```

- [x] **Step 5: Archive ConfirmDialog — deletion-warning message + warning color**

Replace the archive dialog (lines 301-308):

```vue
    <ConfirmDialog
      v-model:open="showArchiveConfirm"
      title="Archive chat"
      message="This chat will be archived. You can reopen it later from the chat list."
      confirm-text="Archive"
      confirm-color="error"
      @confirm="confirmArchive"
    />
```

with:

```vue
    <ConfirmDialog
      v-model:open="showArchiveConfirm"
      title="Archive chat"
      message="This chat will be archived and eventually deleted. You can unarchive it to restore it."
      confirm-text="Archive"
      confirm-color="warning"
      @confirm="confirmArchive"
    />
```

- [x] **Step 6: Type badge next to the title (non-compact only — compact dock header has no room)**

Add the import at the top of `<script setup>`:

```typescript
import { getChatType, CHAT_TYPE_BADGE } from '~/lib/chat-type'
```

Then change the title `<h2>` block (lines 146-153) from:

```vue
    <h2
      class="font-semibold truncate flex-1 min-w-0 text-sm"
      :class="compact && isOwner && isActive ? 'cursor-pointer hover:text-primary' : ''"
      :title="compact && isOwner && isActive ? 'Click to rename' : undefined"
      @click="compact && isOwner && isActive ? emit('startEditTitle') : undefined"
    >
      {{ session.title || 'Chat' }}
    </h2>
```

to:

```vue
    <div class="flex items-center gap-1.5 flex-1 min-w-0">
      <h2
        class="font-semibold truncate min-w-0 text-sm"
        :class="compact && isOwner && isActive ? 'cursor-pointer hover:text-primary' : ''"
        :title="compact && isOwner && isActive ? 'Click to rename' : undefined"
        @click="compact && isOwner && isActive ? emit('startEditTitle') : undefined"
      >
        {{ session.title || 'Chat' }}
      </h2>
      <UBadge
        v-if="!compact"
        :color="CHAT_TYPE_BADGE[getChatType(session)].color"
        variant="subtle"
        size="xs"
        class="shrink-0"
      >
        {{ CHAT_TYPE_BADGE[getChatType(session)].label }}
      </UBadge>
    </div>
```

(The `flex-1` moves from the `<h2>` to the wrapper `<div>` so the title still claims the free space and truncates correctly.)

- [x] **Step 7: Run tests to verify they pass**

Run: `cd src/web-ui && pnpm test -- ChatSessionHeader`
Expected: PASS (all existing + 5 new)

- [x] **Step 8: Commit**

```bash
git add src/web-ui/app/components/chat/ChatSessionHeader.vue src/web-ui/app/components/chat/__tests__/ChatSessionHeader.test.ts
git commit -m "feat(chat): gate Close on project/card chats, archive deletion warning, type badge in header"
```

---

### Task 5: `ChatDockHistory.vue` — type badge per history item

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatDockHistory.vue:34-46` (item slot)
- Test: `src/web-ui/app/components/chat/__tests__/ChatDockHistory.test.ts`

**Spec deviation (deliberate):** the spec asks ChatDockHistory to also soft-remove archived items and patch on close/reopen. In the dock this is dead code: `ChatDockHistory` and `ChatSessionView` are mounted under mutually exclusive `v-if="dock.mode === ..."` branches in `ChatDock.vue`, so the history list is rebuilt (fresh page-1 fetch via `useChatSessionList`) every time the user opens it, and no archive/close trigger exists while history is visible. A freshly mounted list can never contain stale archived/closed state. Only the badge is needed here.

The summary subtitle is already present (`{{ session.summary || 'No messages' }}` on line 43) — no change needed for that spec point.

- [x] **Step 1: Write the failing test**

Append to `src/web-ui/app/components/chat/__tests__/ChatDockHistory.test.ts` (reuse the file's existing session-fixture helper and mount pattern; the mock API must return a session with `projectId: 'p1'` and `openCardId: null`):

```typescript
  it('shows a type badge on each history item', async () => {
    // Arrange: existing helper/fixture that mounts ChatDockHistory with one
    // session — give it projectId: 'p1', openCardId: null (a project chat).
    const wrapper = await mountWithSessions([
      { id: 's1', title: 'Proj chat', projectId: 'p1', openCardId: null }
    ])
    await flushPromises()
    expect(wrapper.text()).toContain('Project')
  })
```

If the existing test file's fixture helper has a different name/signature, adapt the arrange step to it — the assertion (`wrapper.text()` contains the badge label) stays.

- [x] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test -- ChatDockHistory`
Expected: FAIL — no badge rendered

- [x] **Step 3: Add the badge to the item slot**

Add the import at the top of `<script setup>`:

```typescript
import { getChatType, CHAT_TYPE_BADGE } from '~/lib/chat-type'
```

Then replace the title `<div>` (line 39-41):

```vue
          <div class="text-sm font-medium truncate">
            {{ session.title || 'New Chat' }}
          </div>
```

with:

```vue
          <div class="flex items-center gap-1.5 min-w-0">
            <span class="text-sm font-medium truncate">
              {{ session.title || 'New Chat' }}
            </span>
            <UBadge
              :color="CHAT_TYPE_BADGE[getChatType(session)].color"
              variant="subtle"
              size="xs"
              class="shrink-0"
            >
              {{ CHAT_TYPE_BADGE[getChatType(session)].label }}
            </UBadge>
          </div>
```

- [x] **Step 4: Run tests to verify they pass**

Run: `cd src/web-ui && pnpm test -- ChatDockHistory`
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add src/web-ui/app/components/chat/ChatDockHistory.vue src/web-ui/app/components/chat/__tests__/ChatDockHistory.test.ts
git commit -m "feat(chat): type badge in dock history items"
```

---

### Task 6: `ChatDock.vue` — consume `archive-session` (return to draft on archive)

**Files:**
- Modify: `src/web-ui/app/components/chat/ChatDock.vue:223-236` (ChatSessionView usage)
- Test: `src/web-ui/app/components/chat/__tests__/ChatDock.test.ts`

**Spec deviation (deliberate):** the spec asks the dock to listen to both `@session-refreshed` and `@archive-session` and patch the history list in place. Only `@archive-session` is needed: (a) on close/reopen, `ChatSessionView` already mutates its own `session` ref, which the compact header reads reactively through `sessionViewRef.session` — the open view updates with no listener; (b) the dock's history list is never mounted at the same time as the session view (`v-if` mode branches), and remounting it always fetches a fresh first page — there is no stale list to patch. Wiring a listener for it would be dead code.

- [x] **Step 1: Write the failing test**

Append to `src/web-ui/app/components/chat/__tests__/ChatDock.test.ts`, following the file's existing mount/stub pattern (Pinia via `setActivePinia(createPinia())`, dock store opened into session mode, `ChatSessionView` stubbed or shallow-mounted — match what the existing tests in that file do):

```typescript
  it('returns to draft mode when the active session is archived from inside the view', async () => {
    // Arrange per existing pattern: real chatDock store, isOpen = true,
    // mode = 'session', activeSessionId = 's1'.
    const wrapper = await mountDockInSessionMode('s1')
    const dock = useChatDockStore()

    // Act: the session view fires archive-session after a successful archive.
    await wrapper.findComponent({ name: 'ChatSessionView' }).vm.$emit('archiveSession', 's1')
    await flushPromises()

    expect(dock.activeSessionId).toBeNull()
    expect(dock.mode).toBe('draft')
  })
```

If the existing file doesn't stub `ChatSessionView` by name, stub it explicitly in the mount options:

```typescript
global: { stubs: { ChatSessionView: { name: 'ChatSessionView', template: '<div />' } } }
```

- [x] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test -- ChatDock`
Expected: FAIL — dock stays in session mode with the archived id

- [x] **Step 3: Wire the listener**

In `ChatDock.vue`, add the handler to the script (after `sendDraftMessage`):

```typescript
// Archive fired from inside the open session view — the API call already
// happened in ChatSessionView.handleArchiveSession(). The archived session
// must not stay open in the dock: drop back to draft mode (this also clears
// the localStorage resume key via the store's activeSessionId watcher). The
// history list needs no patch here — it remounts with a fresh fetch every
// time the user opens it (v-if mode branches), so it can never be stale.
function onArchiveFromSession() {
  dock.newChat()
}
```

Then on the `<ChatSessionView>` in the dock body (around line 223), add the listener:

```vue
          <ChatSessionView
            v-if="dock.mode === 'session' && dock.activeSessionId"
            ref="sessionViewRef"
            :key="dock.activeSessionId"
            :session-id="dock.activeSessionId"
            :feature="dock.currentProjectId ? 'ProjectChat' : 'PersonalChat'"
            :initial-message="dock.pendingMessage?.content ?? null"
            :auto-send-initial="!!dock.pendingMessage"
            :initial-preset-id="dock.pendingMessage?.presetId ?? null"
            :initial-model-id="dock.pendingMessage?.modelId ?? null"
            :initial-effort="dock.pendingMessage?.reasoningEffort ?? null"
            compact
            @initial-message-sent="dock.clearPendingMessage()"
            @archive-session="onArchiveFromSession"
          />
```

(Only the last `@archive-session` line is new — the rest is shown for placement.)

- [x] **Step 4: Run tests to verify they pass**

Run: `cd src/web-ui && pnpm test -- ChatDock`
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add src/web-ui/app/components/chat/ChatDock.vue src/web-ui/app/components/chat/__tests__/ChatDock.test.ts
git commit -m "fix(chat): dock returns to draft mode when active session is archived"
```

---

### Final verification

- [x] Run the full web suite: `cd src/web-ui && pnpm test` — all PASS
- [x] Run: `cd src/web-ui && pnpm typecheck` — clean
- [x] Run: `cd src/web-ui && pnpm lint` — clean
- [ ] Manual smoke (dev server + API up): filter dropdown shows Active/Closed/Archived, defaults to Active; normal chat shows no Close button in header (both page and dock); project chat shows Close; archive from dock header returns dock to draft; badges show in sidebar + dock history; closed chat with summary shows summary subtitle in sidebar.

---

## TUI parity (follow-up — separate plan)

Per the spec ("Web UI changes come first, TUI parity second"), the TUI work is deferred to its own plan. When that plan is written it must cover, in `src/HydraForge.Tui`:

1. Filter options in the chat list: Active / Closed / Archived (drop Active & Closed).
2. Close action gated to project/card chats only (`ProjectId != null`).
3. Chat-type indicator in the TUI chat list (Normal/Project/Card, derived from `ProjectId`/`OpenCardId`).
4. Archive confirmation text warning about eventual deletion.

## Spec coverage map

- Three-state model + filter changes (Active default, drop ActiveAndClosed) → Tasks 2, 3
- Chat types + Close gating (projectId check, both modes) → Task 4 (Close button is in the shared `ChatSessionHeader`, so both page and dock inherit the gating)
- Type badge (`/chats` sidebar, dock history, header) → Tasks 1, 3, 4, 5
- Summary subtitle (sidebar) → Task 3; dock history already had it
- Archive confirm modal (deletion warning, warning color; message in both page-level and header dialogs) → Tasks 3, 4
- Symmetric dock/full-view behavior: archive event → Task 6 (+ `3022253`, already committed); close/reopen refresh → no listener needed (documented in Task 6)
- Backend changes → none (spec confirms)
- Out of scope per spec: schema, backend enum removal, retention logic, F6 auto-close
