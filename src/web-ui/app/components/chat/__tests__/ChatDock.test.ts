import { describe, it, expect, beforeEach, vi } from 'vitest'
import { reactive, computed } from 'vue'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'

const mockToggleDock = vi.fn()
const mockCloseDock = vi.fn()
const mockStartNewChat = vi.fn()

// Mutable reactive state — Pinia store-like object that auto-unwrap in templates
const storeState = reactive({
  isOpen: true,
  mode: 'session' as 'draft' | 'session' | 'history',
  activeSessionId: 'abc',
  isCreating: false,
  currentProjectId: 'proj1',
  position: { x: 0, y: 0 },
  pendingMessage: null as { content: string, presetId: string | null, modelId: string | null, reasoningEffort: string | null } | null,
  toggleDock: mockToggleDock,
  closeDock: mockCloseDock,
  startNewChat: mockStartNewChat,
  openDock: vi.fn(),
  newChat: vi.fn(() => {
    storeState.mode = 'draft'
    storeState.activeSessionId = null
  }),
  showHistory: vi.fn(),
  hideHistory: vi.fn(),
  loadSession: vi.fn(),
  clearPendingMessage: vi.fn()
})

const routeState = reactive({ path: '/projects/proj1/board', params: { id: 'proj1' } })

const baseSession = {
  id: 'abc',
  title: 'Real Session Title',
  folderId: null,
  projectId: null,
  openCardId: null,
  personalityId: null,
  personalityArchived: false,
  status: 'Active',
  aiEditMode: 1,
  searchAllMyDocs: false,
  summary: null,
  createdAt: '2026-08-01T00:00:00Z',
  updatedAt: '2026-08-01T00:00:00Z',
  archivedAt: null,
  ownerId: 'u1',
  isShared: false,
  closedAt: null,
  messages: []
}

const mockGET = vi.fn()

mockNuxtImport('useChatDockStore', () => () => storeState)
mockNuxtImport('useRoute', () => () => routeState)
mockNuxtImport('useAppToast', () => () => ({ error: vi.fn(), success: vi.fn() }))
mockNuxtImport('useAuthStore', () => () => ({ user: { userId: 'u1' } }))
mockNuxtImport('useChatStream', () => () => ({
  connect: vi.fn().mockResolvedValue(undefined),
  disconnect: vi.fn().mockResolvedValue(undefined),
  join: vi.fn().mockResolvedValue(undefined),
  leave: vi.fn().mockResolvedValue(undefined),
  send: vi.fn(),
  resend: vi.fn(),
  cancel: vi.fn().mockResolvedValue(undefined),
  streamingMessage: { value: null },
  isStreaming: { value: false },
  isConnected: { value: false },
  isReconnecting: { value: false },
  onStreamStart: vi.fn(),
  onStreamDelta: vi.fn(),
  onStreamDone: vi.fn(),
  onStreamError: vi.fn(),
  onSessionUpdated: vi.fn(),
  onReconnected: vi.fn(),
  clearStreaming: vi.fn()
}))
mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn(),
  PATCH: vi.fn()
}))

import { default as ChatDock } from '~/components/chat/ChatDock.vue'

