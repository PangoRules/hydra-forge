import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'

const mockPOST = vi.fn()

// Route state — mutable so tests can change it
const routeState = { path: '/projects/abc/board', params: { id: 'abc' } }

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
    routeState.path = '/projects/abc/board'
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
    routeState.params = {}
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
      body: { title: 'New chat', projectId: 'abc' }
    })
  })

  it('startNewChat creates a session without projectId when not on board', async () => {
    routeState.path = '/projects'
    routeState.params = {}
    setActivePinia(createPinia())
    mockPOST.mockResolvedValue({ data: { id: 'new-session-no-project' }, error: undefined })
    const store = useChatDockStore()
    await store.startNewChat()
    expect(store.activeSessionId).toBe('new-session-no-project')
    expect(mockPOST).toHaveBeenCalledWith('/api/chat/sessions', {
      body: { title: 'New chat' }
    })
  })
})

import { useChatDockStore } from '~/stores/chatDock'
