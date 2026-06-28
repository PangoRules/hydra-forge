import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CardDependencies from '~/components/card/CardDependencies.vue'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

describe('CardDependencies', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: { relationships: [] }, error: undefined })
  })

  it('renders dependencies header', async () => {
    const wrapper = await mountSuspended(CardDependencies, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('Dependencies')
  })

  it('shows empty state when no dependencies', async () => {
    const wrapper = await mountSuspended(CardDependencies, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('No dependencies')
  })
})