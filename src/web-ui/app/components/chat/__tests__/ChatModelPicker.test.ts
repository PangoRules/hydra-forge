import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import ChatModelPicker from '~/components/chat/ChatModelPicker.vue'
import type { AvailableModelDto } from '~/types/chat'

const mockGET = vi.fn()
mockNuxtImport('useApi', () => () => ({ GET: mockGET }))

const reasoningModel: AvailableModelDto = {
  providerModelConfigId: 'model-reasoning',
  modelName: 'Claude Opus 5',
  providerName: 'Anthropic',
  tier: 'Premium',
  supportsReasoning: true
}

const plainModel: AvailableModelDto = {
  providerModelConfigId: 'model-plain',
  modelName: 'GPT-4o',
  providerName: 'OpenAI',
  tier: 'Standard',
  supportsReasoning: false
}

beforeEach(() => {
  mockGET.mockReset()
  localStorage.clear()
})

// UPopover teleports its #content slot to document.body when open, which sits
// outside `wrapper`'s DOM subtree and is unreachable via wrapper.find (same
// portal problem documented for AppModal in CLAUDE.md's Vue Test Utils stub
// gotchas). Stub it to render both slots inline so content is always queryable
// without needing to click open a real floating-ui popover in jsdom.
const stubs = {
  UPopover: {
    template: '<div><slot /><slot name="content" /></div>'
  }
}

describe('ChatModelPicker effort row', () => {
  it('hides the effort row when the selected model does not support reasoning', async () => {
    mockGET.mockResolvedValue({ data: [plainModel] })
    const wrapper = await mountSuspended(ChatModelPicker, {
      props: { modelValue: null, feature: 'PersonalChat' },
      global: { stubs }
    })
    await new Promise(resolve => setTimeout(resolve, 0))

    expect(wrapper.find('[data-testid="reasoning-effort-row"]').exists()).toBe(false)
  })

  it('shows the effort row when the selected model supports reasoning', async () => {
    mockGET.mockResolvedValue({ data: [reasoningModel] })
    const wrapper = await mountSuspended(ChatModelPicker, {
      props: { modelValue: null, feature: 'PersonalChat' },
      global: { stubs }
    })
    await new Promise(resolve => setTimeout(resolve, 0))

    expect(wrapper.find('[data-testid="reasoning-effort-row"]').exists()).toBe(true)
  })

  it('persists the selected effort to localStorage per-feature', async () => {
    mockGET.mockResolvedValue({ data: [reasoningModel] })
    const wrapper = await mountSuspended(ChatModelPicker, {
      props: { modelValue: null, feature: 'PersonalChat' },
      global: { stubs }
    })
    await new Promise(resolve => setTimeout(resolve, 0))

    const highButton = wrapper.findAll('[data-testid^="effort-option-"]')
      .find(el => el.text() === 'High')
    await highButton?.trigger('click')

    expect(localStorage.getItem('hydraforge:chat:preferredEffort:PersonalChat')).toBe('high')
  })
})
