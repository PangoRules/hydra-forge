import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuth } from '~/composables/useAuth'
import { useAuthStore } from '~/stores/auth'

describe('useAuth', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('logout clears auth and token', () => {
    const store = useAuthStore()
    store.setAuth('token', { userId: 'u1', username: 'test', isAdmin: false })

    const { logout } = useAuth()
    logout()

    expect(store.isAuthenticated).toBe(false)
    expect(store.token).toBeNull()
    expect(store.user).toBeNull()
  })

  it('logout clears auth and token', () => {
    const store = useAuthStore()
    store.setAuth('token', { userId: 'u1', username: 'test', isAdmin: false })

    const { logout } = useAuth()
    logout()

    expect(store.isAuthenticated).toBe(false)
    expect(store.token).toBeNull()
    expect(store.user).toBeNull()
  })

  it('checkAuth returns false when no token', async () => {
    const { checkAuth } = useAuth()
    const result = await checkAuth()
    expect(result).toBe(false)
  })
})
