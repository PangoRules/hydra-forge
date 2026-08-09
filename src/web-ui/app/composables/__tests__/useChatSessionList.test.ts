import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { ChatSessionStatus } from '~/types/chat'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn(),
  PATCH: vi.fn()
}))

mockNuxtImport('useAppToast', () => () => ({
  success: vi.fn(),
  error: vi.fn(),
  remove: vi.fn(),
  clear: vi.fn()
}))

async function flushMicrotasks() {
  await Promise.resolve()
  await Promise.resolve()
}

describe('useChatSessionList', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('loads initial page on mount', async () => {
    mockGET.mockResolvedValue({
      data: { items: [{ id: '1', title: 'Test' }], totalCount: 1 },
      error: undefined
    })
    const { useChatSessionList } = await import('~/composables/useChatSessionList')
    const { sessions, loading } = useChatSessionList()
    expect(loading.value).toBe(true)
    await flushMicrotasks()
    expect(sessions.value.length).toBe(1)
    expect(loading.value).toBe(false)
  })

  it('loadMore appends next page', async () => {
    mockGET.mockResolvedValueOnce({
      data: {
        items: [
          { id: '1', title: 'A', updatedAt: '2026-08-05T12:00:00Z' },
          { id: '2', title: 'B', updatedAt: '2026-08-05T11:00:00Z' }
        ],
        totalCount: 3
      },
      error: undefined
    })
    const { useChatSessionList } = await import('~/composables/useChatSessionList')
    const { sessions, loadMore, hasMore } = useChatSessionList()
    await flushMicrotasks()
    expect(sessions.value.length).toBe(2)
    expect(hasMore.value).toBe(true)

    mockGET.mockResolvedValueOnce({
      data: {
        items: [{ id: '3', title: 'C', updatedAt: '2026-08-05T10:00:00Z' }],
        totalCount: 3
      },
      error: undefined
    })
    await loadMore()
    await flushMicrotasks()
    expect(sessions.value.length).toBe(3)
    expect(hasMore.value).toBe(false)
  })

  it('hasMore is true when more pages exist', async () => {
    mockGET.mockResolvedValue({
      data: {
        items: [{ id: '1', title: 'A' }],
        totalCount: 50
      },
      error: undefined
    })
    const { useChatSessionList } = await import('~/composables/useChatSessionList')
    const { hasMore } = useChatSessionList()
    await flushMicrotasks()
    expect(hasMore.value).toBe(true)
  })

  it('patchSession updates a loaded item in place without refetching', async () => {
    mockGET.mockResolvedValue({
      data: {
        items: [{ id: '1', title: 'Old title', status: 'Active' }],
        totalCount: 1
      },
      error: undefined
    })
    const { useChatSessionList } = await import('~/composables/useChatSessionList')
    const { sessions, patchSession } = useChatSessionList()
    await flushMicrotasks()
    expect(mockGET).toHaveBeenCalledTimes(1)

    patchSession('1', { title: 'New title', status: ChatSessionStatus.Closed })

    expect(sessions.value[0]?.title).toBe('New title')
    expect(sessions.value[0]?.status).toBe('Closed')
    expect(mockGET).toHaveBeenCalledTimes(1)
  })

  it('patchSession is a no-op for an id not in the loaded list', async () => {
    mockGET.mockResolvedValue({
      data: { items: [{ id: '1', title: 'A' }], totalCount: 1 },
      error: undefined
    })
    const { useChatSessionList } = await import('~/composables/useChatSessionList')
    const { sessions, patchSession } = useChatSessionList()
    await flushMicrotasks()

    patchSession('not-loaded', { title: 'Should not appear' })

    expect(sessions.value.length).toBe(1)
    expect(sessions.value[0]?.title).toBe('A')
  })

  it('defaults to the Active filter and sends it as a query param', async () => {
    mockGET.mockResolvedValue({
      data: { items: [], totalCount: 0 },
      error: undefined
    })
    const { useChatSessionList } = await import('~/composables/useChatSessionList')
    const { statusFilter } = useChatSessionList()
    await flushMicrotasks()
    expect(statusFilter.value).toBe('Active')
    const calledUrl = mockGET.mock.calls[0]?.[0] as string
    expect(calledUrl).toContain('status=Active')
  })

  it('defaults to mine scope and does not send a types param when all three are selected', async () => {
    mockGET.mockResolvedValue({
      data: { items: [], totalCount: 0 },
      error: undefined
    })
    const { useChatSessionList } = await import('~/composables/useChatSessionList')
    const { scope, types } = useChatSessionList()
    await flushMicrotasks()
    expect(scope.value).toBe('mine')
    expect(types.value.size).toBe(3)
    const calledUrl = mockGET.mock.calls[0]?.[0] as string
    expect(calledUrl).toContain('scope=mine')
    expect(calledUrl).not.toContain('types=')
  })

  it('sends types param when a subset of types is selected', async () => {
    mockGET.mockResolvedValue({
      data: { items: [], totalCount: 0 },
      error: undefined
    })
    const { useChatSessionList } = await import('~/composables/useChatSessionList')
    const { types, refresh } = useChatSessionList()
    await flushMicrotasks()
    types.value = new Set(['project', 'card'])
    await refresh()
    const calledUrl = mockGET.mock.calls.at(-1)?.[0] as string
    expect(calledUrl).toContain('types=project,card')
  })

  it('refetches when scope changes to participated', async () => {
    mockGET.mockResolvedValue({
      data: { items: [], totalCount: 0 },
      error: undefined
    })
    const { useChatSessionList } = await import('~/composables/useChatSessionList')
    const { scope, refresh } = useChatSessionList()
    await flushMicrotasks()
    scope.value = 'participated'
    await refresh()
    const calledUrl = mockGET.mock.calls.at(-1)?.[0] as string
    expect(calledUrl).toContain('scope=participated')
  })
})
