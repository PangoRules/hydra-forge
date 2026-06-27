import { ApiRoutes, UiRoutes } from '~/lib/routes'

interface LoginResponse {
  accessToken: string
  expiresAt: string
  userId: string
  username: string
  isAdmin: boolean
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

    setToken(data.accessToken)
    store.setAuth(data.accessToken, {
      userId: data.userId,
      username: data.username,
      isAdmin: data.isAdmin
    })
  }

  function logout() {
    store.clearAuth()
    clearToken()
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

  return { login, logout, checkAuth, isAuthenticated: store.isAuthenticated }
}
