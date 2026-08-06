import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ChatInput from '~/components/chat/ChatInput.vue'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({ GET: mockGET }))
mockNuxtImport('useAppToast', () => () => ({ error: vi.fn(), success: vi.fn() }))

const personalities = [
  { id: 'p1', name: 'well', description: null, systemPrompt: '', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null }
]

function routedGet(url: string) {
  if (url.includes('/personalities')) return Promise.resolve({ data: personalities, error: undefined })
  if (url.includes('/presets')) return Promise.resolve({ data: [], error: undefined })
  if (url.includes('/llm/models')) return Promise.resolve({ data: [], error: undefined })
  return Promise.resolve({ data: [], error: undefined })
}

describe('ChatInput — personality picker (before a session exists)', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockImplementation(routedGet)
  })

  it('does not show the personality picker by default', async () => {
    const wrapper = await mountSuspended(ChatInput)
    await flushPromises()
    expect(wrapper.find('[title="Personality"]').exists()).toBe(false)
    expect(wrapper.find('[title="Manage personalities"]').exists()).toBe(false)
  })

  it('shows the personality picker and manage button when showPersonalityPicker is true', async () => {
    const wrapper = await mountSuspended(ChatInput, {
      props: { showPersonalityPicker: true }
    })
    await flushPromises()
    expect(wrapper.find('[title="Personality"]').exists()).toBe(true)
    expect(wrapper.find('[title="Manage personalities"]').exists()).toBe(true)
  })

  it('does not fetch personalities when showPersonalityPicker is false', async () => {
    await mountSuspended(ChatInput)
    await flushPromises()
    expect(mockGET).not.toHaveBeenCalledWith(expect.stringContaining('/personalities'))
  })

  it('emits the selected personalityId as the 5th send argument', async () => {
    const wrapper = await mountSuspended(ChatInput, {
      props: { showPersonalityPicker: true }
    })
    await flushPromises()
    ;(wrapper.vm as unknown as { selectedPersonalityId: string | null }).selectedPersonalityId = 'p1'
    await wrapper.find('textarea').setValue('hi')
    await wrapper.find('textarea').trigger('keydown', { key: 'Enter' })
    const emitted = wrapper.emitted('send')
    expect(emitted?.[0]?.[4]).toBe('p1')
  })

  it('shows the real personality name in the active chip once selected', async () => {
    const wrapper = await mountSuspended(ChatInput, {
      props: { showPersonalityPicker: true }
    })
    await flushPromises()
    ;(wrapper.vm as unknown as { selectedPersonalityId: string | null }).selectedPersonalityId = 'p1'
    await flushPromises()
    expect(wrapper.text()).toContain('Personality: well')
  })
})
