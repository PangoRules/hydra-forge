import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { usePresence } from '~/composables/usePresence'

mockNuxtImport('useAuthToken', () => () => ({
  getToken: vi.fn(() => 'test-token')
}))

describe('usePresence', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('returns connect, disconnect, focusCard functions', () => {
    const p = usePresence()
    expect(typeof p.connect).toBe('function')
    expect(typeof p.disconnect).toBe('function')
    expect(typeof p.focusCard).toBe('function')
  })
})
