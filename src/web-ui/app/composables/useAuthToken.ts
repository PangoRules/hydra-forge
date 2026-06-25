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
    // Nuxt's useCookie: setting to null should delete, but has edge cases.
    // Force-delete the cookie directly as a fallback.
    token.value = null
    try {
      document.cookie = 'auth_token=; Path=/; Max-Age=0; SameSite=Lax'
    } catch {
      // Not running in browser (SSR) — ignore
    }
  }
  function hasToken() {
    // Guard against Nuxt's useCookie leaving a truthy string like "null"
    // when setting the value to null.
    return !!token.value && token.value !== 'null'
  }

  return { getToken, setToken, clearToken, hasToken }
}
