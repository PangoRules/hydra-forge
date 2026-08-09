import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ChatSessionFilters from '~/components/chat/ChatSessionFilters.vue'
import type { ChatType } from '~/lib/chat-type'

describe('ChatSessionFilters', () => {
  it('renders Mine as active when scope is mine', async () => {
    const wrapper = await mountSuspended(ChatSessionFilters, {
      props: { status: 'Active', types: new Set<ChatType>(['normal', 'project', 'card']), scope: 'mine' }
    })
    const buttons = wrapper.findAll('button')
    const mineButton = buttons.find(b => b.text() === 'Mine')!
    expect(mineButton.classes()).toContain('bg-primary')
  })

  it('emits update:scope when Participated is clicked', async () => {
    const wrapper = await mountSuspended(ChatSessionFilters, {
      props: { status: 'Active', types: new Set<ChatType>(['normal', 'project', 'card']), scope: 'mine' }
    })
    const buttons = wrapper.findAll('button')
    const participatedButton = buttons.find(b => b.text() === 'Participated')!
    await participatedButton.trigger('click')
    expect(wrapper.emitted('update:scope')).toEqual([['participated']])
  })

  it('shows "All (3)" when every type is selected', async () => {
    const wrapper = await mountSuspended(ChatSessionFilters, {
      props: { status: 'Active', types: new Set<ChatType>(['normal', 'project', 'card']), scope: 'mine' }
    })
    expect(wrapper.text()).toContain('All (3)')
  })

  it('shows the single type label when only one type is selected', async () => {
    const wrapper = await mountSuspended(ChatSessionFilters, {
      props: { status: 'Active', types: new Set<ChatType>(['card']), scope: 'mine' }
    })
    expect(wrapper.text()).toContain('Card')
  })

  it('emits update:status when the status select changes', async () => {
    const wrapper = await mountSuspended(ChatSessionFilters, {
      props: { status: 'Active', types: new Set<ChatType>(['normal', 'project', 'card']), scope: 'mine' }
    })
    await wrapper.findComponent({ name: 'USelect' }).vm.$emit('update:model-value', 'Closed')
    expect(wrapper.emitted('update:status')).toEqual([['Closed']])
  })
})