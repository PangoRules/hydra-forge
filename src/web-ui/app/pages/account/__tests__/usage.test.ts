import { describe, it, expect, vi } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import UsagePage from '~/pages/account/usage.vue'

const mockGET = vi.fn()
const mockShowApiError = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PATCH: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

mockNuxtImport('useAppToast', () => () => ({
  success: vi.fn(),
  error: vi.fn(),
  showApiError: mockShowApiError,
  remove: vi.fn(),
  clear: vi.fn()
}))

const makeUsage = (overrides = {}) => ({
  tokensUsed: 1500,
  tokensBudget: 100000,
  imagesUsed: 3,
  imagesBudget: 50,
  periodStart: '2026-08-01T00:00:00Z',
  periodEnd: '2026-09-01T00:00:00Z',
  recentCalls: [
    {
      feature: 'PersonalChat',
      model: 'gpt-5',
      tokens: 150,
      images: 0,
      cost: 0.01,
      timestamp: '2026-08-02T10:00:00Z'
    }
  ],
  ...overrides
})

describe('account/usage.vue', () => {
  it('loads usage on mount and renders token/image bars + recent calls', async () => {
    mockGET.mockResolvedValue({ data: makeUsage(), error: undefined })
    const wrapper = await mountSuspended(UsagePage)
    await flushPromises()

    expect(mockGET).toHaveBeenCalledWith('/api/account/usage')
    expect(wrapper.text()).toContain('1,500 / 100,000')
    expect(wrapper.text()).toContain('2%')
    expect(wrapper.text()).toContain('3 / 50')
    expect(wrapper.text()).toContain('gpt-5')
  })

  it('shows "Unlimited" instead of a bar when budget is 0', async () => {
    mockGET.mockResolvedValue({
      data: makeUsage({ tokensBudget: 0, imagesBudget: 0 }),
      error: undefined
    })
    const wrapper = await mountSuspended(UsagePage)
    await flushPromises()

    const unlimitedLabels = wrapper.findAll('span').filter(s => s.text() === 'Unlimited')
    expect(unlimitedLabels).toHaveLength(2)
  })

  it('shows a toast on load failure instead of throwing', async () => {
    mockGET.mockRejectedValue(new Error('network down'))

    await mountSuspended(UsagePage)
    await flushPromises()

    expect(mockShowApiError).toHaveBeenCalled()
  })
})
