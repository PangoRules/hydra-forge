import { ApiRoutes, UiRoutes } from '~/lib/routes'

interface LoginResponse {
  accessToken: string
  expiresAt: string
  userId: string
  username: string
  isAdmin: boolean
}

interface AuthUser {
  userId: string
  username: string
  isAdmin: boolean
}

type AuthChannelMessage
  = | { type: 'login', token: string, user: AuthUser }
    | { type: 'logout' }

// Cross-tab sync: the auth_token cookie is shared by every tab on this origin,
// but each tab's Pinia store only reads that cookie once, at layout mount
// (checkAuth()). Without this, a tab left open under one user keeps using that
// user's stale in-memory token/store forever after a different tab logs in as
// someone else on the same browser — the cookie changes, that tab's store
// never does. BroadcastChannel pushes login/logout to every other open tab
// immediately. Module-level channel is fine here (client-only, per-tab —
// unlike useApi.ts's old singleton this holds no per-request/per-user state).
const AUTH_CHANNEL_NAME = 'hydraforge-auth'
let authChannel: BroadcastChannel | undefined

function getAuthChannel(): BroadcastChannel | undefined {
  if (typeof BroadcastChannel === 'undefined') return undefined
  if (!authChannel) authChannel = new BroadcastChannel(AUTH_CHANNEL_NAME)
  return authChannel
}

export function useAuth() {
  const store = useAuthStore()
  const { setToken, clearToken, getToken, hasToken } = useAuthToken()
  const api = useApi()

  async function login(username: string, password: string) {
    // api.POST throws on non-2xx — no error destructure needed
    const { data } = await api.POST(ApiRoutes.Auth.Login, {
      body: { username, password }
    }) as { data: LoginResponse }
    if (!data) throw new Error('Login failed: no data returned')

    const user: AuthUser = {
      userId: data.userId,
      username: data.username,
      isAdmin: data.isAdmin
    }
    setToken(data.accessToken)
    store.setAuth(data.accessToken, user)
    getAuthChannel()?.postMessage({ type: 'login', token: data.accessToken, user } satisfies AuthChannelMessage)
  }

  function logout() {
    store.clearAuth()
    clearToken()
    getAuthChannel()?.postMessage({ type: 'logout' } satisfies AuthChannelMessage)
    navigateTo(UiRoutes.Login)
  }

  function checkAuth() {
    if (!hasToken()) return false
    const token = getToken()
    if (token) {
      store.restoreToken(token)
      return true
    }
    return false
  }

  // Call once per tab (layout setup) to pick up login/logout events fired by
  // other tabs on the same origin — see AUTH_CHANNEL_NAME comment above.
  function listenForAuthChanges() {
    const channel = getAuthChannel()
    if (!channel) return

    channel.onmessage = (event: MessageEvent<AuthChannelMessage>) => {
      if (event.data.type === 'login') {
        setToken(event.data.token)
        store.setAuth(event.data.token, event.data.user)
      } else {
        store.clearAuth()
        clearToken()
        navigateTo(UiRoutes.Login)
      }
    }
  }

  return { login, logout, checkAuth, listenForAuthChanges, isAuthenticated: store.isAuthenticated }
}
