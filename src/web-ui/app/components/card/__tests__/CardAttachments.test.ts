import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CardAttachments from '~/components/card/CardAttachments.vue'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

describe('CardAttachments', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: { attachments: [] }, error: undefined })
  })

  it('renders attachments header', async () => {
    const wrapper = await mountSuspended(CardAttachments, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('Attachments')
  })

  it('renders upload button', async () => {
    const wrapper = await mountSuspended(CardAttachments, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('Upload')
  })

  it('shows empty state when no attachments', async () => {
    const wrapper = await mountSuspended(CardAttachments, {
      props: { cardId: 'c1', projectId: 'p1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('No attachments')
  })
})