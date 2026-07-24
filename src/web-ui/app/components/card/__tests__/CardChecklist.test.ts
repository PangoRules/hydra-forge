import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CardChecklist from '~/components/card/CardChecklist.vue'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

describe('CardChecklist', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: { items: [] }, error: undefined })
  })

  it('renders checklist header', async () => {
    const wrapper = await mountSuspended(CardChecklist, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('Checklist')
  })

  it('renders add item input', async () => {
    const wrapper = await mountSuspended(CardChecklist, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    await flushPromises()
    expect(wrapper.find('input[placeholder="Add item..."]').exists()).toBe(true)
  })

  it('shows 0/0 count when no items', async () => {
    const wrapper = await mountSuspended(CardChecklist, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('0/0')
  })
})