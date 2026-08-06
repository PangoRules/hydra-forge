import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import { defineComponent } from 'vue'
import { AiEditMode, ChatSessionStatus } from '~/types/chat'
import type { ChatSessionDto } from '~/types/chat'

const mockUseChatSessionList = vi.fn()

vi.mock('~/composables/useChatSessionList', () => ({
  useChatSessionList: (...args: unknown[]) => mockUseChatSessionList(...args)
}))

// Stub the shared presentational component so we test ChatDockHistory's own
// chrome (Back button, History header, item slot content) without pulling in
// ChatSessionList's IntersectionObserver and sentinel logic. The stub renders
// the scoped #item slot so the session title/summary flow through.
vi.mock('~/components/shared/ChatSessionList.vue', () => ({
  default: defineComponent({
    props: ['sessions', 'loading', 'hasMore', 'activeSessionId'],
    emits: ['select', 'loadMore'],
    template: `<div>
      <div v-if="loading && sessions.length === 0">Loading…</div>
      <div v-else-if="!loading && sessions.length === 0">No conversations yet.</div>
      <template v-else>
        <div v-for="s in sessions" :key="s.id">
          <slot name="item" :session="s" :active="false" />
        </div>
        <div v-if="!hasMore && sessions.length > 0">All caught up</div>
      </template>
    </div>`
  })
}))

function makeSession(id: string, title: string, summary: string | null): ChatSessionDto {
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
    summary,
    preferredModelConfigId: null,
    preferredEffort: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    archivedAt: null
  }
}

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

  it('renders session list with title and summary', async () => {
    mockUseChatSessionList.mockReturnValue({
      sessions: ref([makeSession('1', 'Test Chat', 'Hello')]),
      loading: ref(false),
      hasMore: ref(false),
      loadMore: vi.fn(),
      refresh: vi.fn()
    })
    const wrapper = await mountSuspended(ChatDockHistory)
    expect(wrapper.text()).toContain('Test Chat')
    expect(wrapper.text()).toContain('Hello')
  })

  it('shows "New Chat" fallback for untitled sessions', async () => {
    mockUseChatSessionList.mockReturnValue({
      sessions: ref([makeSession('1', '', null)]),
      loading: ref(false),
      hasMore: ref(false),
      loadMore: vi.fn(),
      refresh: vi.fn()
    })
    const wrapper = await mountSuspended(ChatDockHistory)
    expect(wrapper.text()).toContain('New Chat')
    expect(wrapper.text()).toContain('No messages')
  })

  it('renders Back button and History header', async () => {
    const wrapper = await mountSuspended(ChatDockHistory)
    expect(wrapper.text()).toContain('Back')
    expect(wrapper.text()).toContain('History')
  })

  it('shows a type badge on each history item', async () => {
    mockUseChatSessionList.mockReturnValue({
      sessions: ref([{
        ...makeSession('s1', 'Proj chat', null),
        projectId: 'p1',
        openCardId: null
      }]),
      loading: ref(false),
      hasMore: ref(false),
      loadMore: vi.fn(),
      refresh: vi.fn()
    })
    const wrapper = await mountSuspended(ChatDockHistory)
    expect(wrapper.text()).toContain('Project')
  })
})

import ChatDockHistory from '~/components/chat/ChatDockHistory.vue'
