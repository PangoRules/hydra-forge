import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
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
    store.openCard('card-1')
    expect(store.openCardIds).toEqual(['card-1'])
    expect(store.activeCardId).toBe('card-1')
  })

  it('openCard same card is no-op', () => {
    const store = useCardPopupStore()
    store.openCard('card-1')
    mockToastError.mockClear()
    store.openCard('card-1')
    expect(store.openCardIds).toEqual(['card-1'])
    expect(store.activeCardId).toBe('card-1')
    expect(mockToastError).not.toHaveBeenCalled()
  })

  it('max-3 enforcement', () => {
    const store = useCardPopupStore()
    store.openCard('card-1')
    store.openCard('card-2')
    store.openCard('card-3')
    expect(store.openCardIds).toEqual(['card-1', 'card-2', 'card-3'])
    store.openCard('card-4')
    expect(mockToastError).toHaveBeenCalledWith('Close a card popup first (max 3 open)')
    expect(store.openCardIds).toEqual(['card-1', 'card-2', 'card-3'])
  })

  it('canOpen reflects capacity', () => {
    const store = useCardPopupStore()
    store.openCard('card-1')
    store.openCard('card-2')
    store.openCard('card-3')
    expect(store.canOpen).toBe(false)
  })

  it('closeCard removes and re-assigns active', () => {
    const store = useCardPopupStore()
    store.openCard('card-1')
    store.openCard('card-2')
    store.openCard('card-3')
    store.closeCard('card-3')
    expect(store.openCardIds).toEqual(['card-1', 'card-2'])
    expect(store.activeCardId).toBe('card-2')
  })

  it('closeCard of non-active leaves active unchanged', () => {
    const store = useCardPopupStore()
    store.openCard('card-1')
    store.openCard('card-2')
    store.closeCard('card-1')
    expect(store.openCardIds).toEqual(['card-2'])
    expect(store.activeCardId).toBe('card-2')
  })

  it('closeCard last card sets active to null', () => {
    const store = useCardPopupStore()
    store.openCard('card-1')
    store.closeCard('card-1')
    expect(store.openCardIds).toEqual([])
    expect(store.activeCardId).toBe(null)
  })

  it('bringToFront reorders openCardIds', () => {
    const store = useCardPopupStore()
    store.openCard('card-1')
    store.openCard('card-2')
    store.openCard('card-3')
    store.bringToFront('card-1')
    expect(store.openCardIds).toEqual(['card-2', 'card-3', 'card-1'])
  })

  it('positions cascade', () => {
    const store = useCardPopupStore()
    store.openCard('card-a')
    store.openCard('card-b')
    store.openCard('card-c')
    expect(store.positions['card-a']).toEqual({ x: 48, y: 48 })
    expect(store.positions['card-b']).toEqual({ x: 72, y: 72 })
    expect(store.positions['card-c']).toEqual({ x: 96, y: 96 })
  })

  it('positions cleaned on close', () => {
    const store = useCardPopupStore()
    store.openCard('card-1')
    expect(store.positions['card-1']).toBeDefined()
    store.closeCard('card-1')
    expect(store.positions['card-1']).toBeUndefined()
  })

  it('localStorage persistence', () => {
    const store1 = useCardPopupStore()
    store1.openCard('card-1')
    store1.openCard('card-2')

    // Simulate hard reload by creating a new store instance
    setActivePinia(createPinia())
    const store2 = useCardPopupStore()
    expect(store2.openCardIds).toEqual(['card-1', 'card-2'])
    expect(store2.positions['card-1']).toEqual({ x: 48, y: 48 })
    expect(store2.positions['card-2']).toEqual({ x: 72, y: 72 })
  })
})
