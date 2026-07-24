import { describe, it, expect, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { useRealtime } from '~/composables/useRealtime'

mockNuxtImport('useAuthToken', () => () => ({
  getToken: vi.fn(() => 'test-token')
}))

mockNuxtImport('useBoardStore', () => () => ({
  fetchBoard: vi.fn()
}))

describe('useRealtime', () => {
  it('returns connect and disconnect functions', () => {
    const rt = useRealtime()
    expect(typeof rt.connect).toBe('function')
    expect(typeof rt.disconnect).toBe('function')
  })

  it('starts disconnected', () => {
    const rt = useRealtime()
    expect(rt.isConnected.value).toBe(false)
    expect(rt.isReconnecting.value).toBe(false)
  })
})
