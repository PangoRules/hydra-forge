import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ChatSessionView from '~/components/chat/ChatSessionView.vue'
import { ChatSessionStatus, AiEditMode } from '~/types/chat'

const mockGET = vi.fn()
const mockPATCH = vi.fn()
const mockPOST = vi.fn()
const mockDELETE = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  PATCH: mockPATCH,
  POST: mockPOST,
  DELETE: mockDELETE
}))

mockNuxtImport('useToast', () => () => ({ add: vi.fn() }))

const mockChatStream = {
  connect: vi.fn().mockResolvedValue(undefined),
  disconnect: vi.fn().mockResolvedValue(undefined),
  join: vi.fn().mockResolvedValue(undefined),
  leave: vi.fn().mockResolvedValue(undefined),
  send: vi.fn(),
  resend: vi.fn(),
  cancel: vi.fn().mockResolvedValue(undefined),
  streamingMessage: ref(null),
  isStreaming: ref(false),
  isConnected: ref(false),
  isReconnecting: ref(false),
  onStreamStart: vi.fn(),
  onStreamDelta: vi.fn(),
  onStreamDone: vi.fn(),
  onStreamError: vi.fn(),
  onReconnected: vi.fn(),
  clearStreaming: vi.fn()
}

mockNuxtImport('useChatStream', () => () => mockChatStream)

const baseSession = {
  id: 's1',
  title: 'Original Title',
  folderId: null,
  projectId: null,
  openCardId: null,
  personalityId: null,
  personalityArchived: false,
  status: ChatSessionStatus.Active,
  aiEditMode: AiEditMode.PerMutation,
  searchAllMyDocs: false,
  summary: null,
  createdAt: '2026-08-01T00:00:00Z',
  updatedAt: '2026-08-01T00:00:00Z',
  archivedAt: null,
  ownerId: 'u1',
  isShared: false,
  closedAt: null,
  messages: []
}

const stubs = { ChatMessageList: true, ChatInput: true, ConfirmDialog: true }

async function mountView() {
  const wrapper = await mountSuspended(ChatSessionView, {
    props: { sessionId: 's1' },
    global: { stubs }
  })
  await flushPromises()
  return wrapper
}

describe('ChatSessionView — rename', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockPATCH.mockReset()
    mockGET.mockResolvedValue({ data: { ...baseSession }, error: undefined })
  })

  it('renders the fetched session title', async () => {
    const wrapper = await mountView()
    expect(wrapper.text()).toContain('Original Title')
  })

  it('clicking the rename button shows an editable input pre-filled with the current title', async () => {
    const wrapper = await mountView()
    await wrapper.find('[title="Rename chat"]').trigger('click')
    const input = wrapper.find('input[data-testid="title-input"]')
    expect(input.exists()).toBe(true)
    expect((input.element as HTMLInputElement).value).toBe('Original Title')
  })

  it('submitting a new title PATCHes the full request shape and updates the displayed title', async () => {
    mockPATCH.mockResolvedValue({
      data: { ...baseSession, title: 'New Title' },
      error: undefined
    })
    const wrapper = await mountView()
    await wrapper.find('[title="Rename chat"]').trigger('click')
    const input = wrapper.find('input[data-testid="title-input"]')
    await input.setValue('New Title')
    await input.trigger('blur')
    await flushPromises()

    expect(mockPATCH).toHaveBeenCalledWith('/api/chat/sessions/s1', {
      body: {
        title: 'New Title',
        folderId: null,
        personalityId: null,
        aiEditMode: AiEditMode.PerMutation,
        searchAllMyDocs: false
      }
    })
    expect(wrapper.text()).toContain('New Title')
  })

  it('reverts the title and shows an error toast when the rename PATCH fails', async () => {
    mockPATCH.mockRejectedValue(new Error('boom'))
    const wrapper = await mountView()
    await wrapper.find('[title="Rename chat"]').trigger('click')
    const input = wrapper.find('input[data-testid="title-input"]')
    await input.setValue('New Title')
    await input.trigger('blur')
    await flushPromises()

    expect(wrapper.text()).toContain('Original Title')
    expect(wrapper.text()).not.toContain('New Title')
  })

  it('does not PATCH when the title is unchanged', async () => {
    const wrapper = await mountView()
    await wrapper.find('[title="Rename chat"]').trigger('click')
    const input = wrapper.find('input[data-testid="title-input"]')
    await input.trigger('blur')
    await flushPromises()

    expect(mockPATCH).not.toHaveBeenCalled()
  })
})
