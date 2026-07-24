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
    // Delete the browser cookie synchronously FIRST — before any navigation
    // can interrupt it. Nuxt's useCookie token.value = null may be async or
    // get interrupted by navigateTo / window.location redirect.
    try {
      const secure = (config.public.authCookieSecure as boolean | undefined) ?? false
      document.cookie = `auth_token=; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax${secure ? '; Secure' : ''}`
    } catch {
      // Not running in browser (SSR) — ignore
    }
    // Then tell Nuxt's reactive cookie ref to match
    token.value = null
  }
  function hasToken() {
    // Guard against Nuxt's useCookie leaving a truthy string like "null"
    // when setting the value to null.
    return !!token.value && token.value !== 'null'
  }

  return { getToken, setToken, clearToken, hasToken }
}
