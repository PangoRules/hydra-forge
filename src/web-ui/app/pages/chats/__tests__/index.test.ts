import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ChatsPage from '~/pages/chats/index.vue'

describe('chats/index.vue', () => {
  it('renders a Chats heading and a disabled New Chat button', async () => {
    const wrapper = await mountSuspended(ChatsPage)
    expect(wrapper.find('h1').text()).toBe('Chats')
    const button = wrapper.find('button')
    expect(button.attributes('disabled')).toBeDefined()
  })
})
