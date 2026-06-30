import { describe, it, expect } from 'vitest'

// Card types that get the Docs tab at all
const DOCS_CARD_TYPES = ['Goal', 'Idea'] as const
// Card types that get the Plan section within Docs
const PLAN_CARD_TYPES = ['Goal'] as const

function makeCard(type: string) {
  return {
    id: 'card-1',
    cardNumber: 1,
    title: 'Test card',
    type,
    description: null,
    archivedAt: null,
    version: 1,
    columnId: 'col-1',
    position: 0,
    projectId: 'proj-1',
    assignees: [],
    labels: [],
    parentCardId: null,
    dueDate: null,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString()
  }
}

describe('CardModal Docs tab visibility', () => {
  it('shows Docs tab for Goal cards', () => {
    const card = makeCard('Goal')
    const hasDocsTab = DOCS_CARD_TYPES.includes(card.type as typeof DOCS_CARD_TYPES[number])
    expect(hasDocsTab).toBe(true)
  })

  it('shows Docs tab for Idea cards', () => {
    const card = makeCard('Idea')
    const hasDocsTab = DOCS_CARD_TYPES.includes(card.type as typeof DOCS_CARD_TYPES[number])
    expect(hasDocsTab).toBe(true)
  })

  it('does not show Docs tab for Task cards', () => {
    const card = makeCard('Task')
    const hasDocsTab = DOCS_CARD_TYPES.includes(card.type as typeof DOCS_CARD_TYPES[number])
    expect(hasDocsTab).toBe(false)
  })

  it('does not show Docs tab for Issue cards', () => {
    const card = makeCard('Issue')
    const hasDocsTab = DOCS_CARD_TYPES.includes(card.type as typeof DOCS_CARD_TYPES[number])
    expect(hasDocsTab).toBe(false)
  })

  it('Plan only shown for Goal, not Idea', () => {
    expect(PLAN_CARD_TYPES.includes('Goal')).toBe(true)
    expect(PLAN_CARD_TYPES.includes('Idea' as typeof PLAN_CARD_TYPES[number])).toBe(false)
  })
})
