<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { ChatMessageDto, ChatSessionDetailDto } from '~/types/chat'
import { MessageRole } from '~/types/chat'

const props = defineProps<{
  sessionId: string
}>()

const emit = defineEmits<{
  close: []
}>()

const toast = useAppToast()
const api = useApi()

const session = ref<ChatSessionDetailDto | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const isOpen = ref(true)

// Chat stream composable
const chatStream = useChatStream()

// Track which messageId is the in-flight assistant message so we can
// look up its content for streaming display
const streamingMessageId = ref<string | null>(null)

// Register stream callbacks
chatStream.onStreamStart((messageId) => {
  streamingMessageId.value = messageId
})

chatStream.onStreamDone((_messageId) => {
  streamingMessageId.value = null
  // Refresh session to get the persisted assistant message
  fetchSession()
})

chatStream.onStreamError((_messageId) => {
  streamingMessageId.value = null
})

async function fetchSession() {
  loading.value = true
  error.value = null
  try {
    const { data, error: apiError } = await api.GET<ChatSessionDetailDto>(
      ApiRoutes.Chat.sessions.detail(props.sessionId)
    )
    if (apiError) {
      error.value = apiError.title ?? 'Failed to load chat'
      return
    }
    session.value = data as ChatSessionDetailDto
  } catch {
    error.value = 'Failed to load chat session'
  } finally {
    loading.value = false
  }
}

async function handleSend(content: string, images?: { url: string, base64?: string }[]) {
  if (!session.value) return

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
    imagesJson: images ? JSON.stringify(images) : null,
    createdAt: new Date().toISOString()
  }
  session.value.messages.push(userMsg)

  try {
    await chatStream.send(props.sessionId, content, images)
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

function handleClose() {
  emit('close')
}

onMounted(async () => {
  await fetchSession()
  await chatStream.connect()
  await chatStream.join(props.sessionId)
})

onUnmounted(() => {
  chatStream.leave()
  chatStream.disconnect()
})
</script>

<template>
  <AppModal
    v-model:open="isOpen"
    :title="session?.title ?? 'Chat'"
    width="sm:max-w-2xl"
    :loading="loading"
    :error="error"
    @close="handleClose"
  >
    <template #body>
      <div class="flex flex-col h-[60vh] min-h-[400px]">
        <!-- Message list -->
        <ChatMessageList
          :messages="session?.messages ?? []"
          :streaming-message="chatStream.streamingMessage.value"
        />

        <!-- Input -->
        <ChatInput
          :disabled="chatStream.isStreaming.value || session?.status !== 'Active'"
          :preset-id="session?.personalityId"
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
      </div>
    </template>
  </AppModal>
</template>
