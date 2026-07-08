import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useKeyboard } from '~/composables/useKeyboard'

describe('useKeyboard', () => {
  let kb: ReturnType<typeof useKeyboard>

  beforeEach(() => {
    kb = useKeyboard()
    kb.clear()
  })

  it('registers and retrieves shortcuts by scope', () => {
    const handler = vi.fn()

    kb.register('Board', 'j', handler, 'Next card')
    kb.register('Board', 'k', handler, 'Previous card')
    kb.register('Modal', 'Escape', handler, 'Close modal')

    const boardShortcuts = kb.getShortcuts('Board')
    expect(boardShortcuts.length).toBe(2)
    expect(boardShortcuts[0]!.description).toBe('Next card')
    expect(boardShortcuts[1]!.description).toBe('Previous card')
  })

  it('unregister removes all shortcuts for scope', () => {
    kb.register('Board', 'j', vi.fn(), 'Next card')
    kb.register('Modal', 'Escape', vi.fn(), 'Close')

    kb.unregister('Board')
    expect(kb.getShortcuts('Board').length).toBe(0)
    expect(kb.getShortcuts('Modal').length).toBe(1)
  })

  it('getAllShortcuts returns all registered shortcuts', () => {
    kb.register('Board', 'j', vi.fn(), 'Next')
    kb.register('Modal', 'Escape', vi.fn(), 'Close')

    expect(kb.getAllShortcuts().length).toBe(2)
  })
})