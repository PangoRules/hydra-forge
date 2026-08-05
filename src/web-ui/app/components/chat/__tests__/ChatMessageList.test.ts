import { describe, it, expect, vi } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ChatMessageList from '~/components/chat/ChatMessageList.vue'
import type { ChatMessageDto } from '~/types/chat'
import { MessageRole } from '~/types/chat'

mockNuxtImport('useAuthStore', () => () => ({
  user: { username: 'testuser' }
}))

mockNuxtImport('useToast', () => () => ({ add: vi.fn() }))

// 60 messages: MESSAGE_WINDOW_SIZE (50) means only the last 50 (index 10-59)
// render initially. Messages at index < 10, like the "needle" match below at
// index 5, are outside the render window until something pulls them in.
function buildMessages(count: number): ChatMessageDto[] {
  return Array.from({ length: count }, (_, i) => ({
    id: `m${i}`,
    sessionId: 's1',
    role: i % 2 === 0 ? MessageRole.User : MessageRole.Assistant,
    content: i === 5 ? 'find the needle here' : `message ${i}`,
    inputTokens: 0,
    outputTokens: 0,
    cachedTokens: 0,
    modelName: null,
    imagesJson: null,
    createdAt: `2026-08-01T00:${String(i).padStart(2, '0')}:00Z`
  }))
}

describe('ChatMessageList — find-in-conversation window expansion', () => {
  it('does not render a message outside the initial window by default', async () => {
    const messages = buildMessages(60)
    const wrapper = await mountSuspended(ChatMessageList, {
      props: { messages }
    })
    await flushPromises()

    expect(wrapper.find('#chat-message-m5').exists()).toBe(false)
    expect(wrapper.find('#chat-message-m59').exists()).toBe(true)
  })

  it('expands the window and reveals a highlighted message older than the last 50', async () => {
    const messages = buildMessages(60)
    const wrapper = await mountSuspended(ChatMessageList, {
      props: { messages, highlightMessageId: null }
    })
    await flushPromises()
    expect(wrapper.find('#chat-message-m5').exists()).toBe(false)

    await wrapper.setProps({ highlightMessageId: 'm5' })
    await flushPromises()
    await wrapper.vm.$nextTick()

    const target = wrapper.find('#chat-message-m5')
    expect(target.exists()).toBe(true)
    expect(target.classes()).toContain('ring-2')
  })

  it('does not touch the window when the highlighted message is already visible', async () => {
    const messages = buildMessages(60)
    const wrapper = await mountSuspended(ChatMessageList, {
      props: { messages, highlightMessageId: null }
    })
    await flushPromises()

    // m55 is within the initial 50-message window (index 10-59).
    await wrapper.setProps({ highlightMessageId: 'm55' })
    await flushPromises()
    await wrapper.vm.$nextTick()

    expect(wrapper.find('#chat-message-m55').exists()).toBe(true)
    // The window did not need to expand, so the oldest still-hidden message stays hidden.
    expect(wrapper.find('#chat-message-m0').exists()).toBe(false)
  })
})
