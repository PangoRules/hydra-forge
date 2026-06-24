import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardDescription from '~/components/card/CardDescription.vue'

describe('CardDescription', () => {
  const baseCard = {
    id: 'c1',
    title: 'Test',
    description: 'Initial description',
    type: 'Task'
  } as any

  it('renders description label', async () => {
    const wrapper = await mountSuspended(CardDescription, {
      props: { card: baseCard, projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Description')
  })

  it('renders component with card props', async () => {
    const wrapper = await mountSuspended(CardDescription, {
      props: { card: baseCard, projectId: 'p1' }
    })
    expect(wrapper.find('.flex').exists()).toBe(true)
  })
})
