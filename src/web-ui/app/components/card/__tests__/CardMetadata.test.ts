import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CardMetadata from '~/components/card/CardMetadata.vue'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']
type CardAssigneeResponse = components['schemas']['CardAssigneeResponse']

function makeCard(overrides: Partial<CardResponse> = {}): CardResponse {
  return {
    id: 'c1',
    projectId: 'p1',
    columnId: 'col1',
    cardNumber: 1,
    title: 'Test',
    description: '',
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

describe('CardMetadata', () => {
  const baseCard = makeCard()

  it('renders type badge', async () => {
    const wrapper = await mountSuspended(CardMetadata, {
      props: { card: baseCard }
    })
    expect(wrapper.text()).toContain('Type')
    // CardType is a numeric enum; value 0 renders as '0'
    expect(wrapper.text()).toContain('0')
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
    const cardWithAssignee = makeCard({
      assignees: [{ id: 'a1', userId: 'u1', username: 'Alice', assignedAt: '2024-01-01T00:00:00Z' }]
    })
    const wrapper = await mountSuspended(CardMetadata, {
      props: { card: cardWithAssignee }
    })
    expect(wrapper.text()).toContain('Assignees')
  })
})
