import { describe, it, expect, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import AppSidebar from '~/components/layout/AppSidebar.vue'

const mockUser = { userId: 'u1', username: 'admin', isAdmin: true }

mockNuxtImport('useAuthStore', () => () => ({ user: mockUser }))

describe('AppSidebar', () => {
  beforeEach(() => {
    mockUser.isAdmin = true
  })

  it('renders the pinned New Chat action', async () => {
    const wrapper = await mountSuspended(AppSidebar)
    expect(wrapper.text()).toContain('New Chat')
  })

  it('shows the Admin group for an admin user', async () => {
    const wrapper = await mountSuspended(AppSidebar)
    expect(wrapper.text()).toContain('System Settings')
  })

  it('hides the Admin group for a non-admin user', async () => {
    mockUser.isAdmin = false
    const wrapper = await mountSuspended(AppSidebar)
    expect(wrapper.text()).not.toContain('System Settings')
  })

  it('renders backlog items as non-navigable (no anchor tag)', async () => {
    const wrapper = await mountSuspended(AppSidebar)
    expect(wrapper.text()).toContain('Deep Research')
    const links = wrapper.findAll('a')
    expect(links.some(a => a.text().includes('Deep Research'))).toBe(false)
  })

  it('renders Projects as a real navigable link', async () => {
    const wrapper = await mountSuspended(AppSidebar)
    const links = wrapper.findAll('a')
    expect(links.some(a => a.attributes('href') === '/projects')).toBe(true)
  })
})
