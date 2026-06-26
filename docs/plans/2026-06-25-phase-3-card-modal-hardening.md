# Card Modal Hardening — Error Handling, Version Ownership, Shared Utilities Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Branch:** `task/phase-3-card-modal-hardening`
**Parent branch:** `feat/phase-3-web-ui`
**Parent spec:** `2026-06-23-phase-3-web-ui-design.md` — Task 3A

**Goal:** Fix a silent-failure bug in every component that calls `useApi()` without a try/catch, move card-version ownership out of `CardDescription` and into `CardModal` (so Plan 4's metadata editor doesn't reintroduce stale-version 409s), de-duplicate the card-type/due-date logic that has been copy-pasted into three components, and close the component-test gaps this exposed.

**Architecture:** `useApi()` (see `app/composables/useApi.ts`) **throws** `ApiError` on any non-2xx response — it never resolves with a populated `error` field. Several existing call sites destructure `{ error }` from the awaited call as if it were a Result-style resolve, with no surrounding try/catch. On a real failure the `await` throws *before* the destructuring assigns anything, the function's promise rejects unhandled, and the "Failed to X" toast the manual test matrix documents never fires. This plan fixes every such call site, then uses the same pass to lift `CardResponse`/`version` ownership to `CardModal.vue` (children emit `update:card` instead of caching their own version), and extracts `lib/card-type.ts` + `lib/date.ts` so Plan 4's Task 16 (Card Metadata Editor) and the rest of the codebase share one source of truth for card-type labels and due-date formatting.

**Tech Stack:** Vue 3 `<script setup>`, Vitest, `@vue/test-utils`, `@nuxt/test-utils/runtime` (`mockNuxtImport`, `mountSuspended`)

## Global Constraints

- `useApi()` throws `ApiError` (extends `Error`) on non-2xx responses — never resolves with `{ error }` populated. Every call site must wrap the call in try/catch. Destructuring `{ data, error }` and checking `if (error)` without a surrounding try/catch is the bug this plan exists to remove — do not introduce it anywhere else.
- API paths come from `ApiRoutes` (`app/lib/routes.ts`) — never inline path strings, never the raw `params: { path: {...} }` openapi-fetch style.
- `xUnit`/Vitest only — no FluentAssertions equivalents; plain `expect()` assertions.
- `card.value`/`props.card` is the single source of truth for a card's `version` inside the modal — no component below `CardModal.vue` may keep its own cached copy of `version`.
- No `console.log`/`console.error`/`console.warn` — user-facing feedback goes through `useToast()`.

---

## Task 1: Fix Silent Failures on `useApi()` Errors

**Files:**
- Modify: `src/web-ui/app/components/card/CardModal.vue`
- Modify: `src/web-ui/app/components/board/BoardCard.vue`
- Modify: `src/web-ui/app/components/board/CardCreateModal.vue`
- Test: `src/web-ui/app/components/card/__tests__/CardModal.test.ts`
- Test: `src/web-ui/app/components/board/__tests__/BoardCard.test.ts`
- Create: `src/web-ui/app/components/board/__tests__/CardCreateModal.test.ts`

**Interfaces:**
- Consumes: `useApi()` → `{ GET, POST, PUT, DELETE }` (each throws `ApiError` on non-2xx, per `app/composables/useApi.ts`).
- Produces: no new public interfaces — this task only changes error-handling control flow inside existing functions (`confirmArchive`, `handleRestore` in both `CardModal.vue` and `BoardCard.vue`; `handleCreate` in `CardCreateModal.vue`).

### Step 1: Write the failing test for `CardModal` archive failure

Create `src/web-ui/app/components/card/__tests__/CardModal.test.ts` (replacing its current contents):

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CardModal from '~/components/card/CardModal.vue'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import { ApiError } from '~/lib/api-error'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

const mockGET = vi.fn()
const mockPOST = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: mockPOST,
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

function makeCard(overrides: Partial<CardResponse> = {}): CardResponse {
  return {
    id: 'c1',
    projectId: 'p1',
    columnId: 'col1',
    cardNumber: 1,
    title: 'Test card',
    description: '',
    type: 0,
    position: 0,
    dueAt: null,
    version: 1,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
    movedAt: '2024-01-01T00:00:00Z',
    archivedAt: null,
    parentCardId: null,
    assignees: [],
    watchers: [],
    ...overrides
  }
}

async function mountLoadedModal() {
  mockGET.mockResolvedValue({ data: makeCard(), error: undefined })
  const wrapper = await mountSuspended(CardModal, {
    props: { cardId: 'c1', projectId: 'p1' },
    global: { stubs: { AppModal: true, CardDescription: true, CardMetadata: true } }
  })
  await flushPromises()
  return wrapper
}

