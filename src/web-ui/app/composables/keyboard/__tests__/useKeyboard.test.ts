import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useKeyboard } from '~/composables/keyboard/useKeyboard'

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

  it('skips a shortcut whose enabled() returns false', () => {
    const handler = vi.fn()
    kb.register('Board', 'a', handler, 'Archive', false, () => false)
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'a' }))
    expect(handler).not.toHaveBeenCalled()
  })

  it('calls a shortcut once its enabled() predicate becomes true', () => {
    let enabled = false
    const handler = vi.fn()
    kb.register('Board', 'a', handler, 'Archive', false, () => enabled)

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'a' }))
    expect(handler).not.toHaveBeenCalled()

    enabled = true
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'a' }))
    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('treats a shortcut with no enabled() as always enabled', () => {
    const handler = vi.fn()
    kb.register('Board', 'z', handler, 'Zoom')
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'z' }))
    expect(handler).toHaveBeenCalledTimes(1)
  })
})