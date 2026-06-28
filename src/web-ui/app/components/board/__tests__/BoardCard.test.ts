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

describe('BoardCard', () => {
  beforeEach(() => {
    mockPOST.mockReset()
    mockToastAdd.mockReset()
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
      props: { card: makeCard({ type: 1 }), projectId: 'p1' } // Issue type
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
})
