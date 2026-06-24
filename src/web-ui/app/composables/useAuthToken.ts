export function useAuthToken() {
  const token = useCookie<string | null>('auth_token', {
    maxAge: 60 * 60, // 1 hour
    sameSite: 'lax',
    secure: false // true in production
  })

  function getToken() {
    return token.value ?? null
  }
  function setToken(value: string) {
    token.value = value
  }
  function clearToken() {
    token.value = null
  }
  function hasToken() {
    return !!token.value
  }

  return { getToken, setToken, clearToken, hasToken }
}
