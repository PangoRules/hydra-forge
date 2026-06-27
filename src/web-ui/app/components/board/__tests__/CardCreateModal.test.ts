import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { h } from 'vue'
import CardCreateModal from '~/components/board/CardCreateModal.vue'
import { ApiError } from '~/lib/api-error'

const mockPOST = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

const columns = [{ id: 'col1', name: 'To Do', position: 0, wipLimit: null, color: null }]

describe('CardCreateModal', () => {
  beforeEach(() => {
    mockPOST.mockReset()
    mockToastAdd.mockReset()
  })

  it('creates a card and emits created on success', async () => {
    mockPOST.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(CardCreateModal, {
      props: { projectId: 'p1', columns, preselectedColumnId: 'col1' },
      global: {
        stubs: {
          AppModal: {
            render() {
              return h('div', { 'data-testid': 'app-modal' }, [
                this.$slots.body?.(),
                this.$slots.footer?.()
              ])
            }
          },
          MarkdownEditor: { template: '<div />' }
        }
      }
    })
    await flushPromises()

    await wrapper.find('input').setValue('New card')
    await wrapper.findAll('button').find(b => b.text() === 'Create')!.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('created')).toBeTruthy()
    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Card created', color: 'success' }))
  })

  it('shows an error toast and does not emit created when the API call fails', async () => {
    mockPOST.mockRejectedValue(new ApiError(400, 'VALIDATION_ERROR', 'Bad Request', 'Title is required', 'about:blank', 'corr-1'))
    const wrapper = await mountSuspended(CardCreateModal, {
      props: { projectId: 'p1', columns, preselectedColumnId: 'col1' },
      global: {
        stubs: {
          AppModal: {
            render() {
              return h('div', { 'data-testid': 'app-modal' }, [
                this.$slots.body?.(),
                this.$slots.footer?.()
              ])
            }
          },
          MarkdownEditor: { template: '<div />' }
        }
      }
    })
    await flushPromises()

    await wrapper.find('input').setValue('New card')
    await wrapper.findAll('button').find(b => b.text() === 'Create')!.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('created')).toBeFalsy()
    expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ title: 'Bad Request', color: 'error' }))
  })
})
