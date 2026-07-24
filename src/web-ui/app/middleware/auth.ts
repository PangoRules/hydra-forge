import { UiRoutes } from '~/lib/routes'

export default defineNuxtRouteMiddleware((to) => {
  const { hasToken } = useAuthToken()

  if (to.path === UiRoutes.Login || to.path === UiRoutes.Setup) return

  if (!hasToken()) {
    return navigateTo(UiRoutes.Login)
  }
})
