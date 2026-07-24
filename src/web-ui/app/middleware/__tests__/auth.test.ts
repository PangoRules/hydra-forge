import { describe, it, expect } from 'vitest'
import authMiddleware from '~/middleware/auth'
import { UiRoutes } from '~/lib/routes'

describe('auth middleware', () => {
  it('allows access to /login without token', () => {
    const mockTo = { path: UiRoutes.Login } as const satisfies { path: string }
    const mockFrom = { path: UiRoutes.Login } as const satisfies { path: string }
    const result = authMiddleware(mockTo as never, mockFrom as never)
    expect(result).toBeUndefined()
  })

  it('allows access to /setup without token', () => {
    const mockTo = { path: UiRoutes.Setup } as const satisfies { path: string }
    const mockFrom = { path: UiRoutes.Setup } as const satisfies { path: string }
    const result = authMiddleware(mockTo as never, mockFrom as never)
    expect(result).toBeUndefined()
  })
})
