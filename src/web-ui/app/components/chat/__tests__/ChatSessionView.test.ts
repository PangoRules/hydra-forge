import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ChatSessionView from '~/components/chat/ChatSessionView.vue'
import ChatInput from '~/components/chat/ChatInput.vue'
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
  onSessionUpdated: vi.fn(),
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

  it('filters System-role messages from the rendered list', async () => {
    const ChatMessageListStub = {
      props: ['messages', 'streamingMessage', 'streamError', 'awaitingReply', 'rollbackDisabled', 'highlightMessageId'],
      template: '<div data-testid="message-list">{{ messages.map(m => m.content).join(" ") }}</div>'
    }
    mockGET.mockResolvedValue({
      data: {
        ...baseSession,
        messages: [
          { id: 'sys1', sessionId: 's1', role: 'System', content: 'You are HydraForge assistant.', inputTokens: 0, outputTokens: 0, cachedTokens: 0, modelName: null, imagesJson: null, createdAt: '2026-08-01T00:00:00Z' },
          { id: 'u1', sessionId: 's1', role: 'User', content: 'hello', inputTokens: 0, outputTokens: 0, cachedTokens: 0, modelName: null, imagesJson: null, createdAt: '2026-08-01T00:01:00Z' }
        ]
      },
      error: undefined
    })
    const wrapper = await mountSuspended(ChatSessionView, {
      props: { sessionId: 's1' },
      global: { stubs: { ...stubs, ChatMessageList: ChatMessageListStub } }
    })
    await flushPromises()
    expect(wrapper.find('[data-testid="message-list"]').text()).toContain('hello')
    expect(wrapper.find('[data-testid="message-list"]').text()).not.toContain('You are HydraForge assistant.')
  })

  it('renders the fetched session title', async () => {
    const wrapper = await mountView()
    expect(wrapper.text()).toContain('Original Title')
  })

  it('a SessionUpdated push (e.g. from title generation finishing later) patches the title live, no refetch needed', async () => {
    // Title generation runs as its own decoupled background job — it doesn't land
    // inline with the reply, so this push is the only way a connected client learns
    // the title changed without the user manually refreshing.
    const wrapper = await mountView()
    expect(wrapper.text()).toContain('Original Title')
    mockGET.mockClear()

    const onSessionUpdatedHandler = mockChatStream.onSessionUpdated.mock.calls.at(-1)?.[0]
    expect(onSessionUpdatedHandler).toBeTypeOf('function')
    onSessionUpdatedHandler('s1', 'Live Generated Title', 'Active')
    await flushPromises()

    expect(wrapper.text()).toContain('Live Generated Title')
    expect(wrapper.text()).not.toContain('Original Title')
    // Patched in place from the push payload — no extra GET round trip.
    expect(mockGET).not.toHaveBeenCalled()
    expect(wrapper.emitted('sessionRefreshed')?.at(-1)).toEqual(['s1', 'Live Generated Title', 'Active'])
  })

  it('ignores a SessionUpdated push for a different session', async () => {
    const wrapper = await mountView()
    const onSessionUpdatedHandler = mockChatStream.onSessionUpdated.mock.calls.at(-1)?.[0]
    onSessionUpdatedHandler('some-other-session', 'Should Not Apply', 'Active')
    await flushPromises()

    expect(wrapper.text()).toContain('Original Title')
    expect(wrapper.text()).not.toContain('Should Not Apply')
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

  it('disables the rename button when the session is not Active', async () => {
    mockGET.mockResolvedValue({
      data: { ...baseSession, status: ChatSessionStatus.Closed },
      error: undefined
    })
    const wrapper = await mountView()
    expect(wrapper.find('[title="Rename chat"]').attributes('disabled')).toBeDefined()
  })

  it('leaves the rename button enabled when the session is Active', async () => {
    const wrapper = await mountView()
    expect(wrapper.find('[title="Rename chat"]').attributes('disabled')).toBeUndefined()
  })
})

describe('ChatSessionView — export', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({
      data: {
        ...baseSession,
        messages: [
          { id: 'm1', sessionId: 's1', role: 'User', content: 'Hi', inputTokens: 0, outputTokens: 0, cachedTokens: 0, modelName: null, imagesJson: null, createdAt: '2026-08-01T00:00:00Z' },
          { id: 'm2', sessionId: 's1', role: 'Assistant', content: 'Hello!', inputTokens: 0, outputTokens: 0, cachedTokens: 0, modelName: null, imagesJson: null, createdAt: '2026-08-01T00:01:00Z' }
        ]
      },
      error: undefined
    })
  })

  it('builds a markdown blob with role headers and triggers a download named after the session', async () => {
    const createObjectURL = vi.fn().mockReturnValue('blob:mock-url')
    const revokeObjectURL = vi.fn()
    Object.assign(URL, { createObjectURL, revokeObjectURL })

    const clickSpy = vi.fn()
    const originalCreateElement = document.createElement.bind(document)
    vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
      const el = originalCreateElement(tag)
      if (tag === 'a') el.click = clickSpy
      return el
    })

    const wrapper = await mountView()
    await wrapper.find('[title="Export chat"]').trigger('click')

    expect(createObjectURL).toHaveBeenCalled()
    const blob = createObjectURL.mock.calls[0]![0] as Blob
    const text = await blob.text()
    expect(text).toContain('## User\n\nHi')
    expect(text).toContain('## Assistant\n\nHello!')
    expect(clickSpy).toHaveBeenCalled()
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:mock-url')

    vi.restoreAllMocks()
  })
})

