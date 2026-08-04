import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import UsagePage from '~/pages/admin/usage.vue'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PATCH: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

mockNuxtImport('useAuthStore', () => () => ({
  user: { userId: 'u1', username: 'admin', isAdmin: true }
}))

const makeTokenRecord = (overrides = {}) => ({
  id: 't1',
  userId: 'u2',
  userName: 'testuser1',
  projectId: null,
  feature: 'PersonalChat',
  modelName: 'gpt-5',
  inputTokens: 100,
  outputTokens: 50,
  cachedTokens: 0,
  cost: 0.01,
  createdAt: new Date().toISOString(),
  ...overrides
})

describe('admin/usage.vue', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    mockGET.mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('loads token usage on mount and renders rows + total cost', async () => {
    mockGET.mockResolvedValue({
      data: { items: [makeTokenRecord()], totalCount: 1, totalCost: 0.01 },
      error: undefined
    })
    const wrapper = await mountSuspended(UsagePage)
    await flushPromises()

    expect(mockGET).toHaveBeenCalledWith(expect.stringContaining('/api/admin/usage/tokens'))
    expect(wrapper.text()).toContain('testuser1')
    expect(wrapper.text()).toContain('gpt-5')
    expect(wrapper.text()).toContain('Total Cost: $0.01')
  })

  it('switching to Image Usage tab queries the images endpoint', async () => {
    mockGET.mockResolvedValue({
      data: { items: [], totalCount: 0, totalCost: 0 },
      error: undefined
    })
    const wrapper = await mountSuspended(UsagePage)
    await flushPromises()
    mockGET.mockClear()

    const imageTabButton = wrapper.findAll('button').find(b => b.text() === 'Image Usage')
    await imageTabButton!.trigger('click')
    await flushPromises()

    expect(mockGET).toHaveBeenCalledWith(expect.stringContaining('/api/admin/usage/images'))
  })

  it('sends one query param per selected feature, not a joined string', async () => {
    mockGET.mockResolvedValue({
      data: { items: [], totalCount: 0, totalCost: 0 },
      error: undefined
    })
    const wrapper = await mountSuspended(UsagePage)
    await flushPromises()
    mockGET.mockClear()

    const vm = wrapper.vm as any
    vm.filterFeature = ['PersonalChat', 'ProjectChat']
    await flushPromises()
    vi.advanceTimersByTime(300)
    await flushPromises()

    const calledUrl = mockGET.mock.calls.at(-1)?.[0] as string
    const params = new URLSearchParams(calledUrl.split('?')[1])
    expect(params.getAll('feature')).toEqual(['PersonalChat', 'ProjectChat'])
  })
})
