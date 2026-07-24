import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ProjectsPage from '~/pages/projects/index.vue'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

mockNuxtImport('useToast', () => () => ({ add: vi.fn() }))

describe('projects/index.vue', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: { items: [], totalCount: 0 }, error: undefined })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('fetches on mount with default pagination params', async () => {
    await mountSuspended(ProjectsPage, {
      global: {
        stubs: {
          ProjectCreateModal: true,
          ProjectEditModal: true,
          ProjectListTable: true,
          ProjectList: true,
          ProjectFilterBar: true
        }
      }
    })
    await flushPromises()

    expect(mockGET).toHaveBeenCalledTimes(1)
    const [url] = mockGET.mock.calls[0]!
    expect(url).toContain('skip=0')
    expect(url).toContain('take=10')
    expect(url).toContain('sortBy=CreatedAt')
  })

  it('debounces search input by 300ms before refetching', async () => {
    const wrapper = await mountSuspended(ProjectsPage, {
      global: {
        stubs: {
          ProjectCreateModal: true,
          ProjectEditModal: true,
          ProjectListTable: true,
          ProjectList: true,
          ProjectFilterBar: true
        }
      }
    })
    await flushPromises()
    mockGET.mockClear()

    const vm = wrapper.vm as any
    vm.search = 'orders'
    await flushPromises()
    expect(mockGET).not.toHaveBeenCalled()

    vi.advanceTimersByTime(300)
    await flushPromises()

    expect(mockGET).toHaveBeenCalledTimes(1)
    const [url] = mockGET.mock.calls[0]!
    expect(url).toContain('search=orders')
  })
})