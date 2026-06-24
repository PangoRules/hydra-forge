import { describe, it, expect, beforeEach } from 'vitest'
import { useAuthToken } from '~/composables/useAuthToken'

describe('useAuthToken', () => {
  beforeEach(() => {
    const { clearToken } = useAuthToken()
    clearToken()
  })

  it('hasToken returns false when no token set', () => {
    const { hasToken } = useAuthToken()
    expect(hasToken()).toBe(false)
  })

  it('setToken and getToken round-trip', () => {
    const { setToken, getToken, hasToken } = useAuthToken()
    setToken('test-jwt-token')
    expect(getToken()).toBe('test-jwt-token')
    expect(hasToken()).toBe(true)
  })

  it('clearToken removes token', () => {
    const { setToken, clearToken, hasToken } = useAuthToken()
    setToken('test-jwt-token')
    clearToken()
    expect(hasToken()).toBe(false)
  })

  it('getToken returns falsy value when no token', () => {
    const { getToken } = useAuthToken()
    // useCookie initializes with empty string when unset
    expect(getToken()).toBeFalsy()
  })
})
