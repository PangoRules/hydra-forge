import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'

const mockUseChatSessionList = vi.fn()

vi.mock('~/composables/useChatSessionList', () => ({
  useChatSessionList: (...args: unknown[]) => mockUseChatSessionList(...args)
}))

describe('ChatDockHistory', () => {
  beforeEach(() => {
    mockUseChatSessionList.mockReturnValue({
      sessions: ref([]),
      loading: ref(false),
      hasMore: ref(false),
      loadMore: vi.fn(),
      refresh: vi.fn()
    })
  })

  it('renders empty state', async () => {
    const wrapper = await mountSuspended(ChatDockHistory)
    expect(wrapper.text()).toContain('No conversations yet')
  })

  it('renders session list', async () => {
    mockUseChatSessionList.mockReturnValue({
      sessions: ref([{ id: '1', title: 'Test Chat', summary: 'Hello', updatedAt: '2026-01-01' }]),
      loading: ref(false),
      hasMore: ref(false),
      loadMore: vi.fn(),
      refresh: vi.fn()
    })
    const wrapper = await mountSuspended(ChatDockHistory)
    expect(wrapper.text()).toContain('Test Chat')
  })
})

import ChatDockHistory from '~/components/chat/ChatDockHistory.vue'
