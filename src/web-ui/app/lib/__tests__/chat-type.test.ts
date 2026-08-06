import { describe, it, expect } from 'vitest'
import { getChatType, CHAT_TYPE_BADGE } from '~/lib/chat-type'

describe('getChatType', () => {
  it('returns "card" when both projectId and openCardId are set', () => {
    expect(getChatType({ projectId: 'p1', openCardId: 'c1' })).toBe('card')
  })

  it('returns "project" when only projectId is set', () => {
    expect(getChatType({ projectId: 'p1', openCardId: null })).toBe('project')
  })

  it('returns "normal" when neither is set', () => {
    expect(getChatType({ projectId: null, openCardId: null })).toBe('normal')
  })

  it('returns "normal" when only openCardId is set (impossible via API, but defensive)', () => {
    expect(getChatType({ projectId: null, openCardId: 'c1' })).toBe('normal')
  })
})

describe('CHAT_TYPE_BADGE', () => {
  it('has a label and color for every chat type', () => {
    expect(CHAT_TYPE_BADGE.normal).toEqual({ label: 'Chat', color: 'neutral' })
    expect(CHAT_TYPE_BADGE.project).toEqual({ label: 'Project', color: 'primary' })
    expect(CHAT_TYPE_BADGE.card).toEqual({ label: 'Card', color: 'success' })
  })
})
