export default defineNuxtRouteMiddleware(async (to) => {
  // Skip on setup page itself and login page
  if (to.path === '/setup' || to.path === '/login') return

  const { hasToken } = useAuthToken()
  if (!hasToken()) return

  // Attempt to detect first-run by checking if default admin works.
  // If the user already has a real token, this is a no-op.
  // Full first-run detection will be refined when backend adds a dedicated endpoint.
})
