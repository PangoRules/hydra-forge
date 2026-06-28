import { describe, it, expect } from 'vitest'
import { CARD_TYPE_OPTIONS, CARD_TYPE_FILTER_OPTIONS, cardTypeToApiString, cardTypeOption } from '~/lib/card-type'

describe('card-type', () => {
  it('has four options: Task, Issue, Goal, Idea', () => {
    expect(CARD_TYPE_OPTIONS.map(o => o.label)).toEqual(['Task', 'Issue', 'Goal', 'Idea'])
  })

  it('filter options include All first', () => {
    expect(CARD_TYPE_FILTER_OPTIONS[0].label).toBe('All')
    expect(CARD_TYPE_FILTER_OPTIONS[0].value).toBeNull()
  })

  it('filter options do not include Spec, Bug, or Epic', () => {
    const labels = CARD_TYPE_FILTER_OPTIONS.map(o => o.label)
    expect(labels).not.toContain('Spec')
    expect(labels).not.toContain('Bug')
    expect(labels).not.toContain('Epic')
  })

  it('index 1 maps to Issue', () => {
    expect(cardTypeToApiString(1)).toBe('Issue')
  })

  it('index 2 maps to Goal', () => {
    expect(cardTypeToApiString(2)).toBe('Goal')
  })

  it('passes through a known string type unchanged', () => {
    expect(cardTypeToApiString('Issue')).toBe('Issue')
    expect(cardTypeToApiString('Goal')).toBe('Goal')
  })

  it('defaults unknown numeric index to Task', () => {
    expect(cardTypeToApiString(99)).toBe('Task')
  })

  it('resolves Issue option at index 1', () => {
    expect(cardTypeOption(1).label).toBe('Issue')
    expect(cardTypeOption(1).color).toBe('error')
    expect(cardTypeOption(1).icon).toBe('i-lucide-bug')
  })

  it('resolves Goal option at index 2', () => {
    expect(cardTypeOption(2).label).toBe('Goal')
    expect(cardTypeOption(2).color).toBe('primary')
    expect(cardTypeOption(2).icon).toBe('i-lucide-layers')
  })

  it('resolves by API string', () => {
    expect(cardTypeOption('Goal').label).toBe('Goal')
    expect(cardTypeOption('Issue').label).toBe('Issue')
  })
})