import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import PersonalityManageModal from '~/components/chat/PersonalityManageModal.vue'

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

const appModalStub = defineComponent({
  props: ['open'],
  render() {
    return h('div', { 'data-testid': 'app-modal' }, [
      this.$slots.body?.()
    ])
  }
})

const personalities = [
  { id: 'p1', name: 'well', description: 'Sage guide', systemPrompt: 'You are the well.', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null },
  { id: 'p2', name: 'Carter', description: 'Investigative reviewer', systemPrompt: 'You are Carter.', isDefault: true, createdAt: '', updatedAt: '', archivedAt: null }
]

describe('PersonalityManageModal', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockPOST.mockReset()
    mockPATCH.mockReset()
    mockDELETE.mockReset()
    mockToastAdd.mockReset()
    mockGET.mockResolvedValue({ data: personalities, error: undefined })
  })

  it('lists existing personalities with the default badge', async () => {
    const wrapper = await mountSuspended(PersonalityManageModal, {
      props: { open: true },
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('well')
    expect(wrapper.text()).toContain('Carter')
    expect(wrapper.text()).toContain('Default')
  })

  it('creates a new personality and emits changed', async () => {
    mockPOST.mockResolvedValue({
      data: { id: 'p3', name: 'New Bot', description: null, systemPrompt: 'p', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null },
      error: undefined
    })
    const wrapper = await mountSuspended(PersonalityManageModal, {
      props: { open: true },
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.find('[data-testid="new-personality"]').trigger('click')
    await wrapper.find('[data-testid="personality-name-input"]').setValue('New Bot')
    await wrapper.find('[data-testid="personality-prompt-input"]').setValue('p')
    await wrapper.find('[data-testid="save-personality"]').trigger('click')
    await flushPromises()
    expect(mockPOST).toHaveBeenCalled()
    expect(wrapper.emitted('changed')).toBeTruthy()
  })

  it('shows an error toast when create fails', async () => {
    mockPOST.mockRejectedValue(new Error('Name is required'))
    const wrapper = await mountSuspended(PersonalityManageModal, {
      props: { open: true },
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.find('[data-testid="new-personality"]').trigger('click')
    await wrapper.find('[data-testid="personality-name-input"]').setValue('X')
    await wrapper.find('[data-testid="save-personality"]').trigger('click')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error' })
    )
  })

  it('archives a personality and emits changed', async () => {
    mockDELETE.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(PersonalityManageModal, {
      props: { open: true },
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.find('[data-testid="archive-p1"]').trigger('click')
    await flushPromises()
    expect(mockDELETE).toHaveBeenCalled()
    expect(wrapper.emitted('changed')).toBeTruthy()
  })

  it('edits an existing personality and emits changed', async () => {
    mockPATCH.mockResolvedValue({
      data: { id: 'p1', name: 'well (edited)', description: 'Sage guide', systemPrompt: 'You are the well.', isDefault: false, createdAt: '', updatedAt: '', archivedAt: null },
      error: undefined
    })
    const wrapper = await mountSuspended(PersonalityManageModal, {
      props: { open: true },
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.find('[title="Edit"]').trigger('click')
    expect((wrapper.find('[data-testid="personality-name-input"]').element as HTMLInputElement).value).toBe('well')
    await wrapper.find('[data-testid="personality-name-input"]').setValue('well (edited)')
    await wrapper.find('[data-testid="save-personality"]').trigger('click')
    await flushPromises()
    expect(mockPATCH).toHaveBeenCalledWith(
      '/api/chat/personalities/p1',
      expect.objectContaining({ body: expect.objectContaining({ name: 'well (edited)' }) })
    )
    expect(wrapper.emitted('changed')).toBeTruthy()
  })

  it('shows an error toast when edit fails', async () => {
    mockPATCH.mockRejectedValue(new Error('Update failed'))
    const wrapper = await mountSuspended(PersonalityManageModal, {
      props: { open: true },
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.find('[title="Edit"]').trigger('click')
    await wrapper.find('[data-testid="save-personality"]').trigger('click')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error', title: 'Update failed' })
    )
  })

  it('sets a personality as default and emits changed', async () => {
    mockPOST.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(PersonalityManageModal, {
      props: { open: true },
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.find('[data-testid="set-default-p1"]').trigger('click')
    await flushPromises()
    expect(mockPOST).toHaveBeenCalled()
    expect(wrapper.emitted('changed')).toBeTruthy()
  })
})
