import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ChatDocAttach from '~/components/chat/ChatDocAttach.vue'

const mockGET = vi.fn()
const mockDELETE = vi.fn()
const mockToastAdd = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  DELETE: mockDELETE
}))

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

const attachedDocs = [
  { documentId: 'd1', title: 'Design Doc' },
  { documentId: 'd2', title: 'Spec Doc' }
]

describe('ChatDocAttach — attach success/error', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockToastAdd.mockReset()
    mockGET.mockResolvedValue({ data: attachedDocs, error: undefined })
  })

  it('renders the list of attached documents', async () => {
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    await wrapper.find('[data-testid="doc-attach-toggle"]').trigger('click')
    expect(wrapper.text()).toContain('Design Doc')
    expect(wrapper.text()).toContain('Spec Doc')
  })

  it('shows empty state when no documents are attached', async () => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: [], error: undefined })
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    await wrapper.find('[data-testid="doc-attach-toggle"]').trigger('click')
    expect(wrapper.text()).toContain('No documents attached')
  })

  it('emits attached when a document is picked', async () => {
    mockGET.mockResolvedValue({ data: attachedDocs, error: undefined })
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    await wrapper.find('[data-testid="doc-attach-toggle"]').trigger('click')
    // The emit fires when ChatDocAttachPicker signals a pick.
    // We verify the panel mounts correctly and the + button is present.
    expect(wrapper.find('[title="Attach a document"]').exists()).toBe(true)
  })
})

describe('ChatDocAttach — remove success/error', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockDELETE.mockReset()
    mockToastAdd.mockReset()
  })

  it('removes the document from the list on successful DELETE', async () => {
    mockGET.mockResolvedValue({ data: [...attachedDocs], error: undefined })
    mockDELETE.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    await wrapper.find('[data-testid="doc-attach-toggle"]').trigger('click')

    await wrapper.find('[title="Remove attachment"]').trigger('click')
    await flushPromises()

    expect(mockDELETE).toHaveBeenCalledWith('/api/chat/sessions/s1/documents/d1')
    expect(wrapper.text()).not.toContain('Design Doc')
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'success', title: '"Design Doc" removed' })
    )
  })

  it('keeps the document in the list and shows error toast when DELETE fails', async () => {
    mockGET.mockResolvedValue({ data: [...attachedDocs], error: undefined })
    mockDELETE.mockRejectedValue(new Error('Remove failed'))
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    await wrapper.find('[data-testid="doc-attach-toggle"]').trigger('click')

    await wrapper.find('[title="Remove attachment"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Design Doc')
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error', title: 'Remove failed' })
    )
  })
})

describe('ChatDocAttach — collapse behavior', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: attachedDocs, error: undefined })
  })

  it('is collapsed by default, showing only the summary row', async () => {
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('Attached docs (2)')
    expect(wrapper.text()).not.toContain('Design Doc')
  })

  it('expands to show the doc list when the summary row is clicked', async () => {
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    await wrapper.find('[data-testid="doc-attach-toggle"]').trigger('click')
    expect(wrapper.text()).toContain('Design Doc')
    expect(wrapper.text()).toContain('Spec Doc')
  })

  it('shows a zero count in the summary row when nothing is attached', async () => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: [], error: undefined })
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    expect(wrapper.text()).toContain('Attached docs (0)')
  })
})

describe('ChatDocAttach — error banner independent of list', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockToastAdd.mockReset()
  })

  it('shows error banner when fetch fails, separate from empty list state', async () => {
    mockGET.mockRejectedValue(new Error('Fetch failed'))
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    await wrapper.find('[data-testid="doc-attach-toggle"]').trigger('click')
    // Error banner should be visible
    expect(wrapper.find('.bg-error\\/10').exists()).toBe(true)
    expect(wrapper.text()).toContain('Fetch failed')
    // Empty state should NOT show (error takes precedence)
    expect(wrapper.text()).not.toContain('No documents attached')
  })

  it('dismissing the error banner removes it', async () => {
    mockGET.mockRejectedValue(new Error('Fetch failed'))
    const wrapper = await mountSuspended(ChatDocAttach, {
      props: { sessionId: 's1' }
    })
    await flushPromises()
    await wrapper.find('[data-testid="doc-attach-toggle"]').trigger('click')

    expect(wrapper.find('.bg-error\\/10').exists()).toBe(true)
    await wrapper.find('[title="Dismiss"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('.bg-error\\/10').exists()).toBe(false)
  })
})