describe('CardModal', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockPOST.mockReset()
    mockToastAdd.mockReset()
  })

  it('mounts without error', async () => {
    const wrapper = await mountLoadedModal()
    expect(wrapper.vm).toBeTruthy()
  })

  it('passes the fetch error through to AppModal when the card fails to load', async () => {
    mockGET.mockRejectedValue(new ApiError(404, 'CARD_NOT_FOUND', 'Not Found', 'Card does not exist', 'about:blank', 'corr-1'))
    const wrapper = await mountSuspended(CardModal, {
      props: { cardId: 'missing', projectId: 'p1' },
      global: { stubs: { AppModal: true, CardDescription: true, CardMetadata: true } }
    })
    await flushPromises()
    expect(wrapper.findComponent({ name: 'AppModal' }).props('error')).toBeTruthy()
  })

  it('archives the card and emits archived on confirm', async () => {
    mockPOST.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountLoadedModal()

    await wrapper.find('[title="Archive card"]').trigger('click')
    await wrapper.findComponent(ConfirmDialog).vm.$emit('confirm')
    await flushPromises()

    expect(mockPOST).toHaveBeenCalledWith(
      expect.stringContaining('/archive'),
      expect.objectContaining({ body: { version: 1 } })
    )
    expect(wrapper.emitted('archived')).toBeTruthy()
  })

  it('shows an error toast and does not emit archived when the archive call fails', async () => {
    mockPOST.mockRejectedValue(new ApiError(409, 'CARD_CONCURRENCY_MISMATCH', 'Conflict', 'stale version', 'about:blank', 'corr-2'))
    const wrapper = await mountLoadedModal()

    await wrapper.find('[title="Archive card"]').trigger('click')
    await wrapper.findComponent(ConfirmDialog).vm.$emit('confirm')
    await flushPromises()

    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to archive card', color: 'error' }))
    expect(wrapper.emitted('archived')).toBeFalsy()
  })

  it('shows an error toast when restore fails', async () => {
    mockPOST.mockRejectedValue(new ApiError(500, 'UNKNOWN', 'Server Error', null, 'about:blank', 'corr-3'))
    const wrapper = await mountSuspended(CardModal, {
      props: { cardId: 'c1', projectId: 'p1' },
      global: { stubs: { AppModal: true, CardDescription: true, CardMetadata: true } }
    })
    mockGET.mockResolvedValue({ data: makeCard({ archivedAt: '2024-02-01T00:00:00Z' }), error: undefined })
    await flushPromises()

    await wrapper.find('[title="Restore card"]').trigger('click')
    await flushPromises()

    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to restore card', color: 'error' }))
    expect(wrapper.emitted('restored')).toBeFalsy()
  })
})
```

### Step 2: Run the test to verify it fails

Run: `cd src/web-ui && pnpm vitest run app/components/card/__tests__/CardModal.test.ts`
Expected: FAIL — the "shows an error toast and does not emit archived" and "shows an error toast when restore fails" tests fail because `confirmArchive`/`handleRestore` currently have no try/catch, so the rejected promise from `mockPOST` propagates as an unhandled rejection instead of reaching `toast.add`.

### Step 3: Fix `CardModal.vue`

In `src/web-ui/app/components/card/CardModal.vue`, replace the `confirmArchive` and `handleRestore` functions:

```ts
async function confirmArchive() {
  try {
    await api.POST(ApiRoutes.Cards.archive(props.projectId, card.value!.id), {
      body: { version: card.value!.version }
    })
    emit('archived')
    closeWithAnimation()
  } catch {
    toast.add({ title: 'Failed to archive card', color: 'error' })
  }
}

async function handleRestore() {
  try {
    await api.POST(ApiRoutes.Cards.restore(props.projectId, card.value!.id), {})
    toast.add({ title: 'Card restored', color: 'success' })
    emit('restored')
    closeWithAnimation()
  } catch {
    toast.add({ title: 'Failed to restore card', color: 'error' })
  }
}
```

### Step 4: Run the test to verify it passes

Run: `cd src/web-ui && pnpm vitest run app/components/card/__tests__/CardModal.test.ts`
Expected: PASS — all 5 tests green.

### Step 5: Write the failing test for `BoardCard` archive/restore failure

Replace the top of `src/web-ui/app/components/board/__tests__/BoardCard.test.ts` (everything before the `describe` block) and add two new tests inside it:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import BoardCard from '~/components/board/BoardCard.vue'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import { ApiError } from '~/lib/api-error'

const mockPOST = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

const makeCard = (overrides = {}) => ({
  id: 'c1',
  projectId: 'p1',
  columnId: 'col1',
  cardNumber: 42,
  title: 'Test Card',
  description: 'Test description',
  type: 0,
  position: 0,
  dueAt: null,
  version: 1,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  movedAt: new Date().toISOString(),
  archivedAt: null,
  parentCardId: null,
  assignees: [],
  watchers: [],
  ...overrides,
})
```

