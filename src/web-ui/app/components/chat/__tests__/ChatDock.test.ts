import { describe, it, expect, beforeEach, vi } from 'vitest'
import { reactive } from 'vue'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'

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
  newChat: vi.fn(),
  showHistory: vi.fn(),
  hideHistory: vi.fn(),
  loadSession: vi.fn(),
  clearPendingMessage: vi.fn()
})

const routeState = reactive({ path: '/projects/proj1/board', params: { id: 'proj1' } })

mockNuxtImport('useChatDockStore', () => () => storeState)
mockNuxtImport('useRoute', () => () => routeState)
mockNuxtImport('useAppToast', () => () => ({ error: vi.fn(), success: vi.fn() }))
mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn(),
  PATCH: vi.fn()
}))

import { default as ChatDock } from '~/components/chat/ChatDock.vue'

describe('ChatDock', () => {
  beforeEach(() => {
    vi.clearAllMocks()
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

  it('shows popup with Chat header when on board route with session', async () => {
    storeState.mode = 'session'
    const wrapper = await mountSuspended(ChatDock)
    expect(wrapper.html()).toContain('>Chat<')
  })

  it('shows Chat header when not on a project board', async () => {
    storeState.currentProjectId = undefined as unknown as string
    const wrapper = await mountSuspended(ChatDock)
    expect(wrapper.html()).toContain('>Chat<')
  })

  it('renders ChatSessionView via session-id prop when session is active', async () => {
    storeState.mode = 'session'
    const wrapper = await mountSuspended(ChatDock)
    expect(wrapper.html()).toContain('>Chat<')
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
    expect(mockStartNewChat).toHaveBeenCalledWith('summarize this board', null, null, null)
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
})
