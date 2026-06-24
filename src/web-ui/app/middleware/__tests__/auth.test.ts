import { describe, it, expect } from 'vitest'
import authMiddleware from '~/middleware/auth'

describe('auth middleware', () => {
  it('allows access to /login without token', () => {
    const result = authMiddleware(
      { path: '/login', fullPath: '/login' } as any,
      { path: '/login', fullPath: '/login' } as any
    )
    expect(result).toBeUndefined()
  })

  it('allows access to /setup without token', () => {
    const result = authMiddleware(
      { path: '/setup', fullPath: '/setup' } as any,
      { path: '/setup', fullPath: '/setup' } as any
    )
    expect(result).toBeUndefined()
  })
})
