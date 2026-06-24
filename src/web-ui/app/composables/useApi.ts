import createClient from 'openapi-fetch'
import type { paths } from '~/types/api'
import { ApiError } from '~/lib/api-error'
import { UiRoutes } from '~/lib/routes'

export function useApi() {
  const config = useRuntimeConfig()
  const { getToken, clearToken } = useAuthToken()

  const client = createClient<paths>({
    baseUrl: config.public.apiBaseUrl as string,
    headers: {
      'Content-Type': 'application/json'
    }
  })

  // Auth middleware: attach token to every request
  client.use({
    async onRequest({ request }) {
      const token = getToken()
      if (token) {
        request.headers.set('Authorization', `Bearer ${token}`)
      }
      return request
    },
    async onResponse({ response }) {
      if (response.status === 401) {
        clearToken()
        await navigateTo(UiRoutes.Login)
        return response
      }

      if (!response.ok) {
        const contentType = response.headers.get('content-type') ?? ''
        if (contentType.includes('application/problem+json')) {
          const body = await response.json() as Record<string, unknown>
          throw new ApiError(
            response.status,
            (body.code as string) ?? 'UNKNOWN',
            (body.title as string) ?? response.statusText,
            (body.detail as string) ?? null,
            (body.type as string) ?? 'about:blank',
            (body.correlationId as string) ?? 'unknown'
          )
        }
        throw new ApiError(
          response.status,
          'UNKNOWN',
          response.statusText,
          null,
          'about:blank',
          'unknown'
        )
      }

      return response
    }
  })

  return client
}