Then, inside the existing `describe('BoardCard', ...)` block, add a `beforeEach` and two new tests (keep every existing `it(...)` in that file as-is):

```ts
beforeEach(() => {
  mockPOST.mockReset()
  mockToastAdd.mockReset()
})

it('shows an error toast and does not remove the card when archive fails', async () => {
  mockPOST.mockRejectedValue(new ApiError(409, 'CARD_CONCURRENCY_MISMATCH', 'Conflict', null, 'about:blank', 'corr-1'))
  const wrapper = await mountSuspended(BoardCard, { props: { card: makeCard(), projectId: 'p1' } })

  await wrapper.find('button').trigger('click') // three-dot menu toggle
  const archiveBtn = wrapper.findAll('button').find(b => b.text() === 'Archive')!
  await archiveBtn.trigger('click')
  await wrapper.findComponent(ConfirmDialog).vm.$emit('confirm')
  await flushPromises()

  expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to archive card', color: 'error' }))
})

it('shows an error toast when restore fails', async () => {
  mockPOST.mockRejectedValue(new ApiError(500, 'UNKNOWN', 'Server Error', null, 'about:blank', 'corr-2'))
  const wrapper = await mountSuspended(BoardCard, {
    props: { card: makeCard({ archivedAt: new Date().toISOString() }), projectId: 'p1' }
  })

  await wrapper.find('button').trigger('click') // three-dot menu toggle
  const restoreBtn = wrapper.findAll('button').find(b => b.text() === 'Restore')!
  await restoreBtn.trigger('click')
  await flushPromises()

  expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to restore card', color: 'error' }))
})
```

### Step 6: Run the test to verify it fails

Run: `cd src/web-ui && pnpm vitest run app/components/board/__tests__/BoardCard.test.ts`
Expected: FAIL — same unhandled-rejection failure mode as `CardModal`.

### Step 7: Fix `BoardCard.vue`

In `src/web-ui/app/components/board/BoardCard.vue`, replace `confirmArchive` and `handleRestore`:

```ts
async function confirmArchive() {
  try {
    await api.POST(ApiRoutes.Cards.archive(props.projectId, props.card.id), {
      body: { version: props.card.version }
    })
    board.removeCard(props.card.id)
    toast.add({ title: 'Card archived', color: 'success' })
  } catch {
    toast.add({ title: 'Failed to archive card', color: 'error' })
  }
}

async function handleRestore() {
  closeMenu()
  try {
    await api.POST(ApiRoutes.Cards.restore(props.projectId, props.card.id), {})
    toast.add({ title: 'Card restored', color: 'success' })
  } catch {
    toast.add({ title: 'Failed to restore card', color: 'error' })
  }
}
```

Note `confirmArchive` already calls `closeMenu()` earlier in `handleArchive` — keep that as-is, only the two functions above change.

### Step 8: Run the test to verify it passes

Run: `cd src/web-ui && pnpm vitest run app/components/board/__tests__/BoardCard.test.ts`
Expected: PASS.

### Step 9: Write the failing test for `CardCreateModal` (no test file exists yet)

Create `src/web-ui/app/components/board/__tests__/CardCreateModal.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CardCreateModal from '~/components/board/CardCreateModal.vue'
import { ApiError } from '~/lib/api-error'

const mockPOST = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

const columns = [{ id: 'col1', name: 'To Do', position: 0, wipLimit: null, color: null }]

describe('CardCreateModal', () => {
  beforeEach(() => {
    mockPOST.mockReset()
    mockToastAdd.mockReset()
  })

  it('creates a card and emits created on success', async () => {
    mockPOST.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(CardCreateModal, {
      props: { projectId: 'p1', columns, preselectedColumnId: 'col1' }
    })

    await wrapper.find('input').setValue('New card')
    await wrapper.findAll('button').find(b => b.text() === 'Create')!.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('created')).toBeTruthy()
    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Card created', color: 'success' }))
  })

  it('shows an error toast and does not emit created when the API call fails', async () => {
    mockPOST.mockRejectedValue(new ApiError(400, 'VALIDATION_ERROR', 'Bad Request', 'Title is required', 'about:blank', 'corr-1'))
    const wrapper = await mountSuspended(CardCreateModal, {
      props: { projectId: 'p1', columns, preselectedColumnId: 'col1' }
    })

    await wrapper.find('input').setValue('New card')
    await wrapper.findAll('button').find(b => b.text() === 'Create')!.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('created')).toBeFalsy()
    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Bad Request', color: 'error' }))
  })
})
```

### Step 10: Run the test to verify it fails

Run: `cd src/web-ui && pnpm vitest run app/components/board/__tests__/CardCreateModal.test.ts`
Expected: FAIL — same unhandled-rejection failure mode; the second test fails because no toast fires today.

### Step 11: Fix `CardCreateModal.vue`

