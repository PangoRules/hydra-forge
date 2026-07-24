import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CardComments from '~/components/card/CardComments.vue'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

describe('CardComments', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: { comments: [] }, error: undefined })
  })

  it('renders comments header', async () => {
    const wrapper = await mountSuspended(CardComments, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('Comments')
  })

  it('renders comment input', async () => {
    const wrapper = await mountSuspended(CardComments, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    await flushPromises()
    expect(wrapper.find('textarea[placeholder="Write a comment..."]').exists()).toBe(true)
  })

  it('shows empty state when no comments', async () => {
    const wrapper = await mountSuspended(CardComments, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('No comments yet')
  })
})