import createClient from 'openapi-fetch'
import type { paths } from '~/types/api'
import { ApiError } from '~/lib/api-error'
import { UiRoutes } from '~/lib/routes'

// Typed response envelope — used at all call sites via `as` casts
export interface ApiResponse<T> {
  data: T | undefined
  error: ApiError | undefined
  response: Response
}

function createApiClient() {
  const config = useRuntimeConfig()
  const { getToken, clearToken } = useAuthToken()

  const client = createClient<paths>({
    baseUrl: config.public.apiBaseUrl as string,
    headers: {
      'Content-Type': 'application/json'
    }
  })

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

// Module-level singleton
let _api: ReturnType<typeof createApiClient> | undefined

function getApi() {
  if (!_api) {
    _api = createApiClient()
  }
  return _api
}

// Re-export ApiError so callers can catch it
export { ApiError }

/**
 * useApi — wraps openapi-fetch with auth middleware and 401 handling.
 * Route paths come from ApiRoutes constants (lib/routes.ts).
 * Response data is typed via `as` casts at call sites.
 */
export function useApi() {
  const client = getApi()

  // Thin wrappers that accept string paths (from ApiRoutes) and return ApiResponse<T>
  async function get<T>(url: string): Promise<ApiResponse<T>> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const result = await (client as any).GET(url)
    return result as ApiResponse<T>
  }

  async function post<T>(url: string, opts?: Record<string, unknown>): Promise<ApiResponse<T>> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const result = await (client as any).POST(url, opts)
    return result as ApiResponse<T>
  }

  async function put<T>(url: string, opts?: Record<string, unknown>): Promise<ApiResponse<T>> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const result = await (client as any).PUT(url, opts)
    return result as ApiResponse<T>
  }

  async function del<T>(url: string, opts?: Record<string, unknown>): Promise<ApiResponse<T>> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const result = await (client as any).DELETE(url, opts)
    return result as ApiResponse<T>
  }

  return { GET: get, POST: post, PUT: put, DELETE: del }
}
