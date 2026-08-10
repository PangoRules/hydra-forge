import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import PromptPresetManager from '~/components/chat/PromptPresetManager.vue'

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

const groups = [
  { id: 'g1', name: 'General', createdAt: '', updatedAt: '', archivedAt: null, presets: [] },
  { id: 'g2', name: 'Development', createdAt: '', updatedAt: '', archivedAt: null, presets: [] }
]

const presets = [
  { id: 'p1', groupId: 'g1', name: 'Code Review', content: 'Review this code', createdAt: '', updatedAt: '', archivedAt: null },
  { id: 'p2', groupId: 'g1', name: 'Bug Report', content: 'Write a bug report', createdAt: '', updatedAt: '', archivedAt: null }
]

describe('PromptPresetManager', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockPOST.mockReset()
    mockPATCH.mockReset()
    mockDELETE.mockReset()
    mockToastAdd.mockReset()
    mockGET.mockResolvedValue({ data: groups, error: undefined })
  })

  it('loads groups on mount', async () => {
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    expect(mockGET).toHaveBeenCalledWith('/api/chat/preset-groups')
  })

  it('loads presets when a group is selected', async () => {
    mockGET.mockResolvedValueOnce({ data: groups, error: undefined })
    mockGET.mockResolvedValueOnce({ data: presets, error: undefined })
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.findAll('[data-testid^="group-row-"]').at(0)?.trigger('click')
    await flushPromises()
    expect(mockGET).toHaveBeenCalledWith('/api/chat/presets?groupId=g1')
  })

  it('creates a new group', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'g3', name: 'New Group', createdAt: '', updatedAt: '', archivedAt: null, presets: [] }, error: undefined })
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.find('[data-testid="new-group"]').trigger('click')
    await wrapper.find('[data-testid="group-name-input"]').setValue('New Group')
    await wrapper.find('[data-testid="save-group"]').trigger('click')
    await flushPromises()
    expect(mockPOST).toHaveBeenCalledWith('/api/chat/preset-groups', expect.objectContaining({ body: expect.objectContaining({ name: 'New Group' }) }))
  })

  it('shows an error toast when group creation fails', async () => {
    mockPOST.mockRejectedValue(new Error('Name is required'))
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.find('[data-testid="new-group"]').trigger('click')
    await wrapper.find('[data-testid="group-name-input"]').setValue('X')
    await wrapper.find('[data-testid="save-group"]').trigger('click')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error' })
    )
  })

  it('archives a group', async () => {
    mockDELETE.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.find('[data-testid="archive-group-g1"]').trigger('click')
    await flushPromises()
    expect(mockDELETE).toHaveBeenCalledWith('/api/chat/preset-groups/g1')
  })

  it('creates a new preset', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'p3', groupId: 'g1', name: 'New Preset', content: 'Content', createdAt: '', updatedAt: '', archivedAt: null }, error: undefined })
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.findAll('[data-testid^="group-row-"]').at(0)?.trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="new-preset"]').trigger('click')
    await wrapper.find('[data-testid="preset-name-input"]').setValue('New Preset')
    await wrapper.find('[data-testid="preset-content-input"]').setValue('Content')
    await wrapper.find('[data-testid="save-preset"]').trigger('click')
    await flushPromises()
    expect(mockPOST).toHaveBeenCalledWith('/api/chat/presets', expect.objectContaining({ body: expect.objectContaining({ name: 'New Preset', content: 'Content' }) }))
  })

  it('shows an error toast when preset creation fails', async () => {
    mockPOST.mockRejectedValue(new Error('Name is required'))
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.findAll('[data-testid^="group-row-"]').at(0)?.trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="new-preset"]').trigger('click')
    await wrapper.find('[data-testid="preset-name-input"]').setValue('X')
    await wrapper.find('[data-testid="preset-content-input"]').setValue('C')
    await wrapper.find('[data-testid="save-preset"]').trigger('click')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error' })
    )
  })

  it('archives a preset', async () => {
    mockDELETE.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.findAll('[data-testid^="group-row-"]').at(0)?.trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="archive-preset-p1"]').trigger('click')
    await flushPromises()
    expect(mockDELETE).toHaveBeenCalledWith('/api/chat/presets/p1')
  })

  it('edits an existing preset', async () => {
    mockPATCH.mockResolvedValue({ data: { id: 'p1', groupId: 'g1', name: 'Code Review (edited)', content: 'Review this code', createdAt: '', updatedAt: '', archivedAt: null }, error: undefined })
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.findAll('[data-testid^="group-row-"]').at(0)?.trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="edit-preset-p1"]').trigger('click')
    await wrapper.find('[data-testid="preset-name-input"]').setValue('Code Review (edited)')
    await wrapper.find('[data-testid="save-preset"]').trigger('click')
    await flushPromises()
    expect(mockPATCH).toHaveBeenCalledWith('/api/chat/presets/p1', expect.objectContaining({ body: expect.objectContaining({ name: 'Code Review (edited)' }) }))
  })

  it('shows an error toast when preset edit fails', async () => {
    mockPATCH.mockRejectedValue(new Error('Update failed'))
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.findAll('[data-testid^="group-row-"]').at(0)?.trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="edit-preset-p1"]').trigger('click')
    await wrapper.find('[data-testid="save-preset"]').trigger('click')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error', title: 'Update failed' })
    )
  })
})
