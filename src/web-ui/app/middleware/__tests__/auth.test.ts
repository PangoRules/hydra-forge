import { describe, it, expect } from 'vitest'
import authMiddleware from '~/middleware/auth'
import { UiRoutes } from '~/lib/routes'

describe('auth middleware', () => {
  it('allows access to /login without token', () => {
    const result = authMiddleware(
      { path: UiRoutes.Login, fullPath: UiRoutes.Login } as any,
      { path: UiRoutes.Login, fullPath: UiRoutes.Login } as any
    )
    expect(result).toBeUndefined()
  })

  it('allows access to /setup without token', () => {
    const result = authMiddleware(
      { path: UiRoutes.Setup, fullPath: UiRoutes.Setup } as any,
      { path: UiRoutes.Setup, fullPath: UiRoutes.Setup } as any
    )
    expect(result).toBeUndefined()
  })
})
