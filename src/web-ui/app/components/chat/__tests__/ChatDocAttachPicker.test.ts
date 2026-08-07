import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ChatDocAttachPicker from '~/components/chat/ChatDocAttachPicker.vue'

const mockGET = vi.fn()
const mockPOST = vi.fn()
const mockToastSuccess = vi.fn()
const mockToastError = vi.fn()

mockNuxtImport('useAuthStore', () => () => ({
  token: 'test-token'
}))

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: mockPOST
}))

mockNuxtImport('useAppToast', () => () => ({
  success: mockToastSuccess,
  error: mockToastError
}))

const docList = [
  { id: 'd1', title: 'Design Doc' },
  { id: 'd2', title: 'Spec Doc' }
]

function mountPicker(open = true) {
  return mountSuspended(ChatDocAttachPicker, {
    props: { sessionId: 's1', open },
    global: {
      stubs: {
        AppModal: {
          template: '<div><slot name="header" /><slot name="body" /><slot name="footer" /></div>'
        }
      }
    }
  })
}

describe('ChatDocAttachPicker — debounce + stale response discard', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: [], error: undefined })
    mockToastSuccess.mockReset()
    mockToastError.mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('discards stale responses when user types again before the first request resolves', async () => {
    let resolveFirst: (value: unknown) => void
    const firstPromise = new Promise(resolve => { resolveFirst = resolve })

    let resolveSecond: (value: unknown) => void
    const secondPromise = new Promise(resolve => { resolveSecond = resolve })

    mockGET
      .mockImplementationOnce(() => firstPromise.then(() => ({ data: [] })))
      .mockImplementationOnce(() => firstPromise.then(() => ({ data: [{ id: 's1', title: 'Stale' }] })))
      .mockImplementationOnce(() => secondPromise.then(() => ({ data: docList })))

    const wrapper = await mountPicker()
    const input = wrapper.find('input')

    await input.setValue('a')
    await vi.advanceTimersByTimeAsync(300) // debounce fires, first API call

    await input.setValue('ab')
    await vi.advanceTimersByTimeAsync(300) // second debounce fires, second API call

    resolveFirst!({ data: [{ id: 's1', title: 'Stale' }] })
    resolveSecond!({ data: docList })
    await flushPromises()

    expect(wrapper.text()).toContain('Design Doc')
    expect(wrapper.text()).not.toContain('Stale')
  })
})

describe('ChatDocAttachPicker — search error surfacing', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    mockGET.mockReset()
    mockToastSuccess.mockReset()
    mockToastError.mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('shows the error message when the search fetch fails', async () => {
    mockGET.mockRejectedValue(new Error('Search failed'))
    const wrapper = await mountPicker()
    await flushPromises()

    expect(wrapper.find('.text-error').text()).toBe('Search failed')
  })

  it('clears the error when a new search starts', async () => {
    mockGET.mockRejectedValueOnce(new Error('First error'))
    const wrapper = await mountPicker()
    await flushPromises()
    expect(wrapper.find('.text-error').text()).toBe('First error')

    // Resolve next request successfully
    mockGET.mockResolvedValueOnce({ data: docList })
    const input = wrapper.find('input')
    await input.setValue('x')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()

    expect(wrapper.find('.text-error').exists()).toBe(false)
    expect(wrapper.text()).toContain('Design Doc')
  })
})

describe('ChatDocAttachPicker — pick success', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockPOST.mockReset()
    mockToastSuccess.mockReset()
    mockToastError.mockReset()
    mockGET.mockResolvedValue({ data: docList })
  })

  it('POSTs to the attach endpoint and closes the modal on success', async () => {
    mockPOST.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountPicker()
    await flushPromises()

    await wrapper.find('button').trigger('click')
    await flushPromises()

    expect(mockPOST).toHaveBeenCalledWith(
      '/api/chat/sessions/s1/documents',
      { body: { documentId: 'd1' } }
    )
    expect(mockToastSuccess).toHaveBeenCalledWith('"Design Doc" attached')
    expect(wrapper.find('[data-testid="app-modal"]').exists()).toBe(false)
  })

  it('emits picked with the document id', async () => {
    mockPOST.mockResolvedValue({ data: undefined, error: undefined })
    const wrapper = await mountPicker()
    await flushPromises()

    await wrapper.find('button').trigger('click')
    await flushPromises()

    const pickedEmits = wrapper.emitted('picked')
    expect(pickedEmits).toHaveLength(1)
    expect(pickedEmits![0]).toEqual(['d1'])
  })
})
