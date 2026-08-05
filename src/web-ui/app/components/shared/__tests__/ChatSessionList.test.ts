import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'

// Stub IntersectionObserver — not available in jsdom. Must be a real class
// constructor so `new IntersectionObserver(...)` works and vitest doesn't
// warn about non-function/class mocks.
const mockObserve = vi.fn()
const mockDisconnect = vi.fn()
const observerInstances: unknown[] = []
class MockIntersectionObserver {
  observe = mockObserve
  disconnect = mockDisconnect
  unobserve = vi.fn()
  constructor(_cb: IntersectionObserverCallback, _opts?: IntersectionObserverInit) {
    observerInstances.push(this)
  }
}
vi.stubGlobal('IntersectionObserver', MockIntersectionObserver)

import ChatSessionList from '~/components/shared/ChatSessionList.vue'
import { AiEditMode, ChatSessionStatus } from '~/types/chat'
import type { ChatSessionDto } from '~/types/chat'

function makeSession(id: string, title: string): ChatSessionDto {
  return {
    id,
    title,
    folderId: null,
    projectId: null,
    openCardId: null,
    personalityId: null,
    personalityArchived: false,
    status: ChatSessionStatus.Active,
    aiEditMode: AiEditMode.PerMutation,
    searchAllMyDocs: false,
    summary: null,
    preferredModelConfigId: null,
    preferredEffort: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    archivedAt: null
  }
}

const itemSlotTemplate = `<template #item="{ session, active }">
  <div :data-testid="session.id" :data-active="active">{{ session.title }}</div>
</template>`

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
      slots: { item: itemSlotTemplate }
    })
    expect(wrapper.find('[data-testid="1"]').text()).toBe('Chat One')
    expect(wrapper.find('[data-testid="2"]').text()).toBe('Chat Two')
  })

  it('marks active session via scoped slot prop', async () => {
    const sessions = [makeSession('1', 'Chat One')]
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions, loading: false, hasMore: false, activeSessionId: '1' },
      slots: { item: itemSlotTemplate }
    })
    expect(wrapper.find('[data-testid="1"]').attributes('data-active')).toBe('true')
  })

  it('renders sentinel when hasMore is true', async () => {
    const sessions = [makeSession('1', 'Chat One')]
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions, loading: false, hasMore: true }
    })
    expect(wrapper.find('.h-4.shrink-0').exists()).toBe(true)
  })

  it('renders loading spinner when loading and sessions exist', async () => {
    const sessions = [makeSession('1', 'Chat One')]
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions, loading: true, hasMore: true }
    })
    expect(wrapper.findAll('.animate-spin').length).toBeGreaterThanOrEqual(1)
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
    observerInstances.length = 0
    await mountSuspended(ChatSessionList, {
      props: { sessions: [], loading: false, hasMore: true }
    })
    expect(observerInstances.length).toBe(1)
  })

  it('disconnects observer on unmount', async () => {
    const wrapper = await mountSuspended(ChatSessionList, {
      props: { sessions: [], loading: false, hasMore: true }
    })
    wrapper.unmount()
    expect(mockDisconnect).toHaveBeenCalled()
  })
})
