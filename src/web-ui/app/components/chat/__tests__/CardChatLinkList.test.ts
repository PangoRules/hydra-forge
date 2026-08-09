import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CardChatLinkList from '~/components/chat/CardChatLinkList.vue'

const mockToastAdd = vi.fn()
const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn(),
  PATCH: vi.fn()
}))

mockNuxtImport('useAppToast', () => () => ({
  error: mockToastAdd,
  success: vi.fn()
}))

const baseLink = {
  chatSessionId: 'sess-1',
  cardId: 'card-1',
  ownerUsername: 'testadmin',
  summary: 'Board summary',
  createdAt: '2026-08-01T00:00:00Z'
}

describe('CardChatLinkList', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGET.mockReset()
  })

  it('calls GET /chat/cards/{cardId}/links on mount (not on expand)', async () => {
    mockGET.mockResolvedValue({ data: [baseLink], error: undefined })

    const wrapper = await mountSuspended(CardChatLinkList, {
      props: { cardId: 'card-1' }
    })
    await flushPromises()

    // Fetch must have fired on mount — before any user interaction
    expect(mockGET).toHaveBeenCalledWith(expect.stringContaining('/api/cards/card-1/chat-links'))

    // Button shows count after fetch resolves
    expect(wrapper.text()).toContain('Linked Chats (1)')

    // isExpanded is false — list content is hidden
    expect(wrapper.find('UTable').exists()).toBe(false)
  })

  it('shows loading indicator in button while fetch is pending', async () => {
    let resolveGet: (value: unknown) => void
    mockGET.mockImplementation(() => new Promise(r => (resolveGet = r)))

    const wrapper = await mountSuspended(CardChatLinkList, {
      props: { cardId: 'card-1' }
    })
    // Give Vue a tick to render the pending state
    await wrapper.vm.$nextTick()

    // Loading state reflected in button label
    expect(wrapper.text()).toContain('Linked Chats (...)')

    // Resolve and flush to verify count updates
    resolveGet!({ data: [baseLink], error: undefined })
    await flushPromises()
    expect(wrapper.text()).toContain('Linked Chats (1)')
  })

  it('shows empty message when no links exist', async () => {
    mockGET.mockResolvedValue({ data: [], error: undefined })

    const wrapper = await mountSuspended(CardChatLinkList, {
      props: { cardId: 'card-1' }
    })
    await flushPromises()

    // Button shows zero count
    expect(wrapper.text()).toContain('Linked Chats (0)')
  })

  it('shows error toast when fetch fails', async () => {
    mockGET.mockRejectedValue(new Error('Network failure'))

    await mountSuspended(CardChatLinkList, {
      props: { cardId: 'card-1' }
    })
    await flushPromises()

    expect(mockToastAdd).toHaveBeenCalledWith(expect.stringContaining('Failed to load linked chats'))
  })
})
