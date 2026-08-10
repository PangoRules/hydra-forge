import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import PersonalityManager from '~/components/chat/PersonalityManager.vue'

const mockGET = vi.fn()
const mockPOST = vi.fn()
const mockPATCH = vi.fn()
const mockDELETE = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: mockPOST,
  PATCH: mockPATCH,
  DELETE: mockDELETE
}))

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

const personalities = [
  { id: 'p1', name: 'Sage', description: 'Wise guide', systemPrompt: 'You are Sage.', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null },
  { id: 'p2', name: 'Reviewer', description: null, systemPrompt: 'You are Reviewer.', isDefault: true, createdAt: '', updatedAt: '', archivedAt: null }
]

describe('PersonalityManager', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockPOST.mockReset()
    mockPATCH.mockReset()
    mockDELETE.mockReset()
    mockToastAdd.mockReset()
    mockGET.mockResolvedValue({ data: personalities, error: undefined })
  })

  it('lists existing personalities with the default badge', async () => {
    const wrapper = await mountSuspended(PersonalityManager)
    await flushPromises()
    expect(wrapper.text()).toContain('Sage')
    expect(wrapper.text()).toContain('Reviewer')
    expect(wrapper.text()).toContain('Default')
  })

  it('creates a new personality', async () => {
    mockPOST.mockResolvedValue({
      data: { id: 'p3', name: 'New Bot', description: null, systemPrompt: 'NB', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null },
      error: undefined
    })
    const wrapper = await mountSuspended(PersonalityManager)
    await flushPromises()
    await wrapper.find('[data-testid="new-personality"]').trigger('click')
    await wrapper.find('[data-testid="personality-name-input"]').setValue('New Bot')
    await wrapper.find('[data-testid="personality-prompt-input"]').setValue('NB')
    await wrapper.find('[data-testid="save-personality"]').trigger('click')
    await flushPromises()
    expect(mockPOST).toHaveBeenCalled()
  })

  it('shows an error toast when create fails (D-40)', async () => {
    mockPOST.mockRejectedValue(new Error('Name is required'))
    const wrapper = await mountSuspended(PersonalityManager)
    await flushPromises()
    await wrapper.find('[data-testid="new-personality"]').trigger('click')
    await wrapper.find('[data-testid="personality-name-input"]').setValue('X')
    await wrapper.find('[data-testid="personality-prompt-input"]').setValue('Y')
    await wrapper.find('[data-testid="save-personality"]').trigger('click')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error' })
    )
  })

  it('edits an existing personality', async () => {
    mockPATCH.mockResolvedValue({
      data: { id: 'p1', name: 'Sage (edited)', description: 'Wise guide', systemPrompt: 'You are Sage.', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null },
      error: undefined
    })
    const wrapper = await mountSuspended(PersonalityManager)
    await flushPromises()
    await wrapper.find('[data-testid="edit-p1"]').trigger('click')
    await wrapper.find('[data-testid="personality-name-input"]').setValue('Sage (edited)')
    await wrapper.find('[data-testid="save-personality"]').trigger('click')
    await flushPromises()
    expect(mockPATCH).toHaveBeenCalledWith(
      '/api/chat/personalities/p1',
      expect.objectContaining({ body: expect.objectContaining({ name: 'Sage (edited)' }) })
    )
  })

  it('shows an error toast when edit fails (D-40)', async () => {
    mockPATCH.mockRejectedValue(new Error('Update failed'))
    const wrapper = await mountSuspended(PersonalityManager)
    await flushPromises()
    await wrapper.find('[data-testid="edit-p1"]').trigger('click')
    await wrapper.find('[data-testid="save-personality"]').trigger('click')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error', title: 'Update failed' })
    )
  })

  it('archives a personality', async () => {
    mockDELETE.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(PersonalityManager)
    await flushPromises()
    await wrapper.find('[data-testid="archive-p1"]').trigger('click')
    await flushPromises()
    expect(mockDELETE).toHaveBeenCalledWith('/api/chat/personalities/p1')
  })

  it('sets a personality as default', async () => {
    mockPOST.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(PersonalityManager)
    await flushPromises()
    await wrapper.find('[data-testid="set-default-p1"]').trigger('click')
    await flushPromises()
    expect(mockPOST).toHaveBeenCalledWith('/api/chat/personalities/p1/default')
  })
})
