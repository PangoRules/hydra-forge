import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import DocumentUploader from '~/components/chat/DocumentUploader.vue'

const mockToastAdd = vi.fn()
const mockGetToken = vi.fn(() => Promise.resolve('test-token'))
const mockConfig = { public: { apiBaseUrl: 'http://localhost:5000' } }

mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))
mockNuxtImport('useAuthToken', () => () => ({ getToken: mockGetToken }))
mockNuxtImport('useRuntimeConfig', () => () => mockConfig)

// Spy on fetch globally
const fetchSpy = vi.fn()
vi.stubGlobal('fetch', fetchSpy)

const doc = {
  id: 'd1',
  title: 'test.txt',
  contentType: 'text/plain',
  language: null,
  version: 1,
  createdAt: '',
  updatedAt: '',
  archivedAt: null
}

describe('DocumentUploader', () => {
  beforeEach(() => {
    mockToastAdd.mockReset()
    mockGetToken.mockResolvedValue('test-token')
    fetchSpy.mockReset()
  })

  it('renders drag-drop zone', async () => {
    const wrapper = await mountSuspended(DocumentUploader)
    expect(wrapper.find('[role="button"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Drop a file here')
  })

  it('renders hidden file input', async () => {
    const wrapper = await mountSuspended(DocumentUploader)
    expect(wrapper.find('input[type="file"]').exists()).toBe(true)
  })

  it('shows uploading state during fetch', async () => {
    fetchSpy.mockImplementation(() => new Promise(() => {})) // never resolves
    const wrapper = await mountSuspended(DocumentUploader)
    const input = wrapper.find('input[type="file"]')
    Object.defineProperty(input.element, 'files', {
      value: [new File(['hello'], 'test.txt', { type: 'text/plain' })],
      configurable: true
    })
    await input.trigger('change')
    await flushPromises()
    expect(wrapper.text()).toContain('Uploading…')
  })

  it('emits uploaded and resets state on success', async () => {
    fetchSpy.mockResolvedValue({
      ok: true,
      status: 201,
      json: () => Promise.resolve(doc)
    } as Response)
    const wrapper = await mountSuspended(DocumentUploader)
    const input = wrapper.find('input[type="file"]')
    Object.defineProperty(input.element, 'files', {
      value: [new File(['hello'], 'test.txt', { type: 'text/plain' })],
      configurable: true
    })
    await input.trigger('change')
    await flushPromises()
    await flushPromises()
    expect(wrapper.emitted('uploaded')).toBeTruthy()
    const emitted = wrapper.emitted('uploaded') as unknown[][]
    expect(emitted[0]![0]).toMatchObject({ id: 'd1', title: 'test.txt' })
  })

  it('shows error toast on fetch failure', async () => {
    fetchSpy.mockResolvedValue({
      ok: false,
      status: 413,
      json: () => Promise.resolve({ title: 'File too large', detail: 'max 10 MB' })
    } as Response)
    const wrapper = await mountSuspended(DocumentUploader)
    const input = wrapper.find('input[type="file"]')
    Object.defineProperty(input.element, 'files', {
      value: [new File(['x'.repeat(11 * 1024 * 1024)], 'big.txt', { type: 'text/plain' })],
      configurable: true
    })
    await input.trigger('change')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error' })
    )
  })

  it('shows validation error for oversized file before upload', async () => {
    const file = new File(['x'.repeat(11 * 1024 * 1024)], 'big.txt', { type: 'text/plain' })
    const wrapper = await mountSuspended(DocumentUploader)
    const input = wrapper.find('input[type="file"]')
    Object.defineProperty(input.element, 'files', {
      value: [file],
      configurable: true
    })
    await input.trigger('change')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error', title: expect.stringContaining('10 MB') })
    )
    expect(fetchSpy).not.toHaveBeenCalled()
  })

  it('shows validation error for unsupported file type', async () => {
    const wrapper = await mountSuspended(DocumentUploader)
    const input = wrapper.find('input[type="file"]')
    Object.defineProperty(input.element, 'files', {
      value: [new File([new ArrayBuffer(100)], 'image.png', { type: 'image/png' })],
      configurable: true
    })
    await input.trigger('change')
    await flushPromises()
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error', title: expect.stringContaining('Unsupported') })
    )
    expect(fetchSpy).not.toHaveBeenCalled()
  })
})
