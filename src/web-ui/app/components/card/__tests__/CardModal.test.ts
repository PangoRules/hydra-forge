import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { h } from 'vue'
import CardModal from '~/components/card/CardModal.vue'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
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

async function mountLoadedModal() {
  mockGET.mockResolvedValue({ data: makeCard(), error: undefined })
  const wrapper = await mountSuspended(CardModal, {
    props: { cardId: 'c1', projectId: 'p1' },
    global: {
      stubs: {
        AppModal: {
          render() {
            return h('div', { 'data-testid': 'app-modal' }, this.$slots.default?.())
          }
        },
        CardDescription: true,
        CardMetadata: true
      }
    }
  })
  await flushPromises()
  return wrapper
}

describe('CardModal', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockPOST.mockReset()
    mockDELETE.mockReset()
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
      global: {
        stubs: {
          AppModal: {
            render() {
              return h('div', { 'data-testid': 'app-modal' }, this.$slots.default?.())
            }
          },
          CardDescription: true,
          CardMetadata: true
        }
      }
    })
    await flushPromises()
    expect((wrapper.vm as any).error).toBeTruthy()
  })

  it('watches the card via POST when not already watching', async () => {
    const wrapper = await mountLoadedModal()
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
    const wrapper = await mountSuspended(CardModal, {
      props: { cardId: 'c1', projectId: 'p1' },
      global: {
        stubs: {
          AppModal: {
            render() {
              return h('div', { 'data-testid': 'app-modal' }, this.$slots.default?.())
            }
          },
          CardDescription: true,
          CardMetadata: true
        }
      }
    })
    await flushPromises()
    mockDELETE.mockResolvedValue({ data: makeCard({ watchers: [] }), error: undefined })

    expect((wrapper.vm as any).isWatching).toBe(true)
    await (wrapper.vm as any).toggleWatch()
    await flushPromises()

    expect(mockDELETE).toHaveBeenCalledWith(expect.stringContaining('/watch'))
    expect((wrapper.vm as any).isWatching).toBe(false)
  })

  it('shows an error toast when toggling watch fails', async () => {
    const wrapper = await mountLoadedModal()
    mockPOST.mockRejectedValue(new ApiError(500, 'UNKNOWN', 'Server error', null, 'about:blank', 'corr-3'))

    await (wrapper.vm as any).toggleWatch()
    await flushPromises()

    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to watch card', color: 'error' }))
  })

  it('archives the card and emits archived on confirm', async () => {
    mockPOST.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountLoadedModal()
    await flushPromises()

    // Open confirm dialog and confirm
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
    const wrapper = await mountLoadedModal()
    await flushPromises()

    ;(wrapper.vm as any).showArchiveConfirm = true
    await flushPromises()
    await (wrapper.vm as any).confirmArchive()
    await flushPromises()

    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to archive card', color: 'error' }))
    expect(wrapper.emitted('archived')).toBeFalsy()
  })

  it('shows an error toast when restore fails', async () => {
    mockPOST.mockRejectedValue(new ApiError(500, 'UNKNOWN', 'Server Error', null, 'about:blank', 'corr-3'))
    mockGET.mockResolvedValue({ data: makeCard({ archivedAt: '2024-02-01T00:00:00Z' }), error: undefined })
    const wrapper = await mountSuspended(CardModal, {
      props: { cardId: 'c1', projectId: 'p1' },
      global: {
        stubs: {
          AppModal: {
            render() {
              return h('div', { 'data-testid': 'app-modal' }, this.$slots.default?.())
            }
          },
          CardDescription: true,
          CardMetadata: true
        }
      }
    })
    await flushPromises()

    await (wrapper.vm as any).handleRestore()
    await flushPromises()

    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Failed to restore card', color: 'error' }))
    expect(wrapper.emitted('restored')).toBeFalsy()
  })
})