In `src/web-ui/app/components/board/CardCreateModal.vue`, add the `ApiError` import and replace `handleCreate`:

```ts
import { ApiError } from '~/lib/api-error'
```

```ts
async function handleCreate() {
  if (!canSave.value) return
  saving.value = true
  const body: Record<string, unknown> = {
    columnId: columnId.value,
    title: title.value.trim(),
    description: description.value,
    type: CARD_TYPE_MAP[cardType.value] ?? 'Task'
  }
  if (dueAt.value) {
    body.dueAt = `${dueAt.value}T00:00:00Z`
  }
  if (selectedAssignees.value.length > 0) {
    body.assigneeUserIds = selectedAssignees.value.map(id => id)
  }
  try {
    await api.POST(ApiRoutes.Cards.create(props.projectId), { body })
    toast.add({ title: 'Card created', color: 'success' })
    emit('created')
    closeWithAnimation()
  } catch (e: unknown) {
    toast.add({ title: e instanceof ApiError ? e.message : 'Failed to create card', color: 'error' })
  } finally {
    saving.value = false
  }
}
```

### Step 12: Run the test to verify it passes

Run: `cd src/web-ui && pnpm vitest run app/components/board/__tests__/CardCreateModal.test.ts`
Expected: PASS.

### Step 13: Full verification

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test
```
Expected: zero errors, all tests pass.

### Step 14: Commit

```bash
git add src/web-ui/app/components/card/CardModal.vue src/web-ui/app/components/card/__tests__/CardModal.test.ts \
  src/web-ui/app/components/board/BoardCard.vue src/web-ui/app/components/board/__tests__/BoardCard.test.ts \
  src/web-ui/app/components/board/CardCreateModal.vue src/web-ui/app/components/board/__tests__/CardCreateModal.test.ts
git commit -m "fix: wrap throw-based useApi() calls in try/catch so failure toasts actually fire"
```

---

## Task 2: Lift Card-Version Ownership to `CardModal`

**Files:**
- Modify: `src/web-ui/app/components/card/CardModal.vue`
- Modify: `src/web-ui/app/components/card/CardDescription.vue`
- Modify: `src/web-ui/app/components/card/__tests__/CardDescription.test.ts`

**Interfaces:**
- Consumes: `CardResponse` from `~/types/api` (already used throughout).
- Produces: `CardDescription` now emits `'update:card': [card: CardResponse]` after every successful save. `CardModal` exposes `applyCardUpdate(updated: CardResponse)` internally and wires it to `@update:card` on `CardDescription`. This is the contract Plan 4's Task 16 (Card Metadata Editor) must also use — see the updated Task 16 in `2026-06-23-phase-3-plan-4-card-modal-panels.md`.

**Why:** `CardDescription.vue` currently keeps its own `currentVersion` ref, seeded from `props.card.version` once at mount and updated only when *its own* save succeeds. Plan 4's metadata editor will add a second panel that also calls `PUT /Cards/{cardId}` with a version. Two independent cached copies of the same optimistic-concurrency token will desync the moment a user edits the description and then the type (or vice versa) in the same modal session — the second save will send a stale version and get a spurious `409 CARD_CONCURRENCY_MISMATCH`. There must be exactly one source of truth: `card.value` on `CardModal`.

### Step 1: Write the failing test

Modify `src/web-ui/app/components/card/__tests__/CardDescription.test.ts` — replace its contents:

```ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CardDescription from '~/components/card/CardDescription.vue'
import MarkdownEditor from '~/components/shared/MarkdownEditor.vue'
import { ApiRoutes } from '~/lib/routes'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

const mockPUT = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: vi.fn(),
  PUT: mockPUT,
  DELETE: vi.fn()
}))

function makeCard(overrides: Partial<CardResponse> = {}): CardResponse {
  return {
    id: 'c1',
    projectId: 'p1',
    columnId: 'col1',
    cardNumber: 1,
    title: 'Test',
    description: 'Initial description',
    type: 0,
    position: 0,
    dueAt: null,
    version: 1,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
    movedAt: '2024-01-01T00:00:00Z',
    archivedAt: null,
    parentCardId: null,
    assignees: [],
    watchers: [],
    ...overrides
  }
}

