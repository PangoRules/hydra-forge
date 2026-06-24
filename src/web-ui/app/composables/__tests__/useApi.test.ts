import { describe, it, expect } from 'vitest'
import { useApi } from '~/composables/useApi'

describe('useApi', () => {
  it('returns openapi-fetch client with all HTTP methods', () => {
    const client = useApi()
    expect(client).toBeDefined()
    expect(typeof client.GET).toBe('function')
    expect(typeof client.POST).toBe('function')
    expect(typeof client.PUT).toBe('function')
    expect(typeof client.DELETE).toBe('function')
  })

  it('client is created without throwing', () => {
    const client = useApi()
    expect(client).toBeTruthy()
  })
})
