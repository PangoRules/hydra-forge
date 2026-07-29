import { describe, it, expect, beforeEach } from 'vitest'
import { nextTick } from 'vue'
import { useSidebarCollapse } from '~/composables/useSidebarCollapse'

describe('useSidebarCollapse', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('defaults to expanded (not collapsed) with no stored value', () => {
    const { collapsed } = useSidebarCollapse()
    expect(collapsed.value).toBe(false)
  })

  it('restores collapsed=true from localStorage', () => {
    localStorage.setItem('hydraforge-sidebar-collapsed', 'true')
    const { collapsed } = useSidebarCollapse()
    expect(collapsed.value).toBe(true)
  })

  it('persists changes to localStorage', async () => {
    const { collapsed } = useSidebarCollapse()
    collapsed.value = true
    await nextTick()
    expect(localStorage.getItem('hydraforge-sidebar-collapsed')).toBe('true')
  })
})
