<script setup lang="ts">
import { ApiError } from '~/lib/api-error'
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

// Track the selected preset/personality ID for the chat session
const selectedPresetId = ref<string | null>(null)

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
    const { data } = await api.GET<ChatSessionDetailDto>(
      ApiRoutes.Chat.sessions.detail(props.sessionId)
    )
    session.value = data as ChatSessionDetailDto
    // API returns newest-first; sort chronologically for display
    session.value.messages.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'Failed to load chat session'
  } finally {
    loading.value = false
  }
}

async function handleSend(content: string, presetId?: string | null) {
  if (!session.value) return

  // Update selected preset ID when user picks one
  if (presetId !== undefined) {
    selectedPresetId.value = presetId
  }

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
    await chatStream.send(props.sessionId, content, selectedPresetId.value ?? undefined)
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
          :preset-id="selectedPresetId"
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
