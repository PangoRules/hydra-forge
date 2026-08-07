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
    mockGET.mockImplementation((url: string) => {
      if (url.includes('/presets')) {
        return Promise.resolve({ data: [], error: undefined })
      }
      return Promise.resolve({ data: { items: [], totalCount: 0 }, error: undefined })
    })
  })

  it('renders a Chats heading and an enabled New Chat button', async () => {
    const wrapper = await mountSuspended(ChatsPage)
    await flushPromises()
    expect(wrapper.find('h1').text()).toBe('Chats')
    const button = wrapper.find('button')
    expect(button.attributes('disabled')).toBeUndefined()
  })

  it('shows the empty state and a compose box when no session is selected', async () => {
    const wrapper = await mountSuspended(ChatsPage)
    await flushPromises()
    expect(wrapper.text()).toContain('No chats yet')
    expect(wrapper.text()).toContain('Type a message below to start a new chat.')
    expect(wrapper.find('textarea').exists()).toBe(true)
  })

  it('passes the ProjectChat feature to ChatSessionView for a project-scoped session', async () => {
    mockGET.mockImplementation((url: string) => {
      if (url.includes('/presets')) {
        return Promise.resolve({ data: [], error: undefined })
      }
      if (url.includes('/sessions')) {
        return Promise.resolve({
          data: {
            items: [
              {
                id: 's1',
                title: 'Proj chat',
                projectId: 'p1',
                openCardId: null,
                status: 'Active',
                summary: null,
                archivedAt: null
              }
            ],
            totalCount: 1
          },
          error: undefined
        })
      }
      return Promise.resolve({ data: { items: [], totalCount: 0 }, error: undefined })
    })

    const wrapper = await mountSuspended(ChatsPage, {
      global: {
        stubs: {
          ChatSessionView: {
            name: 'ChatSessionView',
            props: ['feature'],
            template: '<div :data-feature="feature" />'
          }
        }
      }
    })
    await flushPromises()

    const sessionButton = wrapper.findAll('button').find(b => b.text().includes('Proj chat'))
    expect(sessionButton).toBeTruthy()
    await sessionButton!.trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-feature="ProjectChat"]').exists()).toBe(true)
  })

  it('passes the PersonalChat feature to ChatSessionView for a normal session', async () => {
    mockGET.mockImplementation((url: string) => {
      if (url.includes('/presets')) {
        return Promise.resolve({ data: [], error: undefined })
      }
      if (url.includes('/sessions')) {
        return Promise.resolve({
          data: {
            items: [
              {
                id: 's2',
                title: 'Normal chat',
                projectId: null,
                openCardId: null,
                status: 'Active',
                summary: null,
                archivedAt: null
              }
            ],
            totalCount: 1
          },
          error: undefined
        })
      }
      return Promise.resolve({ data: { items: [], totalCount: 0 }, error: undefined })
    })

    const wrapper = await mountSuspended(ChatsPage, {
      global: {
        stubs: {
          ChatSessionView: {
            name: 'ChatSessionView',
            props: ['feature'],
            template: '<div :data-feature="feature" />'
          }
        }
      }
    })
    await flushPromises()

    const sessionButton = wrapper.findAll('button').find(b => b.text().includes('Normal chat'))
    expect(sessionButton).toBeTruthy()
    await sessionButton!.trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-feature="PersonalChat"]').exists()).toBe(true)
  })
})
