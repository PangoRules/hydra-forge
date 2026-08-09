import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ProjectChatsTab from '~/components/project/ProjectChatsTab.vue'
import type { ChatSessionDto } from '~/types/chat'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

mockNuxtImport('useChatDockStore', () => () => ({
  loadSession: vi.fn(),
  openDock: vi.fn()
}))

function makeSession(overrides: Partial<ChatSessionDto> = {}): ChatSessionDto {
  return {
    id: 'session-1',
    title: 'Chat',
    status: 'Active',
    projectId: 'p1',
    openCardId: null,
    folderId: null,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
    archivedAt: null,
    ...overrides
  } as ChatSessionDto
}

describe('ProjectChatsTab', () => {
  beforeEach(() => {
    mockGET.mockReset()
  })

  // Regression: the component used to classify project-vs-card by firing one
  // CardChatLink GET per unique openCardId (Promise.allSettled), which both
  // wasted N requests and misclassified a session as "project" until its
  // CardChatLink row existed (see ChatSessionService.LinkCardAsync). Filtering
  // must derive purely from openCardId on the DTO already in hand.
  it('classifies sessions as project vs card from openCardId alone, with a single fetch', async () => {
    const projectSession = makeSession({ id: 's-project', openCardId: null })
    const cardSession = makeSession({ id: 's-card', openCardId: 'card-1' })
    mockGET.mockResolvedValue({
      data: { items: [projectSession, cardSession], totalCount: 2 },
      error: undefined
    })

    const wrapper = await mountSuspended(ProjectChatsTab, {
      props: { projectId: 'p1' }
    })
    await flushPromises()

    // Exactly one request — the session list — no per-card CardChatLink lookups.
    expect(mockGET).toHaveBeenCalledTimes(1)

    ;(wrapper.vm as any).filter = 'card'
    await flushPromises()
    expect((wrapper.vm as any).filteredSessions.map((s: ChatSessionDto) => s.id)).toEqual(['s-card'])

    ;(wrapper.vm as any).filter = 'project'
    await flushPromises()
    expect((wrapper.vm as any).filteredSessions.map((s: ChatSessionDto) => s.id)).toEqual(['s-project'])
  })
})
