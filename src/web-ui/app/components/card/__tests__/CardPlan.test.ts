import { describe, it, expect, vi } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import CardPlan from '~/components/card/CardPlan.vue'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

describe('CardPlan', () => {
  it('renders Plan section header', async () => {
    mockGET.mockResolvedValue({ data: { plans: [] }, error: undefined })
    const wrapper = await mountSuspended(CardPlan, {
      props: { cardId: 'c1', projectId: 'p1' },
      global: {
        stubs: {
          MarkdownEditor: { template: '<div data-testid="markdown-editor" />' }
        }
      }
    })
    expect(wrapper.text()).toContain('Plan')
  })

  it('shows Add Plan button when no plan exists', async () => {
    mockGET.mockResolvedValue({ data: { plans: [] }, error: undefined })
    const wrapper = await mountSuspended(CardPlan, {
      props: { cardId: 'c1', projectId: 'p1' },
      global: {
        stubs: {
          MarkdownEditor: { template: '<div data-testid="markdown-editor" />' }
        }
      }
    })
    expect(wrapper.text()).toContain('Add Plan')
  })

  it('hides Add Plan button in readonly mode', async () => {
    mockGET.mockResolvedValue({ data: { plans: [] }, error: undefined })
    const wrapper = await mountSuspended(CardPlan, {
      props: { cardId: 'c1', projectId: 'p1', readonly: true },
      global: {
        stubs: {
          MarkdownEditor: { template: '<div data-testid="markdown-editor" />' }
        }
      }
    })
    expect(wrapper.text()).not.toContain('Add Plan')
    expect(wrapper.text()).not.toContain('Save')
  })
})
