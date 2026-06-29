import { describe, it, expect, vi } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import CardSpec from '~/components/card/CardSpec.vue'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

describe('CardSpec', () => {
  it('renders Spec section header', async () => {
    mockGET.mockResolvedValue({ data: { specs: [] }, error: undefined })
    const wrapper = await mountSuspended(CardSpec, {
      props: { cardId: 'c1', projectId: 'p1' },
      global: {
        stubs: {
          MarkdownEditor: { template: '<div data-testid="markdown-editor" />' }
        }
      }
    })
    expect(wrapper.text()).toContain('Spec')
  })

  it('shows Create button when no spec exists', async () => {
    mockGET.mockResolvedValue({ data: { specs: [] }, error: undefined })
    const wrapper = await mountSuspended(CardSpec, {
      props: { cardId: 'c1', projectId: 'p1' },
      global: {
        stubs: {
          MarkdownEditor: { template: '<div data-testid="markdown-editor" />' }
        }
      }
    })
    expect(wrapper.text()).toContain('Create')
  })

  it('hides Create button in readonly mode', async () => {
    mockGET.mockResolvedValue({ data: { specs: [] }, error: undefined })
    const wrapper = await mountSuspended(CardSpec, {
      props: { cardId: 'c1', projectId: 'p1', readonly: true },
      global: {
        stubs: {
          MarkdownEditor: { template: '<div data-testid="markdown-editor" />' }
        }
      }
    })
    expect(wrapper.text()).not.toContain('Create')
    expect(wrapper.text()).not.toContain('Save')
  })
})
