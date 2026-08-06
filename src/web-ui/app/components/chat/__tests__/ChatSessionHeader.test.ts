import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ChatSessionHeader from '~/components/chat/ChatSessionHeader.vue'
import { AiEditMode, ChatSessionStatus } from '~/types/chat'
import type { ChatSessionDetailDto } from '~/types/chat'

const mockGET = vi.fn()
const mockPATCH = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  PATCH: mockPATCH
}))

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

function makeSession(overrides: Partial<ChatSessionDetailDto> = {}): ChatSessionDetailDto {
  return {
    id: 's1',
    title: 'Test Chat',
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
    createdAt: '2026-08-01T00:00:00Z',
    updatedAt: '2026-08-01T00:00:00Z',
    archivedAt: null,
    ownerId: 'u1',
    isShared: false,
    closedAt: null,
    messages: [],
    ...overrides
  }
}

describe('ChatSessionHeader — ownership visibility', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockToastAdd.mockReset()
    mockGET.mockResolvedValue({
      data: [
        { id: 'p1', name: 'Helper', description: null, systemPrompt: '', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null }
      ],
      error: undefined
    })
  })

  it('owner sees rename pencil, export, and close buttons', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Rename chat"]').exists()).toBe(true)
    expect(wrapper.find('[title="Export chat"]').exists()).toBe(true)
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(true)
  })

  it('non-owner does not see rename pencil, export, or close buttons', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ ownerId: 'u2' }), isOwner: false }
    })
    await flushPromises()
    expect(wrapper.find('[title="Rename chat"]').exists()).toBe(false)
    expect(wrapper.find('[title="Export chat"]').exists()).toBe(false)
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(false)
  })

  it('shows fork button on shared project chat the caller does not own', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: {
        session: makeSession({ isShared: true, projectId: 'proj1', ownerId: 'u2' }),
        isOwner: false
      }
    })
    await flushPromises()
    expect(wrapper.find('[title="Summarize → start my own"]').exists()).toBe(true)
  })

  it('hides fork button on own shared project chat', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: {
        session: makeSession({ isShared: true, projectId: 'proj1', ownerId: 'u1' }),
        isOwner: true
      }
    })
    await flushPromises()
    expect(wrapper.find('[title="Summarize → start my own"]').exists()).toBe(false)
  })
})

describe('ChatSessionHeader — disabled-when-closed state', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({
      data: [
        { id: 'p1', name: 'Helper', description: null, systemPrompt: '', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null }
      ],
      error: undefined
    })
  })

  it('rename pencil is disabled when session is Closed', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ status: ChatSessionStatus.Closed }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Rename chat"]').attributes('disabled')).toBeDefined()
  })

  it('close button is absent when session is Closed', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ status: ChatSessionStatus.Closed }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Close chat"]').exists()).toBe(false)
  })

  it('scope toggle is absent when session is Closed', async () => {
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ status: ChatSessionStatus.Closed }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.text()).not.toContain('All docs')
  })
})

describe('ChatSessionHeader — personality fetch error path', () => {
  it('shows error toast when personalities GET fails', async () => {
    mockGET.mockReset()
    mockToastAdd.mockReset()
    mockGET.mockRejectedValue(new Error('Server error'))
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession(), isOwner: true }
    })
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error', title: 'Server error' })
    )
  })

  it('renders session title even when personalities fetch fails', async () => {
    mockGET.mockReset()
    mockGET.mockRejectedValue(new Error('Server error'))
    const wrapper = await mountSuspended(ChatSessionHeader, {
      props: { session: makeSession({ title: 'My Chat' }), isOwner: true }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('My Chat')
  })
})
