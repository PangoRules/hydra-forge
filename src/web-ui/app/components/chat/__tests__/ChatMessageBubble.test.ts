import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import ChatMessageBubble from '~/components/chat/ChatMessageBubble.vue'
import { MessageRole } from '~/types/chat'

mockNuxtImport('useAuthStore', () => () => ({
  user: { username: 'testuser' }
}))

const mockToastAdd = vi.fn()
mockNuxtImport('useToast', () => () => ({ add: mockToastAdd }))

const baseMessage = {
  id: 'm1',
  sessionId: 's1',
  role: MessageRole.Assistant,
  content: 'Hello **world**',
  inputTokens: 0,
  outputTokens: 0,
  cachedTokens: 0,
  cost: null,
  modelName: null,
  imagesJson: null,
  createdAt: '2026-08-01T00:00:00Z'
}

function mockClipboard(writeText: ReturnType<typeof vi.fn>) {
  // navigator.clipboard is a getter-only property in happy-dom — Object.assign
  // throws "Cannot set property clipboard of [object Object] which has only a
  // getter". defineProperty with a fresh descriptor is required instead.
  Object.defineProperty(navigator, 'clipboard', {
    value: { writeText },
    configurable: true,
    writable: true
  })
}

describe('ChatMessageBubble — copy', () => {
  beforeEach(() => {
    mockToastAdd.mockReset()
    mockClipboard(vi.fn().mockResolvedValue(undefined))
  })

  it('copies the raw (unrendered) message content to the clipboard', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: { message: baseMessage }
    })
    await wrapper.find('[title="Copy message"]').trigger('click')
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('Hello **world**')
  })

  it('shows an error toast when the clipboard write fails', async () => {
    mockClipboard(vi.fn().mockRejectedValue(new Error('denied')))
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: { message: baseMessage }
    })
    await wrapper.find('[title="Copy message"]').trigger('click')
    await new Promise(resolve => setTimeout(resolve, 0))
    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'error' })
    )
  })
})

describe('ChatMessageBubble — highlight', () => {
  it('applies a highlight ring class when highlighted is true', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: { message: baseMessage, highlighted: true }
    })
    expect(wrapper.find(`#chat-message-${baseMessage.id}`).classes()).toContain('ring-2')
  })

  it('does not apply the highlight ring class by default', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: { message: baseMessage }
    })
    expect(wrapper.find(`#chat-message-${baseMessage.id}`).classes()).not.toContain('ring-2')
  })
})

describe('ChatMessageBubble — findQuery highlight', () => {
  it('wraps matched text in <mark> when findQuery is set', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: {
        message: { ...baseMessage, content: 'The quick brown fox' },
        findQuery: 'quick'
      }
    })
    expect(wrapper.html()).toContain('<mark')
  })

  it('does not add <mark> when findQuery is empty', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: {
        message: { ...baseMessage, content: 'The quick brown fox' }
      }
    })
    expect(wrapper.html()).not.toContain('<mark')
  })
})

describe('ChatMessageBubble — token/cost usage label', () => {
  it('shows tokens + cost for an assistant reply with both known', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: {
        message: { ...baseMessage, inputTokens: 120, outputTokens: 340, cachedTokens: 0, cost: 0.00123 }
      }
    })
    expect(wrapper.text()).toContain('460 tok')
    expect(wrapper.text()).toContain('$0.00123')
  })

  it('shows tokens only when cost is unknown (no PricePerToken configured)', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: {
        message: { ...baseMessage, inputTokens: 50, outputTokens: 50, cachedTokens: 0, cost: null }
      }
    })
    expect(wrapper.text()).toContain('100 tok')
    expect(wrapper.text()).not.toContain('$')
  })

  it('shows nothing for a zero-token message', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: { message: baseMessage }
    })
    expect(wrapper.text()).not.toContain('tok')
  })

  it('never shows a usage label on user messages, even with token fields set', async () => {
    const wrapper = await mountSuspended(ChatMessageBubble, {
      props: {
        message: {
          ...baseMessage,
          role: MessageRole.User,
          inputTokens: 10,
          outputTokens: 20,
          cost: 0.01
        }
      }
    })
    expect(wrapper.text()).not.toContain('tok')
  })
})
