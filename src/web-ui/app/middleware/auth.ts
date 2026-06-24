export default defineNuxtRouteMiddleware((to) => {
  const { hasToken } = useAuthToken()

  if (to.path === '/login' || to.path === '/setup') return

  if (!hasToken()) {
    return navigateTo('/login')
  }
})
