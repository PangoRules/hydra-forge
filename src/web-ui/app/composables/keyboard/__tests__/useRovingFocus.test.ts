import { describe, it, expect } from 'vitest'
import { ref } from 'vue'
import { useRovingFocus } from '~/composables/keyboard/useRovingFocus'

describe('useRovingFocus', () => {
  it('starts at index 0', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    expect(nav.selectedIndex.value).toBe(0)
    expect(nav.selectedId.value).toBe('a')
  })

  it('next wraps around at the end', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    nav.next()
    nav.next()
    expect(nav.selectedIndex.value).toBe(2)
    nav.next()
    expect(nav.selectedIndex.value).toBe(0)
  })

  it('prev wraps around at the start', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    nav.prev()
    expect(nav.selectedIndex.value).toBe(2)
  })

  it('select sets an exact index', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    nav.select(2)
    expect(nav.selectedIndex.value).toBe(2)
    expect(nav.selectedId.value).toBe('c')
  })

  it('selectById finds the matching index', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    nav.selectById('b')
    expect(nav.selectedIndex.value).toBe(1)
  })

  it('selectById is a no-op for an unknown id', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    nav.selectById('zzz')
    expect(nav.selectedIndex.value).toBe(0)
  })

  it('isSelected reflects the current index', () => {
    const ids = ref(['a', 'b', 'c'])
    const nav = useRovingFocus(() => ids.value)
    nav.select(1)
    expect(nav.isSelected(1)).toBe(true)
    expect(nav.isSelected(0)).toBe(false)
  })

  it('next/prev are no-ops on an empty list', () => {
    const ids = ref<string[]>([])
    const nav = useRovingFocus(() => ids.value)
    nav.next()
    expect(nav.selectedIndex.value).toBe(0)
    nav.prev()
    expect(nav.selectedIndex.value).toBe(0)
  })

  it('selectedId is null for an empty list', () => {
    const ids = ref<string[]>([])
    const nav = useRovingFocus(() => ids.value)
    expect(nav.selectedId.value).toBeNull()
  })
})
