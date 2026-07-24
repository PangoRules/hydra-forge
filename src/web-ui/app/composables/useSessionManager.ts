import { ApiRoutes } from '~/lib/routes'

/**
 * Session manager composable.
 *
 * Decodes the JWT's `exp` claim and runs a periodic check to detect
 * imminent session expiry (within 2 minutes). Exposes reactive state
 * for a warning modal and an `extendSession()` method that calls
 * `POST /api/Auth/refresh` to re-issue the token.
 *
 * Call `start()` when the layout mounts and `stop()` on unmount.
 */
export function useSessionManager() {
  const store = useAuthStore()
  const { setToken } = useAuthToken()

  /** Warn when ≤ 2 minutes of JWT lifetime remain */
  const EXPIRY_THRESHOLD_SECONDS = 120
  const CHECK_INTERVAL_MS = 1000 // 1s tick for live countdown

  const isExpired = ref(false)
  const isExpiringSoon = ref(false)
  const isExtending = ref(false)
  const timeRemaining = ref(0) // seconds

  /** Derived from `timeRemaining` — ticks every second via the interval. */
  const remainingFormatted = computed(() => {
    const s = timeRemaining.value
    if (s <= 0) return 'Expired'
    const mins = Math.floor(s / 60)
    const secs = s % 60
    if (mins > 0) return `${mins}m ${secs}s`
    return `${secs}s`
  })

  let intervalId: ReturnType<typeof setInterval> | null = null

  // ── helpers ───────────────────────────────────────────────

  /** Decode a JWT's payload without cryptographic verification. */
  function decodeExp(token: string): number | null {
    try {
      const parts = token.split('.')
      if (parts.length !== 3) return null
      const payloadStr = parts[1]
      if (!payloadStr) return null
      const payload = JSON.parse(atob(payloadStr))
      return typeof payload.exp === 'number' ? payload.exp : null
    } catch {
      return null
    }
  }

  // ── session check ─────────────────────────────────────────

  function checkSession() {
    if (!store.token) return

    const exp = decodeExp(store.token)
    if (exp === null) return

    const now = Math.floor(Date.now() / 1000)
    const remaining = Math.max(0, exp - now)

    timeRemaining.value = remaining

    if (remaining <= 0) {
      isExpired.value = true
      isExpiringSoon.value = false
    } else if (remaining <= EXPIRY_THRESHOLD_SECONDS) {
      isExpired.value = false
      isExpiringSoon.value = true
    } else {
      isExpired.value = false
      isExpiringSoon.value = false
    }
  }

  // ── extend session ────────────────────────────────────────

  async function extendSession() {
    if (isExtending.value) return
    isExtending.value = true

    const api = useApi()
    try {
      const { data, error } = await api.POST(ApiRoutes.Auth.refresh, {})
      if (error) throw error
      if (!data) throw new Error('No data from refresh')

      const response = data as { accessToken: string, expiresAt: string }
      setToken(response.accessToken)

      const { userId, username, isAdmin } = store.user ?? {}
      // Restore user info that was already in the store
      store.setAuth(response.accessToken, {
        userId: userId ?? '',
        username: username ?? '',
        isAdmin: isAdmin ?? false
      })

      // Re-evaluate session state with the new token
      isExpiringSoon.value = false
      isExpired.value = false
      checkSession()
    } catch {
      // Refresh failed — the periodic check will handle expiry
    } finally {
      isExtending.value = false
    }
  }

  // ── lifecycle ─────────────────────────────────────────────

  function start() {
    checkSession()
    intervalId = setInterval(checkSession, CHECK_INTERVAL_MS)
  }

  function stop() {
    if (intervalId !== null) {
      clearInterval(intervalId)
      intervalId = null
    }
  }

  return {
    isExpired,
    isExpiringSoon,
    isExtending,
    timeRemaining,
    remainingFormatted,
    extendSession,
    start,
    stop
  }
}
