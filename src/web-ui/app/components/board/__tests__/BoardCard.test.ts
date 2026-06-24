import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import BoardCard from '~/components/board/BoardCard.vue'

const makeCard = (overrides = {}) => ({
  id: 'c1',
  projectId: 'p1',
  columnId: 'col1',
  cardNumber: 42,
  title: 'Test Card',
  description: 'Test description',
  type: 0,
  position: 0,
  dueAt: null,
  version: 1,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  movedAt: new Date().toISOString(),
  archivedAt: null,
  parentCardId: null,
  assignees: [],
  watchers: [],
  ...overrides,
})

describe('BoardCard', () => {
  it('renders card title', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ title: 'Fix login bug' }) }
    })
    expect(wrapper.text()).toContain('Fix login bug')
  })

  it('shows card number', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ cardNumber: 99 }) }
    })
    expect(wrapper.text()).toContain('#99')
  })

  it('shows description when present', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ description: 'Some description' }) }
    })
    expect(wrapper.text()).toContain('Some description')
  })

  it('hides description when absent', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ description: '' }) }
    })
    expect(wrapper.text()).not.toContain('description')
  })

  it('shows archived badge when archived', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ archivedAt: new Date().toISOString() }) }
    })
    expect(wrapper.text()).toContain('archived')
  })

  it('emits click with card', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard() }
    })
    await wrapper.find('.cursor-pointer').trigger('click')
    expect(wrapper.emitted('click')).toBeTruthy()
    expect(wrapper.emitted('click')?.[0]).toBeDefined()
  })

  it('renders type-specific icon', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ type: 1 }) } // Bug type
    })
    expect(wrapper.find('.size-4').exists()).toBe(true)
  })

  it('shows assignee avatars when present', async () => {
    const wrapper = await mountSuspended(BoardCard, {
      props: {
        card: makeCard({
          assignees: [{ id: 'a1', userId: 'u1', username: 'alice', assignedAt: new Date().toISOString() }]
        })
      }
    })
    expect(wrapper.text()).toContain('A')
  })

  it('shows due date when present and not overdue', async () => {
    const futureDate = new Date()
    futureDate.setDate(futureDate.getDate() + 7)
    const wrapper = await mountSuspended(BoardCard, {
      props: { card: makeCard({ dueAt: futureDate.toISOString() }) }
    })
    expect(wrapper.find('.size-3').exists()).toBe(true)
  })
})
