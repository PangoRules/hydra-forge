import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ChatsPage from '~/pages/chats/index.vue'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn()
}))

mockNuxtImport('useToast', () => () => ({ add: vi.fn() }))

describe('chats/index.vue', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: { items: [], totalCount: 0 }, error: undefined })
  })

  it('renders a Chats heading and an enabled New Chat button', async () => {
    const wrapper = await mountSuspended(ChatsPage)
    await flushPromises()
    expect(wrapper.find('h1').text()).toBe('Chats')
    const button = wrapper.find('button')
    expect(button.attributes('disabled')).toBeUndefined()
  })

  it('shows the empty state once the session list loads', async () => {
    const wrapper = await mountSuspended(ChatsPage)
    await flushPromises()
    expect(wrapper.text()).toContain('No chats yet')
  })
})
