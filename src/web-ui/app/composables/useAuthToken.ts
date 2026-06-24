export function useAuthToken() {
  const config = useRuntimeConfig()

  const token = useCookie<string | null>('auth_token', {
    maxAge: config.public.authCookieMaxAge as number ?? 3600,
    sameSite: 'lax',
    secure: (config.public.authCookieSecure as boolean | undefined) ?? false
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
