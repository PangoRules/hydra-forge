import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardMetadata from '~/components/card/CardMetadata.vue'

describe('CardMetadata', () => {
  const baseCard = {
    id: 'c1',
    title: 'Test',
    type: 'Task',
    columnId: 'col1',
    dueDate: null,
    assignees: [],
    parentEpicId: null
  } as any

  it('renders type badge', async () => {
    const wrapper = await mountSuspended(CardMetadata, {
      props: { card: baseCard }
    })
    expect(wrapper.text()).toContain('Type')
    expect(wrapper.text()).toContain('Task')
  })

  it('shows None for missing assignees', async () => {
    const wrapper = await mountSuspended(CardMetadata, {
      props: { card: baseCard }
    })
    expect(wrapper.text()).toContain('None')
  })

  it('shows None for missing due date', async () => {
    const wrapper = await mountSuspended(CardMetadata, {
      props: { card: baseCard }
    })
    expect(wrapper.text()).toContain('None')
  })

  it('renders assignee avatars when present', async () => {
    const wrapper = await mountSuspended(CardMetadata, {
      props: {
        card: {
          ...baseCard,
          assignees: [{ userId: 'u1', username: 'Alice' }]
        }
      }
    })
    expect(wrapper.text()).toContain('Assignees')
  })
})
