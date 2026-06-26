import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardDescription from '~/components/card/CardDescription.vue'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

function makeCard(overrides: Partial<CardResponse> = {}): CardResponse {
  return {
    id: 'c1',
    projectId: 'p1',
    columnId: 'col1',
    cardNumber: 1,
    title: 'Test',
    description: 'Initial description',
    type: 0,
    position: 0,
    dueAt: null,
    version: 1,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
    movedAt: '2024-01-01T00:00:00Z',
    archivedAt: null,
    parentCardId: null,
    assignees: [],
    watchers: [],
    ...overrides
  }
}

describe('CardDescription', () => {
  const baseCard = makeCard()

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