describe('ChatDock', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    // ChatDock's session mode mounts the real ChatSessionView -> ChatSessionHeader
    // -> ChatInput -> ChatDocAttach tree, each firing its own GET (session detail,
    // personalities, presets, attached docs) against this same shared mock —
    // route by URL so each gets a response shaped like what it actually expects.
    mockGET.mockImplementation((url: string) => {
      if (url.includes('/personalities')) return Promise.resolve({ data: [], error: undefined })
      if (url.includes('/presets')) return Promise.resolve({ data: [], error: undefined })
      if (url.includes('/documents')) return Promise.resolve({ data: [], error: undefined })
      if (url.includes('/chat/sessions/')) return Promise.resolve({ data: baseSession, error: undefined })
      return Promise.resolve({ data: [], error: undefined })
    })
    // Reset to defaults
    storeState.isOpen = true
    storeState.mode = 'session'
    storeState.activeSessionId = 'abc'
    storeState.isCreating = false
    storeState.currentProjectId = 'proj1'
    storeState.position = { x: 0, y: 0 }
    storeState.pendingMessage = null
    routeState.path = '/projects/proj1/board'
    routeState.params = { id: 'proj1' }
    // Re-configure newChat impl each run so state doesn't bleed between tests
    storeState.newChat.mockImplementation(() => {
      storeState.mode = 'draft'
      storeState.activeSessionId = null
    })
  })

  it('renders the FAB button when dock is closed', async () => {
    storeState.isOpen = false
    storeState.activeSessionId = undefined as unknown as string
    const wrapper = await mountSuspended(ChatDock)
    expect(wrapper.find('button[aria-label="Open chat"]').exists()).toBe(true)
  })

  it('does not render the FAB when dock is open', async () => {
    storeState.isOpen = true
    const wrapper = await mountSuspended(ChatDock)
    expect(wrapper.find('button[aria-label="Open chat"]').exists()).toBe(false)
  })

  it('shows the real session title (not a hardcoded placeholder) when on board route with session', async () => {
    storeState.mode = 'session'
    const wrapper = await mountSuspended(ChatDock)
    await flushPromises()
    expect(wrapper.html()).toContain('Real Session Title')
  })

  it('shows the real session title when not on a project board', async () => {
    storeState.currentProjectId = undefined as unknown as string
    const wrapper = await mountSuspended(ChatDock)
    await flushPromises()
    expect(wrapper.html()).toContain('Real Session Title')
  })

  it('renders ChatSessionView via session-id prop when session is active', async () => {
    storeState.mode = 'session'
    const wrapper = await mountSuspended(ChatDock)
    await flushPromises()
    expect(wrapper.html()).toContain('Real Session Title')
  })

  it('renders exactly one header row in session mode (no duplicate title bar)', async () => {
    storeState.mode = 'session'
    const wrapper = await mountSuspended(ChatDock)
    await flushPromises()
    expect(wrapper.findAll('[data-testid="dock-header-row"]').length).toBe(1)
  })

  it('is hidden on /chats route', async () => {
    routeState.path = '/chats'
    const wrapper = await mountSuspended(ChatDock)
    // ClientOnly renders <!--v-if--> when slot is hidden — no popup renders
    expect(wrapper.html()).not.toContain('fixed z-50')
  })

  it('draft mode: renders the same ChatInput composer used by an active session', async () => {
    storeState.mode = 'draft'
    storeState.activeSessionId = undefined as unknown as string
    const wrapper = await mountSuspended(ChatDock)
    expect(wrapper.find('textarea').exists()).toBe(true)
  })

  it('draft mode: typing a message and pressing Enter creates a session with that content', async () => {
    storeState.mode = 'draft'
    storeState.activeSessionId = undefined as unknown as string
    const wrapper = await mountSuspended(ChatDock)
    const textarea = wrapper.find('textarea')
    expect(textarea.exists()).toBe(true)
    await textarea.setValue('summarize this board')
    await textarea.trigger('keydown', { key: 'Enter' })
    expect(mockStartNewChat).toHaveBeenCalledWith('summarize this board', null, null, null, null)
  })

  it('draft mode: does not create a session on empty/whitespace-only Enter', async () => {
    storeState.mode = 'draft'
    storeState.activeSessionId = undefined as unknown as string
    const wrapper = await mountSuspended(ChatDock)
    const textarea = wrapper.find('textarea')
    await textarea.setValue('   ')
    await textarea.trigger('keydown', { key: 'Enter' })
    expect(mockStartNewChat).not.toHaveBeenCalled()
  })

  it('draft mode: shows history button so history is reachable without an active session', async () => {
    storeState.mode = 'draft'
    storeState.activeSessionId = undefined as unknown as string
    const wrapper = await mountSuspended(ChatDock)
    expect(wrapper.find('button[title="History"]').exists()).toBe(true)
  })

  it('history mode: hides the history button (already viewing history)', async () => {
    storeState.mode = 'history'
    const wrapper = await mountSuspended(ChatDock)
    expect(wrapper.find('button[title="History"]').exists()).toBe(false)
  })

  it('returns to draft mode when the active session is archived from inside the view', async () => {
    storeState.mode = 'session'
    storeState.activeSessionId = 'abc'
    const wrapper = await mountSuspended(ChatDock, {
      global: {
        stubs: {
          ChatSessionView: {
            name: 'ChatSessionView',
            template: '<div data-testid="stub-session-view" />',
            setup() {
              return {
                session: computed(() => ({
                  id: 'abc',
                  title: 'Test',
                  status: 'Active',
                  projectId: 'p1',
                  openCardId: null
                })),
                isOwner: true
              }
            }
          }
        }
      }
    })
    await flushPromises()

    await wrapper.findComponent({ name: 'ChatSessionView' }).vm.$emit('archiveSession', 'abc')
    await flushPromises()

    expect(storeState.activeSessionId).toBeNull()
    expect(storeState.mode).toBe('draft')
  })
})