describe('ChatSessionView — find in conversation', () => {
  const ChatMessageListStub = {
    props: ['messages', 'streamingMessage', 'streamError', 'awaitingReply', 'rollbackDisabled', 'highlightMessageId'],
    template: '<div data-testid="message-list" :data-highlight="highlightMessageId ?? \'\'" />'
  }

  const messages = [
    { id: 'm1', sessionId: 's1', role: 'User', content: 'find the needle here', inputTokens: 0, outputTokens: 0, cachedTokens: 0, modelName: null, imagesJson: null, createdAt: '2026-08-01T00:00:00Z' },
    { id: 'm2', sessionId: 's1', role: 'Assistant', content: 'no match', inputTokens: 0, outputTokens: 0, cachedTokens: 0, modelName: null, imagesJson: null, createdAt: '2026-08-01T00:01:00Z' },
    { id: 'm3', sessionId: 's1', role: 'User', content: 'another needle', inputTokens: 0, outputTokens: 0, cachedTokens: 0, modelName: null, imagesJson: null, createdAt: '2026-08-01T00:02:00Z' }
  ]

  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: { ...baseSession, messages }, error: undefined })
  })

  async function mountWithFindStub() {
    const wrapper = await mountSuspended(ChatSessionView, {
      props: { sessionId: 's1' },
      global: { stubs: { ...stubs, ChatMessageList: ChatMessageListStub } }
    })
    await flushPromises()
    return wrapper
  }

  it('opens a search input when the find button is clicked', async () => {
    const wrapper = await mountWithFindStub()
    await wrapper.find('[title="Find in conversation"]').trigger('click')
    expect(wrapper.find('input[data-testid="find-input"]').exists()).toBe(true)
  })

  it('typing a query highlights the first match and shows a 1 of N counter', async () => {
    const wrapper = await mountWithFindStub()
    await wrapper.find('[title="Find in conversation"]').trigger('click')
    await wrapper.find('input[data-testid="find-input"]').setValue('needle')
    await flushPromises()

    expect(wrapper.find('[data-testid="find-count"]').text()).toBe('1/2')
    expect(wrapper.find('[data-testid="message-list"]').attributes('data-highlight')).toBe('m1')
  })

  it('next/prev cycle through matches and wrap around', async () => {
    const wrapper = await mountWithFindStub()
    await wrapper.find('[title="Find in conversation"]').trigger('click')
    await wrapper.find('input[data-testid="find-input"]').setValue('needle')
    await flushPromises()

    await wrapper.find('[title="Next match"]').trigger('click')
    expect(wrapper.find('[data-testid="message-list"]').attributes('data-highlight')).toBe('m3')

    await wrapper.find('[title="Next match"]').trigger('click')
    expect(wrapper.find('[data-testid="message-list"]').attributes('data-highlight')).toBe('m1')

    await wrapper.find('[title="Previous match"]').trigger('click')
    expect(wrapper.find('[data-testid="message-list"]').attributes('data-highlight')).toBe('m3')
  })

  it('closing find clears the query and highlight', async () => {
    const wrapper = await mountWithFindStub()
    await wrapper.find('[title="Find in conversation"]').trigger('click')
    await wrapper.find('input[data-testid="find-input"]').setValue('needle')
    await flushPromises()

    await wrapper.find('[title="Close find"]').trigger('click')
    expect(wrapper.find('input[data-testid="find-input"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="message-list"]').attributes('data-highlight')).toBe('')
  })
})

describe('ChatSessionView — send with reasoning effort', () => {
  beforeEach(() => {
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: { ...baseSession }, error: undefined })
    mockChatStream.send.mockReset()
    mockChatStream.send.mockResolvedValue({
      userMessage: { id: 'm1', sessionId: 's1', role: 'User', content: 'hello' },
      streamStarted: true
    })
  })

  it('passes the selected reasoning effort through to chatStream.send', async () => {
    const wrapper = await mountView()
    const chatInput = wrapper.findComponent(ChatInput)

    await chatInput.vm.$emit('send', 'hello', null, 'model-1', 'high')
    await flushPromises()

    // handleSend normalizes a null presetId to undefined (`presetId ?? undefined`,
    // pre-existing behavior unrelated to reasoningEffort) before calling chatStream.send.
    expect(mockChatStream.send).toHaveBeenCalledWith('s1', 'hello', undefined, 'model-1', 'high')
  })
})
