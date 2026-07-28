import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import BoardCard from '~/components/board/BoardCard.vue'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import { ApiError } from '~/lib/api-error'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

const mockPOST = vi.fn()
const mockDELETE = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: vi.fn(),
  DELETE: mockDELETE
}))

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

mockNuxtImport('useAuthStore', () => () => ({
  user: { userId: 'me', username: 'me', isAdmin: false }
}))

const makeCard = (overrides: Partial<CardResponse> = {}): CardResponse => ({
  id: 'c1',
  projectId: 'p1',
  columnId: 'col1',
  cardNumber: 42,
  title: 'Test Card',
  description: 'Test description',
  type: 'Task',
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
  relationshipBadges: [],
  relationshipCount: 0,
  ...overrides,
})

describe('BoardCard', () => {
  beforeEach(() => {
    mockPOST.mockReset()
    mockDELETE.mockReset()
    mockToastAdd.mockReset()
  })

  it('watches via POST when not already watching, unwatches via DELETE when already watching', async () => {
    mockPOST.mockResolvedValue({ data: undefined, error: undefined })
    const notWatching = await mountSuspended(BoardCard, {
      props: { card: makeCard(), projectId: 'p1' },
      global: { stubs: { ConfirmDialog } }
    })
    await (notWatching.vm as any).toggleWatch()
    expect(mockPOST).toHaveBeenCalledWith(expect.stringContaining('/watch'))

    mockDELETE.mockResolvedValue({ data: undefined, error: undefined })
    const watching = await mountSuspended(BoardCard, {
      props: {
        card: makeCard({ watchers: [{ userId: 'me', username: 'me', addedAt: new Date().toISOString() }] }),
        projectId: 'p1'
      },
      global: { stubs: { ConfirmDialog } }
    })
    await (watching.vm as any).toggleWatch()
    expect(mockDELETE).toHaveBeenCalledWith(expect.stringContaining('/watch'))
  })

  it('shows an error toast when toggling watch fails', async () => {
    mockPOST.mockRejectedValue(new ApiError(500, 'UNKNOWN', 'Server Error', null, 'about:blank', 'corr-3'))
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard(), projectId: 'p1' },
      global: { stubs: { ConfirmDialog } }
    })

    await (wrapper.vm as any).toggleWatch()
    await flushPromises()

    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to watch card', color: 'error' }))
  })

  it('shows an error toast and does not call fetchBoard when archive fails', async () => {
    mockPOST.mockRejectedValue(new ApiError(409, 'CARD_CONCURRENCY_MISMATCH', 'Conflict', null, 'about:blank', 'corr-1'))
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard(), projectId: 'p1' },
      global: { stubs: { ConfirmDialog } }
    })

    ;(wrapper.vm as any).showArchiveConfirm = true
    await flushPromises()
    await (wrapper.vm as any).confirmArchive()
    await flushPromises()

    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to archive card', color: 'error' }))
  })

  it('shows an error toast when restore fails', async () => {
    mockPOST.mockRejectedValue(new ApiError(500, 'UNKNOWN', 'Server Error', null, 'about:blank', 'corr-2'))
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ archivedAt: new Date().toISOString() }), projectId: 'p1' },
      global: { stubs: { ConfirmDialog } }
    })

    ;(wrapper.vm as any).showMenu = true
    await flushPromises()
    await (wrapper.vm as any).handleRestore()
    await flushPromises()

    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to restore card', color: 'error' }))
  })

  it('renders card title', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ cardNumber: 99 }), projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('#99')
  })

  it('shows description when present', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ description: 'Some description' }), projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Some description')
  })

  it('hides description when absent', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ description: '' }), projectId: 'p1' }
    })
    expect(wrapper.text()).not.toContain('description')
  })

  it('shows archived badge when archived', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ archivedAt: new Date().toISOString() }), projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('archived')
  })

  it('emits click with card', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard(), projectId: 'p1' }
    })
    await wrapper.find('.cursor-pointer').trigger('click')
    expect(wrapper.emitted('click')).toBeTruthy()
    expect(wrapper.emitted('click')?.[0]).toBeDefined()
  })

  it('renders type-specific icon', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ type: 'Issue' }), projectId: 'p1' }
    })
    expect(wrapper.find('.size-4').exists()).toBe(true)
  })

  it('shows assignee avatars when present', async () => {
    const wrapper = await mountSuspended(BoardCard, {
        props: {
          card: makeCard({
            assignees: [{ id: 'a1', userId: 'u1', username: 'alice', assignedAt: new Date().toISOString() }]
          }),
          projectId: 'p1'
        }
    })
    expect(wrapper.text()).toContain('A')
  })

  it('shows due date when present and not overdue', async () => {
    const futureDate = new Date()
    futureDate.setDate(futureDate.getDate() + 7)
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ dueAt: futureDate.toISOString() }), projectId: 'p1' }
    })
    expect(wrapper.find('.size-3').exists()).toBe(true)
  })

  it('shows a typed relationship badge with verb, card number and title, truncated', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: {
        card: makeCard({
          relationshipCount: 1,
          relationshipBadges: [{
            relatedCardId: 'r1',
            relatedCardNumber: 7,
            relatedCardTitle: 'A very long related card title indeed',
            type: 'BlockedBy',
            isSource: false
          }]
        }),
        projectId: 'p1'
      }
    })
    expect(wrapper.text()).toContain('blocked by #7 A very long related card title indeed')
    expect(wrapper.find('.truncate').exists()).toBe(true)
  })

  it('uses the source-side verb when this card is the relationship source', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: {
        card: makeCard({
          relationshipCount: 1,
          relationshipBadges: [{
            relatedCardId: 'r1',
            relatedCardNumber: 4,
            relatedCardTitle: 'Downstream card',
            type: 'BlockedBy',
            isSource: true
          }]
        }),
        projectId: 'p1'
      }
    })
    expect(wrapper.text()).toContain('blocks #4 Downstream card')
  })

  it('shows a +N indicator when relationshipCount exceeds the badge list', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: {
        card: makeCard({
          relationshipCount: 3,
          relationshipBadges: [{
            relatedCardId: 'r1',
            relatedCardNumber: 7,
            relatedCardTitle: 'Blocker',
            type: 'BlockedBy',
            isSource: false
          }]
        }),
        projectId: 'p1'
      }
    })
    expect(wrapper.text()).toContain('+2 more')
  })

  it('shows no relationship section and no lock icon when there are no relationships', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard(), projectId: 'p1' }
    })
    expect(wrapper.text()).not.toContain('Related')
    expect(wrapper.find('[class*="i-lucide-lock"]').exists()).toBe(false)
  })
})
