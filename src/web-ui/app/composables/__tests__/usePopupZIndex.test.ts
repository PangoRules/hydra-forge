import { describe, it, expect, beforeEach, vi } from 'vitest'
import { usePopupZIndex } from '~/composables/usePopupZIndex'

describe('usePopupZIndex', () => {
  beforeEach(() => {
    // Reset module-level state between tests
    const { stack } = usePopupZIndex()
    stack.value = []
  })

  it('stack starts empty', () => {
    const { stack } = usePopupZIndex()
    expect(stack.value.length).toBe(0)
  })

  it('register adds entry', () => {
    const { stack, registerPopup } = usePopupZIndex()
    const closeFn = vi.fn()
    registerPopup('card-1', 'card', closeFn)
    expect(stack.value.length).toBe(1)
    expect(stack.value[0]!.id).toBe('card-1')
    expect(stack.value[0]!.kind).toBe('card')
    expect(stack.value[0]!.close).toBe(closeFn)
  })

  it('register is idempotent', () => {
    const { stack, registerPopup } = usePopupZIndex()
    const closeFn1 = vi.fn()
    const closeFn2 = vi.fn()
    registerPopup('card-1', 'card', closeFn1)
    registerPopup('card-1', 'card', closeFn2)
    expect(stack.value.length).toBe(1)
    expect(stack.value[0]!.close).toBe(closeFn2)
  })

  it('unregister removes entry', () => {
    const { stack, registerPopup, unregisterPopup } = usePopupZIndex()
    const closeFn1 = vi.fn()
    const closeFn2 = vi.fn()
    registerPopup('card-1', 'card', closeFn1)
    registerPopup('card-2', 'card', closeFn2)
    unregisterPopup('card-1')
    expect(stack.value.length).toBe(1)
    expect(stack.value[0]!.id).toBe('card-2')
  })

  it('bringToFront moves to end', () => {
    const { stack, registerPopup, bringToFront } = usePopupZIndex()
    registerPopup('a', 'card', vi.fn())
    registerPopup('b', 'card', vi.fn())
    registerPopup('c', 'card', vi.fn())
    bringToFront('a')
    expect(stack.value.map(e => e.id)).toEqual(['b', 'c', 'a'])
  })

  it('closeTopmost calls the topmost closeFn', () => {
    const { registerPopup, closeTopmost } = usePopupZIndex()
    const closeFn1 = vi.fn()
    const closeFn2 = vi.fn()
    registerPopup('card-1', 'card', closeFn1)
    registerPopup('card-2', 'card', closeFn2)
    closeTopmost()
    expect(closeFn2).toHaveBeenCalledTimes(1)
    expect(closeFn1).not.toHaveBeenCalled()
  })

  it('zIndexFor returns baseZ + index', () => {
    const { registerPopup, zIndexFor } = usePopupZIndex()
    registerPopup('card-1', 'card', vi.fn())
    registerPopup('card-2', 'card', vi.fn())
    registerPopup('card-3', 'card', vi.fn())
    expect(zIndexFor('card-1')).toBe(50)
    expect(zIndexFor('card-2')).toBe(51)
    expect(zIndexFor('card-3')).toBe(52)
  })

  it('zIndexFor returns baseZ for unknown id', () => {
    const { zIndexFor } = usePopupZIndex()
    expect(zIndexFor('nonexistent')).toBe(50)
  })

  it('dock + card popups share one stack', () => {
    const { stack, registerPopup, closeTopmost } = usePopupZIndex()
    const dockClose = vi.fn()
    const cardClose = vi.fn()
    registerPopup('chat-dock', 'dock', dockClose)
    registerPopup('card-1', 'card', vi.fn())
    registerPopup('card-2', 'card', cardClose)
    expect(stack.value.length).toBe(3)
    closeTopmost()
    expect(cardClose).toHaveBeenCalledTimes(1)
    expect(dockClose).not.toHaveBeenCalled()
  })

  it('listener lifecycle — Escape calls closeTopmost when stack non-empty', async () => {
    const { stack, registerPopup, closeTopmost } = usePopupZIndex()
    const closeFn = vi.fn()
    registerPopup('card-1', 'card', closeFn)

    // Simulate Escape keydown event
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
    await new Promise(r => setTimeout(r, 0))
    expect(closeFn).toHaveBeenCalledTimes(1)
  })
})