describe('CardDescription', () => {
  beforeEach(() => {
    mockPUT.mockReset()
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('renders description label', async () => {
    const wrapper = await mountSuspended(CardDescription, {
      props: { card: makeCard(), projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Description')
  })

  it('renders markdown editor with card description', async () => {
    const wrapper = await mountSuspended(CardDescription, {
      props: { card: makeCard(), projectId: 'p1' }
    })
    expect(wrapper.findComponent(MarkdownEditor).exists()).toBe(true)
  })

  it('uses the version from the card prop on save, not a value cached at mount', async () => {
    mockPUT.mockResolvedValue({ data: makeCard({ version: 99 }), error: undefined })
    const wrapper = await mountSuspended(CardDescription, {
      props: { card: makeCard({ version: 7 }), projectId: 'p1' }
    })

    // Simulate a sibling panel (e.g. the metadata editor) saving first and
    // bumping the version before the description save fires.
    await wrapper.setProps({ card: makeCard({ version: 42 }) })

    await wrapper.findComponent(MarkdownEditor).vm.$emit('update:modelValue', 'edited')
    vi.advanceTimersByTime(2000)
    await flushPromises()

    expect(mockPUT).toHaveBeenCalledWith(
      ApiRoutes.Cards.update('p1', 'c1'),
      expect.objectContaining({ body: expect.objectContaining({ version: 42 }) })
    )
  })

  it('emits update:card with the server response after a successful save', async () => {
    const updated = makeCard({ version: 2, description: 'edited' })
    mockPUT.mockResolvedValue({ data: updated, error: undefined })
    const wrapper = await mountSuspended(CardDescription, {
      props: { card: makeCard(), projectId: 'p1' }
    })

    await wrapper.findComponent(MarkdownEditor).vm.$emit('update:modelValue', 'edited')
    const saveBtn = wrapper.findAll('button').find(b => b.text() === 'Save')!
    await saveBtn.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('update:card')).toBeTruthy()
    expect(wrapper.emitted('update:card')![0]).toEqual([updated])
  })
})
```

### Step 2: Run the test to verify it fails

Run: `cd src/web-ui && pnpm vitest run app/components/card/__tests__/CardDescription.test.ts`
Expected: FAIL — the "uses the version from the card prop" test fails because `CardDescription` currently reads its own `currentVersion` ref (frozen at `7` from mount), not `props.card.version` (`42` after the prop update). The "emits update:card" test fails because that event does not exist yet.

### Step 3: Refactor `CardDescription.vue`

Replace the `<script setup>` block of `src/web-ui/app/components/card/CardDescription.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import MarkdownEditor from '~/components/shared/MarkdownEditor.vue'

type CardResponse = components['schemas']['CardResponse']

// Maps numeric enum values to API string enum values
const CARD_TYPE_MAP: Record<number, string> = {
  0: 'Task',
  1: 'Bug',
  2: 'Epic',
  3: 'Spec',
  4: 'Idea'
}

const props = defineProps<{
  card: CardResponse
  projectId: string
  isArchived?: boolean
}>()

const emit = defineEmits<{
  'update:card': [card: CardResponse]
}>()

const description = ref(props.card.description ?? '')
const saving = ref(false)
const dirty = ref(false)
const saveError = ref<string | null>(null)

const api = useApi()
const board = useBoardStore()

let saveTimer: ReturnType<typeof setTimeout> | null = null

/** Normalize card.type to API string enum value — handles both number (legacy)
 *  and string (post-fix) values from the server. */
function toTypeString(type: CardResponse['type']): string {
  return typeof type === 'string' ? type : (CARD_TYPE_MAP[type as number] ?? 'Task')
}

function onDescriptionChange(value: string) {
  if (props.isArchived) return
  description.value = value
  dirty.value = true
  if (saveTimer) clearTimeout(saveTimer)
  saveTimer = setTimeout(saveDescription, 2000)
}

async function saveDescription() {
  if (!dirty.value || props.isArchived) return
  saving.value = true
  saveError.value = null
  try {
    const { data, error } = await api.PUT(ApiRoutes.Cards.update(props.projectId, props.card.id), {
      body: {
        title: props.card.title,
        description: description.value,
        type: toTypeString(props.card.type),
        version: props.card.version,
        parentCardId: props.card.parentCardId,
        dueAt: props.card.dueAt
      }
    })
    if (error) throw error
    if (data) emit('update:card', data as CardResponse)
    board.updateCard(props.card.id, { description: description.value })
    dirty.value = false
  } catch (e: unknown) {
    saveError.value = e instanceof Error ? e.message : 'Failed to save'
  } finally {
    saving.value = false
  }
}

function handleSaveClick() {
  if (saveTimer) clearTimeout(saveTimer)
  saveDescription()
}

function onKeydown(e: KeyboardEvent) {
  if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
    e.preventDefault()
    if (saveTimer) clearTimeout(saveTimer)
    saveDescription()
  }
}
</script>
```

This removes the `currentVersion` ref and its update line, and changes every `currentVersion.value` reference to `props.card.version`. The `CARD_TYPE_MAP`/`toTypeString` local helper stays for now — Task 3 replaces it with the shared `lib/card-type.ts` utility. The template is unchanged.

### Step 4: Wire `CardModal.vue` to consume `update:card`

In `src/web-ui/app/components/card/CardModal.vue`, add after `fetchCard`:

```ts
function applyCardUpdate(updated: CardResponse) {
  card.value = updated
}
```

Add `@update:card="applyCardUpdate"` to both `<CardDescription>` instances (desktop and mobile blocks):

```vue
<CardDescription
  :card="card"
  :project-id="projectId"
  :is-archived="isArchived"
  @update:card="applyCardUpdate"
/>
```

### Step 5: Run the test to verify it passes

Run: `cd src/web-ui && pnpm vitest run app/components/card/__tests__/CardDescription.test.ts`
Expected: PASS — all 4 tests green.

### Step 6: Full verification

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test
```
Expected: zero errors, all tests pass.

### Step 7: Commit

```bash
git add src/web-ui/app/components/card/CardModal.vue src/web-ui/app/components/card/CardDescription.vue \
  src/web-ui/app/components/card/__tests__/CardDescription.test.ts
git commit -m "refactor: lift card version ownership to CardModal, CardDescription emits update:card"
```

---

## Task 3: Shared `lib/card-type.ts` and `lib/date.ts` — Remove Triplicated Logic

**Files:**
- Create: `src/web-ui/app/lib/card-type.ts`
- Create: `src/web-ui/app/lib/date.ts`
- Create: `src/web-ui/app/lib/__tests__/card-type.test.ts`
- Create: `src/web-ui/app/lib/__tests__/date.test.ts`
- Modify: `src/web-ui/app/components/card/CardDescription.vue` (use `cardTypeToApiString` from the new file instead of its local copy)
- Modify: `src/web-ui/app/components/board/BoardCard.vue` (use `cardTypeOption`, `formatDueDate`, `isOverdue` instead of local copies)
- Modify: `src/web-ui/app/components/board/CardCreateModal.vue` (use `CARD_TYPE_OPTIONS` instead of its local `CARD_TYPE_MAP` and hardcoded `<option>` list)

**Why:** The numeric-CardType → API-string-enum map (`{0:'Task',1:'Bug',2:'Epic',3:'Spec',4:'Idea'}`) is hand-copied in `CardDescription.vue`, `CardCreateModal.vue`, and (as a label-only icon map) `BoardCard.vue`. The due-date formatting + overdue check is hand-copied in `BoardCard.vue` and would be copied a third time by Plan 4's Task 16. One miss-typed entry in any copy silently breaks type filtering or shows the wrong label. This task is the only place either piece of logic should live going forward.

**Interfaces:**
- Produces: `CARD_TYPE_OPTIONS: { value: number, apiValue: string, label: string, color: string, icon: string }[]`, `cardTypeToApiString(type: number | string): string`, `cardTypeOption(type: number | string): typeof CARD_TYPE_OPTIONS[number]` from `~/lib/card-type`. `formatDueDate(dueAt: string | null | undefined): string | null` and `isOverdue(dueAt: string | null | undefined): boolean` from `~/lib/date`.

### Step 1: Write the failing tests

Create `src/web-ui/app/lib/__tests__/card-type.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { CARD_TYPE_OPTIONS, cardTypeToApiString, cardTypeOption } from '~/lib/card-type'

describe('card-type', () => {
  it('has one option per CardType enum value', () => {
    expect(CARD_TYPE_OPTIONS.map(o => o.label)).toEqual(['Task', 'Bug', 'Epic', 'Spec', 'Idea'])
  })

  it('maps a numeric type to its API string value', () => {
    expect(cardTypeToApiString(1)).toBe('Bug')
  })

  it('passes through a string type unchanged', () => {
    expect(cardTypeToApiString('Epic')).toBe('Epic')
  })

  it('defaults unknown numeric types to Task', () => {
    expect(cardTypeToApiString(99)).toBe('Task')
  })

  it('resolves the full option for a numeric type', () => {
    expect(cardTypeOption(2).label).toBe('Epic')
  })
})
```

Create `src/web-ui/app/lib/__tests__/date.test.ts`:

```ts
import { describe, it, expect, vi, afterEach } from 'vitest'
import { formatDueDate, isOverdue } from '~/lib/date'

describe('date', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('returns null for a missing due date', () => {
    expect(formatDueDate(null)).toBeNull()
    expect(formatDueDate(undefined)).toBeNull()
  })

  it('formats an ISO date as short month + day', () => {
    expect(formatDueDate('2024-03-15T00:00:00Z')).toBe('Mar 15')
  })

  it('is not overdue when there is no due date', () => {
    expect(isOverdue(null)).toBe(false)
  })

  it('is overdue when the due date is in the past', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2024-06-01T00:00:00Z'))
    expect(isOverdue('2024-01-01T00:00:00Z')).toBe(true)
  })

  it('is not overdue when the due date is in the future', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2024-01-01T00:00:00Z'))
    expect(isOverdue('2024-06-01T00:00:00Z')).toBe(false)
  })
})
```

### Step 2: Run the tests to verify they fail

Run: `cd src/web-ui && pnpm vitest run app/lib/__tests__/card-type.test.ts app/lib/__tests__/date.test.ts`
Expected: FAIL with "Failed to resolve import `~/lib/card-type`" / `~/lib/date` — neither file exists yet.

### Step 3: Create `lib/card-type.ts`

```ts
export const CARD_TYPE_OPTIONS = [
  { value: 0, apiValue: 'Task', label: 'Task', color: 'neutral', icon: 'i-lucide-square-check' },
  { value: 1, apiValue: 'Bug', label: 'Bug', color: 'error', icon: 'i-lucide-bug' },
  { value: 2, apiValue: 'Epic', label: 'Epic', color: 'primary', icon: 'i-lucide-layers' },
  { value: 3, apiValue: 'Spec', label: 'Spec', color: 'info', icon: 'i-lucide-file-text' },
  { value: 4, apiValue: 'Idea', label: 'Idea', color: 'warning', icon: 'i-lucide-lightbulb' }
] as const

export function cardTypeToApiString(type: number | string): string {
  if (typeof type === 'string') return type
  return CARD_TYPE_OPTIONS.find(o => o.value === type)?.apiValue ?? 'Task'
}

export function cardTypeOption(type: number | string) {
  const apiValue = cardTypeToApiString(type)
  return CARD_TYPE_OPTIONS.find(o => o.apiValue === apiValue) ?? CARD_TYPE_OPTIONS[0]
}
```

### Step 4: Create `lib/date.ts`

```ts
export function formatDueDate(dueAt: string | null | undefined): string | null {
  if (!dueAt) return null
  return new Date(dueAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

export function isOverdue(dueAt: string | null | undefined): boolean {
  if (!dueAt) return false
  return new Date(dueAt) < new Date()
}
```

### Step 5: Run the tests to verify they pass

Run: `cd src/web-ui && pnpm vitest run app/lib/__tests__/card-type.test.ts app/lib/__tests__/date.test.ts`
Expected: PASS — all 10 tests green.

### Step 6: Remove the duplicated map from `CardDescription.vue`

In `src/web-ui/app/components/card/CardDescription.vue`, add the import:

```ts
import { cardTypeToApiString } from '~/lib/card-type'
```

Delete the local `CARD_TYPE_MAP` constant and `toTypeString` function (added in Task 2 Step 3):

```ts
// Maps numeric enum values to API string enum values
const CARD_TYPE_MAP: Record<number, string> = {
  0: 'Task',
  1: 'Bug',
  2: 'Epic',
  3: 'Spec',
  4: 'Idea'
}

/** Normalize card.type to API string enum value — handles both number (legacy)
 *  and string (post-fix) values from the server. */
function toTypeString(type: CardResponse['type']): string {
  return typeof type === 'string' ? type : (CARD_TYPE_MAP[type as number] ?? 'Task')
}
```

Replace its one call site (`type: toTypeString(props.card.type)`) with `type: cardTypeToApiString(props.card.type)`.

### Step 7: Refactor `BoardCard.vue` to use the shared utilities

In `src/web-ui/app/components/board/BoardCard.vue`, add the import:

```ts
import { cardTypeOption } from '~/lib/card-type'
import { formatDueDate, isOverdue } from '~/lib/date'
```

Remove the local `cardTypeIcons` map and `typeIcon`/`formattedDue`/`isOverdue` computeds, replacing them with:

```ts
const typeOption = computed(() => cardTypeOption(props.card.type))
const formattedDue = computed(() => formatDueDate(props.card.dueAt))
const cardIsOverdue = computed(() => isOverdue(props.card.dueAt))
```

Update the template's two usages: `:name="typeIcon"` → `:name="typeOption.icon"`, and `:class="isOverdue ? ... : ...'"` → `:class="cardIsOverdue ? 'text-red-500 font-medium' : 'text-gray-400'"`. (`isOverdue` was renamed to `cardIsOverdue` locally to avoid shadowing the imported function name.)

### Step 8: Refactor `CardCreateModal.vue` to use the shared options

In `src/web-ui/app/components/board/CardCreateModal.vue`, replace the import and local map:

```ts
import { CARD_TYPE_OPTIONS } from '~/lib/card-type'
```

Delete the local `CARD_TYPE_MAP` constant. Replace `body.type = CARD_TYPE_MAP[cardType.value] ?? 'Task'` with `body.type = CARD_TYPE_OPTIONS.find(o => o.value === cardType.value)?.apiValue ?? 'Task'` (this line now lives inside the try block from Task 1 Step 11 — keep that structure, only change the right-hand side of the `type` field). Replace the hardcoded `<option>` list in the Type `<select>` with:

```vue
<option v-for="opt in CARD_TYPE_OPTIONS" :key="opt.value" :value="opt.value">
  {{ opt.label }}
</option>
```

### Step 9: Full verification

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test
```
Expected: zero errors, all tests pass (including the existing `BoardCard.test.ts` and `CardCreateModal.test.ts` suites — neither test file asserts on the removed internal names, only on rendered output, so they should pass unchanged).

### Step 10: Commit

```bash
git add src/web-ui/app/lib/card-type.ts src/web-ui/app/lib/date.ts src/web-ui/app/lib/__tests__/ \
  src/web-ui/app/components/card/CardDescription.vue src/web-ui/app/components/board/BoardCard.vue \
  src/web-ui/app/components/board/CardCreateModal.vue
git commit -m "refactor: extract shared card-type and due-date utilities, remove triplicated maps"
```

---

## Task 4: Accessibility — Save Status and Error Announcements

**Files:**
- Modify: `src/web-ui/app/components/card/CardDescription.vue`
- Modify: `src/web-ui/app/components/card/__tests__/CardDescription.test.ts`

**Interfaces:**
- Consumes: none new.
- Produces: none new — purely template/markup additions for assistive technology.

**Why:** A screen-reader user gets no feedback that a save started, succeeded, or failed beyond the visual "Saving..." label swap and an error `<p>` with no `role`. This task makes both states announced.

### Step 1: Write the failing test

Add to `src/web-ui/app/components/card/__tests__/CardDescription.test.ts`:

```ts
it('marks the save error as an alert for assistive technology', async () => {
  mockPUT.mockResolvedValue({ data: undefined, error: { message: 'boom' } })
  const wrapper = await mountSuspended(CardDescription, {
    props: { card: makeCard(), projectId: 'p1' }
  })
  await wrapper.findComponent(MarkdownEditor).vm.$emit('update:modelValue', 'edited')
  const saveBtn = wrapper.findAll('button').find(b => b.text() === 'Save')!
  await saveBtn.trigger('click')
  await flushPromises()

  const error = wrapper.find('[role="alert"]')
  expect(error.exists()).toBe(true)
  expect(error.text()).toContain('Failed to save')
})

it('announces saving state to screen readers via an aria-live region', async () => {
  let resolvePut: (value: unknown) => void = () => {}
  mockPUT.mockImplementation(() => new Promise(resolve => { resolvePut = resolve }))
  const wrapper = await mountSuspended(CardDescription, {
    props: { card: makeCard(), projectId: 'p1' }
  })
  await wrapper.findComponent(MarkdownEditor).vm.$emit('update:modelValue', 'edited')
  const saveBtn = wrapper.findAll('button').find(b => b.text() === 'Save')!
  await saveBtn.trigger('click')

  const liveRegion = wrapper.find('[aria-live="polite"]')
  expect(liveRegion.exists()).toBe(true)
  expect(liveRegion.text()).toContain('Saving')

  resolvePut({ data: makeCard({ version: 2 }), error: undefined })
  await flushPromises()
})
```

### Step 2: Run the tests to verify they fail

Run: `cd src/web-ui && pnpm vitest run app/components/card/__tests__/CardDescription.test.ts`
Expected: FAIL — neither `[role="alert"]` nor `[aria-live="polite"]` exist in the template yet.

### Step 3: Add the markup

In the `<template>` of `src/web-ui/app/components/card/CardDescription.vue`, replace the closing block:

```vue
    <p
      v-if="saveError"
      class="text-xs text-error mt-1"
      role="alert"
    >
      {{ saveError }}
    </p>
    <p
      v-if="saving"
      class="sr-only"
      aria-live="polite"
    >
      Saving description…
    </p>
  </div>
</template>
```

(Replaces the previous `<p v-if="saveError" class="text-xs text-error mt-1">{{ saveError }}</p>` — only `role="alert"` is added there — plus the new `aria-live` status paragraph.)

### Step 4: Run the tests to verify they pass

Run: `cd src/web-ui && pnpm vitest run app/components/card/__tests__/CardDescription.test.ts`
Expected: PASS.

### Step 5: Full verification

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test
```
Expected: zero errors, all tests pass.

### Step 6: Commit

```bash
git add src/web-ui/app/components/card/CardDescription.vue src/web-ui/app/components/card/__tests__/CardDescription.test.ts
git commit -m "feat: announce description save status and errors to assistive technology"
```

---

## Verification (Plan Complete)

1. `cd src/web-ui && pnpm typecheck` — zero errors
2. `cd src/web-ui && pnpm lint` — zero errors
3. `cd src/web-ui && pnpm build` — successful production build
4. `cd src/web-ui && pnpm test` — all tests pass
5. Manual: open a card, edit the description, then (after Plan 4's Task 16 lands) edit the type/due date in the same session without closing the modal — no spurious 409 from a stale cached version.
6. Manual: stop the API server, try to archive/restore a card from both the board's three-dot menu and the card modal header — error toast appears in both cases (previously: nothing happened, silently).
