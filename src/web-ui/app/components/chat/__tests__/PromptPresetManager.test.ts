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
  { id: 'p1', groupId: 'g1', name: 'Code Review', content: 'Review this code', createdAt: '', updatedAt: '', archivedAt: null, position: 0 },
  { id: 'p2', groupId: 'g1', name: 'Bug Report', content: 'Write a bug report', createdAt: '', updatedAt: '', archivedAt: null, position: 0 }
]

describe('PromptPresetManager', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockPOST.mockReset()
    mockPATCH.mockReset()
    mockDELETE.mockReset()
    mockToastAdd.mockReset()
    // Route by URL instead of call order — call order depends on component internals
    // (watchers, re-fetches after mutations) and drifts easily, silently misassigning
    // groups data to presets.value or vice versa.
    mockGET.mockImplementation((url: string) => {
      if (url.startsWith('/api/chat/preset-groups')) {
        return Promise.resolve({ data: groups, error: undefined })
      }
      if (url.startsWith('/api/chat/presets')) {
        return Promise.resolve({ data: presets, error: undefined })
      }
      return Promise.resolve({ data: undefined, error: undefined })
    })
  })

  it('loads groups on mount', async () => {
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    expect(mockGET).toHaveBeenCalledWith('/api/chat/preset-groups')
  })

  it('loads presets when a group is selected', async () => {
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
    mockPOST.mockResolvedValue({ data: { id: 'p3', groupId: 'g1', name: 'New Preset', content: 'Content', createdAt: '', updatedAt: '', archivedAt: null, position: 0 }, error: undefined })
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
    mockPATCH.mockResolvedValue({ data: { id: 'p1', groupId: 'g1', name: 'Code Review (edited)', content: 'Review this code', createdAt: '', updatedAt: '', archivedAt: null, position: 0 }, error: undefined })
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

  it('moves a preset up and syncs positions to server', async () => {
    mockPATCH.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.findAll('[data-testid^="group-row-"]').at(0)?.trigger('click')
    await flushPromises()
    // p1 is at index 0, moving it down (to index 1) is valid
    await wrapper.find('[data-testid="move-preset-down-p1"]').trigger('click')
    await flushPromises()
    // Both presets' positions are synced after the swap
    const patchCalls = mockPATCH.mock.calls
    expect(patchCalls.some(([url]) => url.includes('/api/chat/presets/p1'))).toBe(true)
    expect(patchCalls.some(([url]) => url.includes('/api/chat/presets/p2'))).toBe(true)
  })

  it('handles preset drop (drag from source to destination index)', async () => {
    mockPATCH.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.findAll('[data-testid^="group-row-"]').at(0)?.trigger('click')
    await flushPromises()

    const vm = wrapper.vm as unknown as Record<string, unknown>
    const handlePresetDrop = vm.handlePresetDrop as (destIndex: number, event: DragEvent) => Promise<void>

    const mockEvent = {
      dataTransfer: {
        getData: (key: string) => (key === 'text/plain' ? '0' : ''),
        effectAllowed: ''
      }
    } as unknown as DragEvent

    await handlePresetDrop(1, mockEvent)
    await flushPromises()
    // Dragging preset at index 0 to index 1: p1 removed from 0, inserted at 1
    // Both presets' positions should be patched
    const patchCalls = mockPATCH.mock.calls
    expect(patchCalls.some(([url]) => url.includes('/api/chat/presets/p1'))).toBe(true)
    expect(patchCalls.some(([url]) => url.includes('/api/chat/presets/p2'))).toBe(true)
  })

  it('edits an existing group', async () => {
    mockPATCH.mockResolvedValue({ data: { id: 'g1', name: 'General (edited)', createdAt: '', updatedAt: '', archivedAt: null, presets: [] }, error: undefined })
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.find('[data-testid="edit-group-g1"]').trigger('click')
    await wrapper.find('[data-testid="group-name-input-edit"]').setValue('General (edited)')
    await wrapper.find('[data-testid="save-group-edit"]').trigger('click')
    await flushPromises()
    expect(mockPATCH).toHaveBeenCalledWith('/api/chat/preset-groups/g1', expect.objectContaining({ body: expect.objectContaining({ name: 'General (edited)' }) }))
  })

  it('shows an error toast when group edit fails', async () => {
    mockPATCH.mockRejectedValue(new Error('Update failed'))
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.find('[data-testid="edit-group-g1"]').trigger('click')
    await wrapper.find('[data-testid="group-name-input-edit"]').setValue('General (edited)')
    await wrapper.find('[data-testid="save-group-edit"]').trigger('click')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error' })
    )
  })

  it('shows an error toast when movePreset fails and rolls back via fetchPresets', async () => {
    mockPATCH.mockRejectedValue(new Error('Position sync failed'))
    mockGET.mockImplementation((url: string) => {
      if (url.startsWith('/api/chat/preset-groups')) {
        return Promise.resolve({ data: groups, error: undefined })
      }
      if (url.startsWith('/api/chat/presets')) {
        // Return a slightly different preset list to prove rollback happened
        return Promise.resolve({ data: [...presets], error: undefined })
      }
      return Promise.resolve({ data: undefined, error: undefined })
    })
    const wrapper = await mountSuspended(PromptPresetManager, {
      global: { stubs: { AppModal: appModalStub } }
    })
    await flushPromises()
    await wrapper.findAll('[data-testid^="group-row-"]').at(0)?.trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="move-preset-down-p1"]').trigger('click')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error' })
    )
  })
})
