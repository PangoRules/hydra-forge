import { describe, it, expect, vi } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import AppTopbar from '~/components/layout/AppTopbar.vue'

const mockLogout = vi.fn()

mockNuxtImport('useAuth', () => () => ({
  logout: mockLogout,
  isAuthenticated: true
}))

mockNuxtImport('useAuthStore', () => () => ({
  user: { userId: 'u1', username: 'testadmin', isAdmin: true }
}))

describe('AppTopbar', () => {
  it('shows the username as the user-menu trigger label', async () => {
    const wrapper = await mountSuspended(AppTopbar, {
      global: { stubs: { NotificationPanel: true } }
    })
    expect(wrapper.text()).toContain('testadmin')
  })

  it('renders a brand link to the Chats home page', async () => {
    const wrapper = await mountSuspended(AppTopbar, {
      global: { stubs: { NotificationPanel: true } }
    })
    const brandLink = wrapper.find('a[href="/chats"]')
    expect(brandLink.exists()).toBe(true)
  })
})
