import { defineStore } from 'pinia'

interface User {
  userId: string
  username: string
  isAdmin: boolean
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(null)
  const user = ref<User | null>(null)
  const isAuthenticated = computed(() => !!token.value)

  function setAuth(newToken: string, newUser: User) {
    token.value = newToken
    user.value = newUser
  }

  function clearAuth() {
    token.value = null
    user.value = null
  }

  function restoreToken(savedToken: string) {
    token.value = savedToken
  }

  return { token, user, isAuthenticated, setAuth, clearAuth, restoreToken }
})
