import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { nextTick } from 'vue'
import { usePopupZIndex } from '~/composables/usePopupZIndex'

const mockToastError = vi.fn()
const mockToastSuccess = vi.fn()

mockNuxtImport('useAppToast', () => () => ({
  error: mockToastError,
  success: mockToastSuccess
}))

import { useCardPopupStore } from '~/stores/cardPopup'

describe('cardPopup store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    mockToastError.mockClear()
    mockToastSuccess.mockClear()
    localStorage.clear()

    // Reset module-level z-index stack between tests
    const { stack } = usePopupZIndex()
    stack.value = []
  })

  it('starts with empty state', () => {
    const store = useCardPopupStore()
    expect(store.openCardIds.length).toBe(0)
    expect(store.activeCardId).toBe(null)
    expect(store.canOpen).toBe(true)
  })

  it('openCard adds to openCardIds', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    expect(store.openCardIds).toEqual(['card-1'])
    expect(store.activeCardId).toBe('card-1')
  })

  it('openCard same card is no-op', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    mockToastError.mockClear()
    store.openCard('card-1', 'proj-1')
    expect(store.openCardIds).toEqual(['card-1'])
    expect(store.activeCardId).toBe('card-1')
    expect(mockToastError).not.toHaveBeenCalled()
  })

  it('max-3 enforcement', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    store.openCard('card-2', 'proj-1')
    store.openCard('card-3', 'proj-1')
    expect(store.openCardIds).toEqual(['card-1', 'card-2', 'card-3'])
    store.openCard('card-4', 'proj-1')
    expect(mockToastError).toHaveBeenCalledWith('Close a card popup first (max 3 open)')
    expect(store.openCardIds).toEqual(['card-1', 'card-2', 'card-3'])
  })

  it('canOpen reflects capacity', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    store.openCard('card-2', 'proj-1')
    store.openCard('card-3', 'proj-1')
    expect(store.canOpen).toBe(false)
  })

  it('closeCard removes and re-assigns active', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    store.openCard('card-2', 'proj-1')
    store.openCard('card-3', 'proj-1')
    store.closeCard('card-3')
    expect(store.openCardIds).toEqual(['card-1', 'card-2'])
    expect(store.activeCardId).toBe('card-2')
  })

  it('closeCard of non-active leaves active unchanged', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    store.openCard('card-2', 'proj-1')
    store.closeCard('card-1')
    expect(store.openCardIds).toEqual(['card-2'])
    expect(store.activeCardId).toBe('card-2')
  })

  it('closeCard last card sets active to null', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    store.closeCard('card-1')
    expect(store.openCardIds).toEqual([])
    expect(store.activeCardId).toBe(null)
  })

  it('bringToFront reorders openCardIds', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    store.openCard('card-2', 'proj-1')
    store.openCard('card-3', 'proj-1')
    store.bringToFront('card-1')
    expect(store.openCardIds).toEqual(['card-2', 'card-3', 'card-1'])
  })

  it('positions cascade', () => {
    const store = useCardPopupStore()
    store.openCard('card-a', 'proj-1')
    store.openCard('card-b', 'proj-1')
    store.openCard('card-c', 'proj-1')
    expect(store.positions['card-a']).toEqual({ x: 48, y: 48 })
    expect(store.positions['card-b']).toEqual({ x: 72, y: 72 })
    expect(store.positions['card-c']).toEqual({ x: 96, y: 96 })
  })

  it('positions cleaned on close', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    expect(store.positions['card-1']).toBeDefined()
    store.closeCard('card-1')
    expect(store.positions['card-1']).toBeUndefined()
  })

  it('closeAll clears every open card and unregisters them from the z-index stack', () => {
    const store = useCardPopupStore()
    const { stack } = usePopupZIndex()
    store.openCard('card-1', 'proj-1')
    store.openCard('card-2', 'proj-1')
    store.openCard('card-3', 'proj-1')
    expect(stack.value.length).toBe(3)

    store.closeAll()

    expect(store.openCardIds).toEqual([])
    expect(store.positions).toEqual({})
    expect(store.activeCardId).toBe(null)
    expect(stack.value.length).toBe(0)
  })

  it('closeAll on an empty store is a no-op', () => {
    const store = useCardPopupStore()
    expect(() => store.closeAll()).not.toThrow()
    expect(store.openCardIds).toEqual([])
  })

  it('localStorage persistence', async () => {
    vi.useFakeTimers()
    const store1 = useCardPopupStore()
    store1.openCard('card-1', 'proj-1')
    store1.openCard('card-2', 'proj-1')
    // Persistence watcher is debounced (250ms) so a drag/resize doesn't hit
    // localStorage on every pixel — advance past it before simulating reload.
    await vi.advanceTimersByTimeAsync(250)
    vi.useRealTimers()

    // Simulate hard reload by creating a new store instance
    setActivePinia(createPinia())
    const store2 = useCardPopupStore()
    expect(store2.openCardIds).toEqual(['card-1', 'card-2'])
    expect(store2.positions['card-1']).toEqual({ x: 48, y: 48 })
    expect(store2.positions['card-2']).toEqual({ x: 72, y: 72 })
    expect(store2.getProjectId('card-1')).toEqual('proj-1')
    expect(store2.getProjectId('card-2')).toEqual('proj-1')
  })

  it('getSize defaults to a desktop-sized popup', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    expect(store.getSize('card-1')).toEqual({ width: 680, height: 640 })
  })

  it('resizeCard updates size for an open card', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    store.resizeCard('card-1', { width: 800, height: 700 })
    expect(store.getSize('card-1')).toEqual({ width: 800, height: 700 })
  })

  it('resizeCard is a no-op for a card that is not open', () => {
    const store = useCardPopupStore()
    store.resizeCard('never-opened', { width: 800, height: 700 })
    expect(store.sizes['never-opened']).toBeUndefined()
  })

  it('closeCard clears the size entry', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    store.resizeCard('card-1', { width: 800, height: 700 })
    store.closeCard('card-1')
    expect(store.sizes['card-1']).toBeUndefined()
  })

  it('size persists across reload alongside position', async () => {
    vi.useFakeTimers()
    const store1 = useCardPopupStore()
    store1.openCard('card-1', 'proj-1')
    store1.resizeCard('card-1', { width: 900, height: 750 })
    await vi.advanceTimersByTimeAsync(250)
    vi.useRealTimers()

    setActivePinia(createPinia())
    const store2 = useCardPopupStore()
    expect(store2.getSize('card-1')).toEqual({ width: 900, height: 750 })
  })

  it('bringToFront no-ops when the card is already topmost', () => {
    const store = useCardPopupStore()
    const { stack } = usePopupZIndex()
    store.openCard('card-1', 'proj-1')
    store.openCard('card-2', 'proj-1')
    const stackBefore = [...stack.value]
    store.bringToFront('card-2')
    expect(store.openCardIds).toEqual(['card-1', 'card-2'])
    expect(stack.value).toEqual(stackBefore)
  })

  it('setActive no-ops when the card is already active', () => {
    const store = useCardPopupStore()
    store.openCard('card-1', 'proj-1')
    expect(store.activeCardId).toBe('card-1')
    store.setActive('card-1')
    expect(store.activeCardId).toBe('card-1')
  })

  describe('project-archived readonly gate', () => {
    it('isProjectArchived is false for a card whose project was never marked archived', () => {
      const store = useCardPopupStore()
      store.openCard('card-1', 'proj-1')
      expect(store.isProjectArchived('card-1')).toBe(false)
    })

    it('isProjectArchived reflects setProjectArchived for the card\'s project', () => {
      const store = useCardPopupStore()
      store.openCard('card-1', 'proj-1')
      store.setProjectArchived('proj-1', true)
      expect(store.isProjectArchived('card-1')).toBe(true)
    })

    it('isProjectArchived only affects cards in the archived project, not other open cards', () => {
      const store = useCardPopupStore()
      store.openCard('card-1', 'proj-1')
      store.openCard('card-2', 'proj-2')
      store.setProjectArchived('proj-1', true)
      expect(store.isProjectArchived('card-1')).toBe(true)
      expect(store.isProjectArchived('card-2')).toBe(false)
    })

    it('isProjectArchived is false for a card that was never opened (no cardProjectIds entry)', () => {
      const store = useCardPopupStore()
      expect(store.isProjectArchived('never-opened')).toBe(false)
    })
  })
})
