<script setup lang="ts">
import { ApiError } from '~/lib/api-error'
import { ApiRoutes } from '~/lib/routes'
import type { ChatMessageDto, ChatSessionDetailDto } from '~/types/chat'
import { MessageRole } from '~/types/chat'

const props = defineProps<{
  sessionId: string
  /** A message to send automatically once connected — used when this session was just
   * created from a compose-first "new chat" box so the first message isn't lost. */
  initialMessage?: string | null
  initialPresetId?: string | null
  initialModelId?: string | null
}>()

const emit = defineEmits<{
  initialMessageSent: []
  /** Fires after every successful fetch — lets the sidebar list stay in sync with
   * this session's title (e.g. the AI-generated title landing after the first
   * exchange) without polling or a shared store. */
  sessionRefreshed: [id: string, title: string, status: string]
}>()

const toast = useAppToast()
const api = useApi()

const session = ref<ChatSessionDetailDto | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

// Chat stream composable
const chatStream = useChatStream()

// Track which messageId is the in-flight assistant message so we can
// look up its content for streaming display
const streamingMessageId = ref<string | null>(null)

// Last stream failure, shown inline in the message list until the next send
const streamError = ref<string | null>(null)

// Register stream callbacks
chatStream.onStreamStart((messageId) => {
  streamingMessageId.value = messageId
  streamError.value = null
})

chatStream.onStreamDone((_messageId) => {
  streamingMessageId.value = null
  // Refresh session to get the persisted assistant message
  fetchSession()
})

chatStream.onStreamError((_messageId, _code, message) => {
  streamingMessageId.value = null
  streamError.value = message
})

async function fetchSession() {
  loading.value = true
  error.value = null
  try {
    const { data } = await api.GET<ChatSessionDetailDto>(
      ApiRoutes.Chat.sessions.detail(props.sessionId)
    )
    session.value = data as ChatSessionDetailDto
    // API returns newest-first; sort chronologically for display
    session.value.messages.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
    emit('sessionRefreshed', session.value.id, session.value.title, session.value.status)
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'Failed to load chat session'
  } finally {
    loading.value = false
  }
}

async function handleSend(
  content: string,
  presetId?: string | null,
  preferredModelId?: string | null
) {
  if (!session.value) return

  streamError.value = null

  // Add user message optimistically
  const userMsg: ChatMessageDto = {
    id: crypto.randomUUID(),
    sessionId: props.sessionId,
    role: MessageRole.User,
    content,
    inputTokens: 0,
    outputTokens: 0,
    cachedTokens: 0,
    modelName: null,
    imagesJson: null,
    createdAt: new Date().toISOString()
  }
  session.value.messages.push(userMsg)

  try {
    await chatStream.send(
      props.sessionId,
      content,
      presetId ?? undefined,
      preferredModelId ?? undefined
    )
  } catch (err) {
    // Remove optimistic user message on failure
    const idx = session.value.messages.findIndex(m => m.id === userMsg.id)
    if (idx !== -1) session.value.messages.splice(idx, 1)
    toast.error(err instanceof Error ? err.message : 'Failed to send message')
  }
}

async function handleCancel() {
  await chatStream.cancel(props.sessionId)
}

onMounted(async () => {
  await fetchSession()
  await chatStream.connect()
  await chatStream.join(props.sessionId)
  if (props.initialMessage) {
    // Tell the parent to forget this pending message before sending — the
    // parent owns the one-shot bookkeeping (this component gets recreated
    // on every re-entry to the session via :key, so a local flag here
    // wouldn't survive across visits and the message would resend forever).
    const message = props.initialMessage
    const presetId = props.initialPresetId
    const modelId = props.initialModelId
    emit('initialMessageSent')
    await handleSend(message, presetId, modelId)
  }
})

onUnmounted(() => {
  chatStream.leave()
  chatStream.disconnect()
})
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <div class="shrink-0 border-b border-gray-200 dark:border-gray-700 px-4 py-3">
      <h2 class="font-semibold truncate">
        {{ session?.title ?? 'Chat' }}
      </h2>
    </div>

    <div
      v-if="loading"
      class="flex-1 flex items-center justify-center text-muted text-sm"
    >
      Loading…
    </div>
    <div
      v-else-if="error"
      class="flex-1 flex items-center justify-center text-error text-sm p-4 text-center"
    >
      {{ error }}
    </div>
    <template v-else>
      <ChatMessageList
        :messages="session?.messages ?? []"
        :streaming-message="chatStream.streamingMessage.value"
        :stream-error="streamError"
      />

      <ChatInput
        :disabled="chatStream.isStreaming.value || session?.status !== 'Active'"
        @send="handleSend"
        @cancel="handleCancel"
      >
        <template
          v-if="chatStream.isStreaming.value"
          #cancel
        >
          <UButton
            icon="i-lucide-x"
            variant="ghost"
            size="xs"
            class="absolute right-2 bottom-2"
            @click="handleCancel"
          />
        </template>
      </ChatInput>
    </template>
  </div>
</template>
