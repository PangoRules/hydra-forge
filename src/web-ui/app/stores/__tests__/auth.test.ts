import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('starts unauthenticated', () => {
    const store = useAuthStore()
    expect(store.isAuthenticated).toBe(false)
    expect(store.token).toBeNull()
    expect(store.user).toBeNull()
  })

  it('setAuth sets token and user', () => {
    const store = useAuthStore()
    store.setAuth('jwt-token', { userId: 'u1', username: 'test', isAdmin: false })
    expect(store.isAuthenticated).toBe(true)
    expect(store.token).toBe('jwt-token')
    expect(store.user?.username).toBe('test')
  })

  it('clearAuth resets state', () => {
    const store = useAuthStore()
    store.setAuth('jwt-token', { userId: 'u1', username: 'test', isAdmin: false })
    store.clearAuth()
    expect(store.isAuthenticated).toBe(false)
    expect(store.token).toBeNull()
    expect(store.user).toBeNull()
  })

  it('isAuthenticated false when only token set', () => {
    const store = useAuthStore()
    store.token = 'jwt-token'
    expect(store.isAuthenticated).toBe(false)
  })

  it('isAuthenticated false when only user set', () => {
    const store = useAuthStore()
    store.user = { userId: 'u1', username: 'test', isAdmin: false }
    expect(store.isAuthenticated).toBe(false)
  })
})
