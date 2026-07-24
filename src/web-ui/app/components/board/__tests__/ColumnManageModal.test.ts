import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { h } from 'vue'
import ColumnManageModal from '~/components/board/ColumnManageModal.vue'

const mockPOST = vi.fn()
const mockPUT = vi.fn()
const mockDELETE = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: mockPUT,
  DELETE: mockDELETE
}))

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

const columns = [
  { id: 'col1', name: 'Backlog', position: 0, wipLimit: null, color: null },
  { id: 'col2', name: 'Done', position: 1, wipLimit: null, color: null }
]

describe('ColumnManageModal', () => {
  beforeEach(() => {
    mockPOST.mockReset()
    mockPUT.mockReset()
    mockDELETE.mockReset()
    mockToastAdd.mockReset()
  })

  it('renders all columns by name', async () => {
    const wrapper = await mountSuspended(ColumnManageModal, {
      props: { open: true, projectId: 'p1', columns },
      global: {
        stubs: {
          AppModal: { render() { return h('div', {}, this.$slots.body?.()) } }
        }
      }
    })
    expect(wrapper.text()).toContain('Backlog')
    expect(wrapper.text()).toContain('Done')
  })

  it('renames a column via PUT and shows it updated', async () => {
    mockPUT.mockResolvedValue({ data: { id: 'col1', name: 'Renamed', position: 0, wipLimit: null, color: null }, error: undefined })
    const wrapper = await mountSuspended(ColumnManageModal, {
      props: { open: true, projectId: 'p1', columns },
      global: {
        stubs: {
          AppModal: { render() { return h('div', {}, this.$slots.body?.()) } }
        }
      }
    })

    await wrapper.find('[data-testid="edit-col1"]').trigger('click')
    await wrapper.find('[data-testid="edit-name-col1"]').setValue('Renamed')
    await wrapper.find('[data-testid="save-col1"]').trigger('click')
    await flushPromises()

    expect(mockPUT).toHaveBeenCalledWith('/api/projects/p1/Columns/col1', {
      body: { name: 'Renamed', color: null, wipLimit: null }
    })
  })

  it('deletes a column via DELETE after confirming', async () => {
    mockDELETE.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(ColumnManageModal, {
      props: { open: true, projectId: 'p1', columns },
      global: {
        stubs: {
          AppModal: { render() { return h('div', {}, this.$slots.body?.()) } }
        }
      }
    })

    await wrapper.find('[data-testid="delete-col2"]').trigger('click')
    await (wrapper.vm as any).confirmDelete()
    await flushPromises()

    expect(mockDELETE).toHaveBeenCalledWith('/api/projects/p1/Columns/col2')
  })

  it('creates a new column via POST', async () => {
    mockPOST.mockResolvedValue({ data: { id: 'col3', name: 'New Col', position: 2, wipLimit: null, color: null }, error: undefined })
    const wrapper = await mountSuspended(ColumnManageModal, {
      props: { open: true, projectId: 'p1', columns },
      global: {
        stubs: {
          AppModal: { render() { return h('div', {}, this.$slots.body?.()) } }
        }
      }
    })

    await wrapper.find('[data-testid="new-column-name"]').setValue('New Col')
    await wrapper.find('[data-testid="new-column-add"]').trigger('click')
    await flushPromises()

    expect(mockPOST).toHaveBeenCalledWith('/api/projects/p1/Columns', {
      body: { name: 'New Col', color: null, wipLimit: null }
    })
  })
})