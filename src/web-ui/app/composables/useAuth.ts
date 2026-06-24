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
    const { data, error } = await api.POST(ApiRoutes.Auth.Login, {
      body: { username, password }
    })
    if (error) throw error
    if (!data) throw new Error('Login failed: no data returned')

    const response = data as unknown as LoginResponse
    setToken(response.accessToken)
    store.setAuth(response.accessToken, {
      userId: response.userId,
      username: response.username,
      isAdmin: response.isAdmin
    })
  }

  function logout() {
    store.clearAuth()
    clearToken()
    navigateTo(UiRoutes.Login)
  }

  async function checkAuth() {
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
