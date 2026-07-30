import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { h } from 'vue'
import ProjectCreateModal from '~/components/project/ProjectCreateModal.vue'

const mockGET = vi.fn()
const mockPOST = vi.fn()

const currentUser = { userId: 'creator-1', username: 'creator', isAdmin: false }

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: mockPOST,
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

mockNuxtImport('useAuthStore', () => () => ({
  user: currentUser,
  token: 'tok',
  isAuthenticated: true,
  setAuth: vi.fn(),
  clearAuth: vi.fn(),
  restoreToken: vi.fn()
}))

describe('ProjectCreateModal', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    mockGET.mockReset()
    mockPOST.mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('excludes the current user (project creator) from member search results', async () => {
    mockGET.mockResolvedValue({
      data: [
        currentUser,
        { id: 'other-1', username: 'someone-else' }
      ],
      error: undefined
    })

    const wrapper = await mountSuspended(ProjectCreateModal, {
      props: { open: true },
      global: {
        stubs: {
          AppModal: {
            render() {
              return h('div', { 'data-testid': 'app-modal' }, [
                this.$slots.body?.(),
                this.$slots.footer?.()
              ])
            }
          }
        }
      }
    })

    const vm = wrapper.vm as any
    vm.searchQuery = 'el'
    vm.onSearchInput()
    vi.advanceTimersByTime(300)
    await flushPromises()

    const ids = vm.searchResults.map((u: { id: string }) => u.id)
    expect(ids).not.toContain(currentUser.userId)
    expect(ids).toContain('other-1')
  })
})
