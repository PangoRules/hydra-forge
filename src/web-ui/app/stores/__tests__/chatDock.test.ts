import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'

const mockPOST = vi.fn()

// Route state — mutable so tests can change it
const routeState = { path: '/projects/abc', params: { id: 'abc' } }

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: vi.fn(),
  DELETE: vi.fn(),
  PATCH: vi.fn()
}))

mockNuxtImport('useAppToast', () => () => ({ error: vi.fn(), success: vi.fn() }))

describe('chatDock store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    mockPOST.mockReset()
    // Reset route to board context
    routeState.path = '/projects/abc'
    routeState.params = { id: 'abc' }
  })

  mockNuxtImport('useRoute', () => () => routeState)

  it('starts closed with no active session', () => {
    const store = useChatDockStore()
    expect(store.isOpen).toBe(false)
    expect(store.activeSessionId).toBe(null)
  })

  it('toggleDock flips isOpen', () => {
    const store = useChatDockStore()
    expect(store.isOpen).toBe(false)
    store.toggleDock()
    expect(store.isOpen).toBe(true)
    store.toggleDock()
    expect(store.isOpen).toBe(false)
  })

  it('closeDock sets isOpen false', () => {
    const store = useChatDockStore()
    store.isOpen = true
    store.closeDock()
    expect(store.isOpen).toBe(false)
  })

  it('detects projectId from board route', () => {
    const store = useChatDockStore()
    expect(store.currentProjectId).toBe('abc')
  })

  it('returns null projectId when not on a board route', () => {
    routeState.path = '/projects'
    routeState.params = { id: undefined as unknown as string }
    setActivePinia(createPinia())
    const store = useChatDockStore()
    expect(store.currentProjectId).toBe(null)
  })

  it('startNewChat creates a session with projectId when on board', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'new-session' }, error: undefined })
    const store = useChatDockStore()
    await store.startNewChat()
    expect(store.activeSessionId).toBe('new-session')
    expect(mockPOST).toHaveBeenCalledWith('/api/chat/sessions', {
      body: { title: '', projectId: 'abc' }
    })
  })

  it('startNewChat creates a session without projectId when not on board', async () => {
    routeState.path = '/projects'
    routeState.params = { id: undefined as unknown as string }
    setActivePinia(createPinia())
    mockPOST.mockResolvedValue({ data: { id: 'new-session-no-project' }, error: undefined })
    const store = useChatDockStore()
    await store.startNewChat()
    expect(store.activeSessionId).toBe('new-session-no-project')
    expect(mockPOST).toHaveBeenCalledWith('/api/chat/sessions', {
      body: { title: '' }
    })
  })

  it('startNewChat with content sets pendingMessage for the auto-send flow', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'new-session' }, error: undefined })
    const store = useChatDockStore()
    await store.startNewChat('summarize this board', null, 'model-1', 'high')
    expect(store.activeSessionId).toBe('new-session')
    expect(store.pendingMessage).toEqual({
      content: 'summarize this board',
      presetId: null,
      modelId: 'model-1',
      reasoningEffort: 'high'
    })
  })

  it('startNewChat sends the chosen model/effort on the create request', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'new-session' }, error: undefined })
    const store = useChatDockStore()
    await store.startNewChat('hello', null, 'model-1', 'high')
    expect(mockPOST).toHaveBeenCalledWith('/api/chat/sessions', {
      body: { title: 'hello', projectId: 'abc', preferredModelConfigId: 'model-1', preferredEffort: 'high' }
    })
  })

  it('startNewChat derives temp title from the first line of content', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'new-session' }, error: undefined })
    const store = useChatDockStore()
    const longLine = 'A'.repeat(100) + '\nsecond line ignored'
    await store.startNewChat(longLine)
    expect(mockPOST).toHaveBeenCalledWith('/api/chat/sessions', {
      body: { title: 'A'.repeat(60) + '…', projectId: 'abc' }
    })
  })

  it('startNewChat without content leaves pendingMessage null', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'new-session' }, error: undefined })
    const store = useChatDockStore()
    await store.startNewChat()
    expect(store.pendingMessage).toBe(null)
  })

  it('newChat clears pendingMessage', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'new-session' }, error: undefined })
    const store = useChatDockStore()
    await store.startNewChat('hello')
    expect(store.pendingMessage).not.toBe(null)
    store.newChat()
    expect(store.pendingMessage).toBe(null)
  })

  it('loadSession clears any stale pendingMessage', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'new-session' }, error: undefined })
    const store = useChatDockStore()
    await store.startNewChat('hello')
    store.loadSession('other-session')
    expect(store.pendingMessage).toBe(null)
  })

  it('loadSession persists activeSessionId to localStorage for cross-page resume', async () => {
    localStorage.clear()
    mockPOST.mockResolvedValue({ data: { id: 'new-session' }, error: undefined })
    const store = useChatDockStore()
    await store.startNewChat('hello')
    await nextTick()
    // The watcher fires on activeSessionId change — no explicit closeDock needed.
    expect(localStorage.getItem('hydraforge:chat:activeSessionId')).toBe('new-session')
  })

  it('newChat clears activeSessionId from localStorage', async () => {
    localStorage.clear()
    mockPOST.mockResolvedValue({ data: { id: 'new-session' }, error: undefined })
    const store = useChatDockStore()
    await store.startNewChat('hello')
    await nextTick()
    expect(localStorage.getItem('hydraforge:chat:activeSessionId')).toBe('new-session')
    store.newChat()
    await nextTick()
    expect(localStorage.getItem('hydraforge:chat:activeSessionId')).toBe(null)
  })

  it('dock registers with popup z-index stack and returns correct z-index', async () => {
    const popupZ = usePopupZIndex()
    const { stack } = popupZ
    stack.value = [] // reset

    // Register dock
    popupZ.registerPopup('chat-dock', 'dock', vi.fn())
    expect(popupZ.zIndexFor('chat-dock')).toBe(50)

    // Register a card popup — dock stays at 50, card gets 51
    popupZ.registerPopup('card-1', 'card', vi.fn())
    expect(popupZ.zIndexFor('chat-dock')).toBe(50)
    expect(popupZ.zIndexFor('card-1')).toBe(51)
  })
})

import { useChatDockStore } from '~/stores/chatDock'
import { usePopupZIndex } from '~/composables/usePopupZIndex'
