import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CardPopupBody from '~/components/card/CardPopupBody.vue'
import { ApiError } from '~/lib/api-error'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

const mockGET = vi.fn()
const mockPOST = vi.fn()
const mockDELETE = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: mockPOST,
  PUT: vi.fn(),
  DELETE: mockDELETE
}))

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

mockNuxtImport('useAuthStore', () => () => ({
  user: { userId: 'me', username: 'me', isAdmin: false }
}))

mockNuxtImport('useBoardStore', () => () => ({
  cardContentEvent: null
}))

mockNuxtImport('usePresenceStore', () => () => ({
  onlineUsers: new Map(),
  focusedCards: new Map()
}))

mockNuxtImport('useChatDockStore', () => () => ({
  loadSession: vi.fn(),
  openDock: vi.fn()
}))

const globalStubs = {
  CardDescription: true,
  CardMetadata: true,
  CardChecklist: true,
  CardComments: true,
  CardAttachments: true,
  CardDependencies: true,
  CardSpec: true,
  CardChatLinkList: true,
  CardPlan: {
    name: 'CardPlan',
    template: '<card-plan-stub />',
    methods: { loadAndExpandFirst() {} }
  }
}

function makeCard(overrides: Partial<CardResponse> = {}): CardResponse {
  return {
    id: 'c1',
    projectId: 'p1',
    columnId: 'col1',
    cardNumber: 1,
    title: 'Test card',
    description: '',
    type: 'Task',
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
    relationshipBadges: [],
    relationshipCount: 0,
    parentCard: null,
    childCount: 0,
    ...overrides
  }
}

async function mountBody(props: { cardId?: string, projectId?: string, readonly?: boolean } = {}) {
  const wrapper = await mountSuspended(CardPopupBody, {
    props: { cardId: 'c1', projectId: 'p1', ...props },
    global: { stubs: globalStubs }
  })
  await flushPromises()
  return wrapper
}

