import { describe, it, expect, vi } from 'vitest'
import { ref } from 'vue'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import DefaultLayout from '~/layouts/default.vue'

mockNuxtImport('useAuth', () => () => ({
  logout: vi.fn(),
  isAuthenticated: true,
  checkAuth: vi.fn(),
  listenForAuthChanges: vi.fn()
}))
mockNuxtImport('useNotifications', () => () => ({
  fetchUnreadCount: vi.fn()
}))
mockNuxtImport('useNotificationHub', () => () => ({
  connect: vi.fn(),
  disconnect: vi.fn()
}))
mockNuxtImport('useSessionManager', () => () => ({
  isExpired: ref(false),
  isExpiringSoon: ref(false),
  isExtending: ref(false),
  timeRemaining: ref(0),
  remainingFormatted: ref('0:00'),
  extendSession: vi.fn(),
  start: vi.fn(),
  stop: vi.fn()
}))

describe('layouts/default.vue', () => {
  it('renders AppSidebar and AppTopbar around the page slot', async () => {
    const wrapper = await mountSuspended(DefaultLayout, {
      slots: { default: () => 'Page Content' },
      global: {
        stubs: {
          AppSidebar: true,
          AppTopbar: true,
          SessionExpiryModal: true
        }
      }
    })
    expect(wrapper.find('app-sidebar-stub').exists()).toBe(true)
    expect(wrapper.find('app-topbar-stub').exists()).toBe(true)
    expect(wrapper.text()).toContain('Page Content')
  })
})
