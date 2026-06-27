import { describe, it, expect } from 'vitest'
import { CARD_TYPE_OPTIONS, CARD_TYPE_FILTER_OPTIONS, cardTypeToApiString, cardTypeOption } from '~/lib/card-type'

describe('card-type', () => {
  it('has one option per CardType enum value', () => {
    expect(CARD_TYPE_OPTIONS.map(o => o.label)).toEqual(['Task', 'Bug', 'Epic', 'Spec', 'Idea'])
  })

  it('filter options include All first', () => {
    expect(CARD_TYPE_FILTER_OPTIONS[0].label).toBe('All')
    expect(CARD_TYPE_FILTER_OPTIONS[0].value).toBeNull()
  })

  it('maps a numeric type to its API string value', () => {
    expect(cardTypeToApiString(1)).toBe('Bug')
  })

  it('passes through a string type unchanged', () => {
    expect(cardTypeToApiString('Epic')).toBe('Epic')
  })

  it('defaults unknown numeric types to Task', () => {
    expect(cardTypeToApiString(99)).toBe('Task')
  })

  it('resolves the full option for a numeric type', () => {
    expect(cardTypeOption(2).label).toBe('Epic')
  })

  it('includes color and icon for each type', () => {
    expect(cardTypeOption(1).color).toBe('error')
    expect(cardTypeOption(1).icon).toBe('i-lucide-bug')
  })
})