describe('CardPopupBody', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockPOST.mockReset()
    mockDELETE.mockReset()
    mockToastAdd.mockReset()
    mockGET.mockResolvedValue({ data: makeCard(), error: undefined })
  })

  it('mounts without error', async () => {
    const wrapper = await mountBody()
    expect(wrapper.vm).toBeTruthy()
  })

  it('surfaces the fetch error when the card fails to load', async () => {
    mockGET.mockRejectedValue(new ApiError(404, 'CARD_NOT_FOUND', 'Not Found', 'Card does not exist', 'about:blank', 'corr-1'))
    const wrapper = await mountBody({ cardId: 'missing' })
    expect((wrapper.vm as any).error).toBeTruthy()
  })

  it('watches the card via POST when not already watching', async () => {
    const wrapper = await mountBody()
    mockPOST.mockResolvedValue({
      data: makeCard({ watchers: [{ userId: 'me', username: 'me', addedAt: '2024-01-01T00:00:00Z' }] }),
      error: undefined
    })

    expect((wrapper.vm as any).isWatching).toBe(false)
    await (wrapper.vm as any).toggleWatch()
    await flushPromises()

    expect(mockPOST).toHaveBeenCalledWith(expect.stringContaining('/watch'))
    expect((wrapper.vm as any).isWatching).toBe(true)
  })

  it('unwatches the card via DELETE when already watching', async () => {
    mockGET.mockResolvedValue({
      data: makeCard({ watchers: [{ userId: 'me', username: 'me', addedAt: '2024-01-01T00:00:00Z' }] }),
      error: undefined
    })
    const wrapper = await mountBody()
    mockDELETE.mockResolvedValue({ data: makeCard({ watchers: [] }), error: undefined })

    expect((wrapper.vm as any).isWatching).toBe(true)
    await (wrapper.vm as any).toggleWatch()
    await flushPromises()

    expect(mockDELETE).toHaveBeenCalledWith(expect.stringContaining('/watch'))
    expect((wrapper.vm as any).isWatching).toBe(false)
  })

  it('shows an error toast when toggling watch fails', async () => {
    const wrapper = await mountBody()
    mockPOST.mockRejectedValue(new ApiError(500, 'UNKNOWN', 'Server error', null, 'about:blank', 'corr-3'))

    await (wrapper.vm as any).toggleWatch()
    await flushPromises()

    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to watch card', color: 'error' }))
  })

  it('archives the card and emits archived on confirm', async () => {
    mockPOST.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountBody()

    ;(wrapper.vm as any).showArchiveConfirm = true
    await flushPromises()
    await (wrapper.vm as any).confirmArchive()
    await flushPromises()

    expect(mockPOST).toHaveBeenCalledWith(
      expect.stringContaining('/archive'),
      expect.objectContaining({ body: { version: 1 } })
    )
    expect(wrapper.emitted('archived')).toBeTruthy()
  })

  it('shows an error toast and does not emit archived when the archive call fails', async () => {
    mockPOST.mockRejectedValue(new ApiError(409, 'CARD_CONCURRENCY_MISMATCH', 'Conflict', 'stale version', 'about:blank', 'corr-2'))
    const wrapper = await mountBody()

    ;(wrapper.vm as any).showArchiveConfirm = true
    await flushPromises()
    await (wrapper.vm as any).confirmArchive()
    await flushPromises()

    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to archive card', color: 'error' }))
    expect(wrapper.emitted('archived')).toBeFalsy()
  })

  it('shows the archive warning when this card is the target of a relationship, not just the source', async () => {
    // "Blocked by" links reverse which side is Source — this card can be the
    // relationship's target (see CardDependencies.vue's `reverse` flag).
    // Archiving must warn regardless of which side this card is on.
    const wrapper = await mountBody()

    mockGET.mockResolvedValueOnce({
      data: {
        relationships: [
          {
            sourceCardId: 'other',
            targetCardId: 'c1',
            sourceCardNumber: 5,
            sourceCardTitle: 'Blocker card',
            targetCardNumber: 1,
            targetCardTitle: 'Test card',
            type: 'BlockedBy'
          }
        ]
      },
      error: undefined
    })

    await (wrapper.vm as any).handleArchive()
    await flushPromises()

    expect((wrapper.vm as any).showArchiveWarning).toBe(true)
    expect((wrapper.vm as any).archiveDependents).toEqual([
      expect.objectContaining({ id: 'other', title: 'Blocker card' })
    ])
  })

  it('shows an error toast when restore fails', async () => {
    mockPOST.mockRejectedValue(new ApiError(500, 'UNKNOWN', 'Server Error', null, 'about:blank', 'corr-3'))
    mockGET.mockResolvedValue({ data: makeCard({ archivedAt: '2024-02-01T00:00:00Z' }), error: undefined })
    const wrapper = await mountBody()

    await (wrapper.vm as any).handleRestore()
    await flushPromises()

    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to restore card', color: 'error' }))
    expect(wrapper.emitted('restored')).toBeFalsy()
  })

  // Regression coverage for the readonly prop being silently dropped when
  // CardModal.vue was replaced by CardPopup/CardPopupBody — an archived
  // project must still lock editing even though the card itself isn't archived.
  describe('readonly prop (project-archived gate)', () => {
    it('isReadonly is false by default for a non-archived card with no readonly prop', async () => {
      const wrapper = await mountBody({ readonly: false })
      expect((wrapper.vm as any).isReadonly).toBe(false)
    })

    it('isReadonly is true when readonly=true even though the card itself is not archived (CardPopup passing projectArchived down)', async () => {
      const wrapper = await mountBody({ readonly: true })
      expect((wrapper.vm as any).isArchived).toBe(false)
      expect((wrapper.vm as any).isReadonly).toBe(true)
    })

    it('isReadonly is true when the card is archived, regardless of the readonly prop', async () => {
      mockGET.mockResolvedValue({ data: makeCard({ archivedAt: '2024-02-01T00:00:00Z' }), error: undefined })
      const wrapper = await mountBody({ readonly: false })
      expect((wrapper.vm as any).isReadonly).toBe(true)
    })
  })
})
