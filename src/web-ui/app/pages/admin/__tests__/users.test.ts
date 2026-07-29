import { describe, it, expect, vi } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import UsersPage from '~/pages/admin/users.vue'

const mockGET = vi.fn()
const mockPATCH = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PATCH: mockPATCH,
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

mockNuxtImport('useAuthStore', () => () => ({
  user: { userId: 'u1', username: 'admin', isAdmin: true }
}))

const makeUser = (overrides = {}) => ({
  id: 'u2',
  username: 'testuser1',
  name: 'Test User',
  email: 'testuser1@localhost',
  isAdmin: false,
  isDisabled: false,
  lastLoginAt: null,
  createdAt: new Date().toISOString(),
  ...overrides
})

describe('admin/users.vue', () => {
  it('renders a mobile card per user with username, email, and action buttons', async () => {
    mockGET.mockResolvedValue({ data: { items: [makeUser()], totalCount: 1 }, error: undefined })
    const wrapper = await mountSuspended(UsersPage)
    await flushPromises()

    expect(wrapper.text()).toContain('testuser1')
    expect(wrapper.text()).toContain('testuser1@localhost')
    expect(wrapper.text()).toContain('Make Admin')
    expect(wrapper.text()).toContain('Reset Password')
  })

  it('calls the disable endpoint when the card Disable button is clicked', async () => {
    mockGET.mockResolvedValue({ data: { items: [makeUser()], totalCount: 1 }, error: undefined })
    mockPATCH.mockResolvedValue({ data: {}, error: undefined })
    const wrapper = await mountSuspended(UsersPage)
    await flushPromises()

    const disableButtons = wrapper.findAll('button').filter(b => b.text() === 'Disable')
    await disableButtons[0]!.trigger('click')
    await flushPromises()

    expect(mockPATCH).toHaveBeenCalled()
  })
})
