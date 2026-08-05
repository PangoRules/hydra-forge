<script setup lang="ts">
import { ApiError } from '~/lib/api-error'
import { randomId } from '~/lib/id'
import { ApiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import type { ChatMessageDto, ChatSessionDetailDto, ChatSessionDto } from '~/types/chat'
import { MessageRole } from '~/types/chat'

const props = defineProps<{
  sessionId: string
  /** A message to send automatically once connected — used when this session was just
   * created from a compose-first "new chat" box so the first message isn't lost. */
  initialMessage?: string | null
  initialPresetId?: string | null
  initialModelId?: string | null
  initialEffort?: string | null
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

const chatInputRef = ref<{ setContent: (text: string) => void } | null>(null)
const isRollingBack = ref(false)
const showRollbackConfirm = ref(false)
const rollbackTarget = ref<ChatMessageDto | null>(null)
const rollbackDiscardCount = ref(0)
const lastUsedEffort = ref<string | null>(null)

const isEditingTitle = ref(false)
const editedTitle = ref('')
const titleInputRef = ref<HTMLInputElement | null>(null)

const findOpen = ref(false)
const findQuery = ref('')
const findIndex = ref(0)
const findInputRef = ref<HTMLInputElement | null>(null)

const findMatches = computed(() => {
  const q = findQuery.value.trim().toLowerCase()
  if (!q || !session.value) return []
  return session.value.messages.filter(m => m.content.toLowerCase().includes(q))
})

const highlightMessageId = computed(() => findMatches.value[findIndex.value]?.id ?? null)

watch(findQuery, () => {
  findIndex.value = 0
})

// Scrolling to the current match is ChatMessageList's job, not this
// component's — it owns the message-window (only the last 50 messages are
// rendered) and expands that window before scrolling when the match falls
// outside it. This component only tracks which message id is "current".

function toggleFind() {
  findOpen.value = !findOpen.value
  if (findOpen.value) {
    nextTick(() => findInputRef.value?.focus())
  } else {
    findQuery.value = ''
    findIndex.value = 0
  }
}

function closeFind() {
  findOpen.value = false
  findQuery.value = ''
  findIndex.value = 0
}

function nextMatch() {
  if (!findMatches.value.length) return
  findIndex.value = (findIndex.value + 1) % findMatches.value.length
}

function prevMatch() {
  if (!findMatches.value.length) return
  findIndex.value = (findIndex.value - 1 + findMatches.value.length) % findMatches.value.length
}

// True from the moment a reply is successfully triggered (a REST call, see
// useChatStream.send's doc comment) until it's known to be done — via a live
// SignalR StreamDone/StreamError if connected, or the recovery poll below
// either way. Independent of chatStream.isStreaming (which only ever
// becomes true if a live StreamStart actually arrives) so the input stays
// disabled and the poll runs even when no SignalR connection ever comes up.
const awaitingReply = ref(false)
let awaitingBaselineCount = 0

function finishAwaiting() {
  awaitingReply.value = false
  streamingMessageId.value = null
  chatStream.clearStreaming()
}

// Register stream callbacks
chatStream.onStreamStart((messageId) => {
  streamingMessageId.value = messageId
  streamError.value = null
})

chatStream.onStreamDone((_messageId) => {
  finishAwaiting()
  // Refresh session to get the persisted assistant message
  fetchSession()
})

chatStream.onStreamError((_messageId, _code, message) => {
  awaitingReply.value = false
  streamingMessageId.value = null
  streamError.value = message
})

async function fetchSession(silent = false) {
  if (!silent) loading.value = true
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
    if (!silent) error.value = err instanceof ApiError ? err.message : 'Failed to load chat session'
  } finally {
    if (!silent) loading.value = false
  }
}

// Reply generation is now triggered over plain REST and runs as a background
// job entirely decoupled from any client connection (see useChatStream.send's
// doc comment) — so it isn't just mobile reconnects-mid-stream that can miss
// the live SignalR events, a connection that never comes up at all (observed:
// handshakes taking 90-170s+ on some networks) misses ALL of them. Polling
// REST while a reply is in flight is what actually recovers it either way —
// the assistant message is fully persisted server-side by the time it would
// have broadcast StreamDone, regardless of whether anyone was connected to
// hear it. Runs on every reconnect (fast path, in case SignalR does connect)
// and on a fixed interval as the real safety net. Capped so a genuinely
// stuck/failed generation surfaces an error instead of polling forever.
const STALL_POLL_MS = 5000
const STALL_MAX_ATTEMPTS = 60 // 5 minutes

let stallTimer: ReturnType<typeof setInterval> | null = null
let stallAttempts = 0

async function recoverIfReplyLanded() {
  if (!awaitingReply.value) return
  await fetchSession(true)
  if ((session.value?.messages.length ?? 0) > awaitingBaselineCount) {
    finishAwaiting()
    return
  }
  stallAttempts++
  if (stallAttempts >= STALL_MAX_ATTEMPTS) {
    awaitingReply.value = false
    streamError.value = 'The reply is taking unusually long — it may still finish in the background. Try reopening this chat in a bit.'
  }
}

function startStallWatch() {
  stopStallWatch()
  stallAttempts = 0
  stallTimer = setInterval(() => {
    void recoverIfReplyLanded()
  }, STALL_POLL_MS)
}

function stopStallWatch() {
  if (stallTimer) {
    clearInterval(stallTimer)
    stallTimer = null
  }
}

watch(awaitingReply, (waiting) => {
  if (waiting) startStallWatch()
  else stopStallWatch()
})

chatStream.onReconnected(() => {
  void recoverIfReplyLanded()
})

async function handleSend(
  content: string,
  presetId?: string | null,
  preferredModelId?: string | null,
  reasoningEffort?: string | null
) {
  if (!session.value) return

  lastUsedEffort.value = reasoningEffort ?? null
  streamError.value = null

  // Add user message optimistically
  const userMsg: ChatMessageDto = {
    id: randomId(),
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
    const result = await chatStream.send(
      props.sessionId,
      content,
      presetId ?? undefined,
      preferredModelId ?? undefined,
      reasoningEffort ?? undefined
    )
    // undefined only when another send was already in flight (sendingLock) —
    // the optimistic message stays as-is, nothing to reconcile.
    if (result) {
      const idx = session.value.messages.findIndex(m => m.id === userMsg.id)
      if (idx !== -1) session.value.messages[idx] = result.userMessage
      if (result.streamStarted) {
        awaitingBaselineCount = session.value.messages.length
        awaitingReply.value = true
      }
    }
  } catch (err) {
    // Only reached if persisting the message itself failed — a failed/slow
    // reply (streamStarted: false) is not an error, the message was saved.
    const idx = session.value.messages.findIndex(m => m.id === userMsg.id)
    if (idx !== -1) session.value.messages.splice(idx, 1)
    toast.error(err instanceof Error ? err.message : 'Failed to send message')
  }
}

async function handleCancel() {
  await chatStream.cancel(props.sessionId)
  awaitingReply.value = false
}

function startEditTitle() {
  if (!session.value) return
  editedTitle.value = session.value.title
  isEditingTitle.value = true
  nextTick(() => titleInputRef.value?.focus())
}

function cancelTitleEdit() {
  isEditingTitle.value = false
}

async function submitTitleEdit() {
  if (!isEditingTitle.value || !session.value) return
  isEditingTitle.value = false

  const newTitle = editedTitle.value.trim()
  if (!newTitle || newTitle === session.value.title) return

  const previousTitle = session.value.title
  session.value.title = newTitle
  try {
    const { data } = await api.PATCH<ChatSessionDto>(
      ApiRoutes.Chat.sessions.update(props.sessionId),
      {
        body: {
          title: newTitle,
          folderId: session.value.folderId,
          personalityId: session.value.personalityId,
          aiEditMode: session.value.aiEditMode,
          searchAllMyDocs: session.value.searchAllMyDocs
        }
      }
    )
    if (data && session.value) {
      session.value.title = data.title
      emit('sessionRefreshed', session.value.id, data.title, session.value.status)
    }
  } catch (err) {
    if (session.value) session.value.title = previousTitle
    toast.error(err instanceof Error ? err.message : 'Failed to rename chat')
  }
}

function exportChat() {
  if (!session.value) return
  const markdown = session.value.messages
    .map((m) => {
      const heading = m.role === MessageRole.User
        ? '## User'
        : m.role === MessageRole.Assistant
          ? '## Assistant'
          : `## ${m.role}`
      return `${heading}\n\n${m.content}`
    })
    .join('\n\n')

  const safeTitle = session.value.title.replace(/[/\\?%*:|"<>]/g, '-') || 'chat'
  const blob = new Blob([markdown], { type: 'text/markdown' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${safeTitle}.md`
  a.click()
  URL.revokeObjectURL(url)
}

function handleRollbackRequest(message: ChatMessageDto) {
  if (!session.value) return
  const idx = session.value.messages.findIndex(m => m.id === message.id)
  if (idx === -1) return

  rollbackTarget.value = message
  rollbackDiscardCount.value = session.value.messages.length - idx - 1
  showRollbackConfirm.value = true
}

async function confirmRollback() {
  const message = rollbackTarget.value
  if (!message || !session.value) return

  isRollingBack.value = true
  try {
    if (awaitingReply.value) {
      await chatStream.cancel(props.sessionId)
      awaitingReply.value = false
    }

    await api.POST(ApiRoutes.Chat.sessions.rollbackMessage(props.sessionId, message.id))

    const idx = session.value.messages.findIndex(m => m.id === message.id)
    if (idx === -1) return
    session.value.messages.splice(idx)

    if (message.role === MessageRole.User) {
      chatInputRef.value?.setContent(message.content)
    } else {
      const precedingUserMessage = session.value.messages.at(-1)
      if (precedingUserMessage) {
        await chatStream.resend(props.sessionId, precedingUserMessage.id, undefined, undefined, lastUsedEffort.value)
        awaitingBaselineCount = session.value.messages.length
        awaitingReply.value = true
      }
    }
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to roll back message')
  } finally {
    isRollingBack.value = false
    rollbackTarget.value = null
  }
}

onMounted(async () => {
  await fetchSession()

  if (props.initialMessage) {
    // Pre-fill the input so the user can review, edit, then click Send.
    // Per spec: "pre-filled, user edits then sends" — never auto-sent.
    chatInputRef.value?.setContent(props.initialMessage)
    if (props.initialEffort) lastUsedEffort.value = props.initialEffort
  }

  // Connect *after* the message above, not before/alongside it. A SignalR
  // handshake that takes a long time to negotiate (observed 90-170s+ on some
  // networks) opens/holds a connection under HTTP/1.1's small per-origin
  // browser connection cap while it retries — started earlier, it can starve
  // the plain REST calls above of a free connection and leave them stuck
  // pending indefinitely with no error surfaced (the request never even
  // leaves the browser, so there's nothing to catch or toast). Connect is
  // purely for optional live-typing display now — send()/resend() don't
  // need it (see useChatStream.send's doc comment) — so it's safe to let it
  // start last and lose that race.
  void chatStream.connect()
  void chatStream.join(props.sessionId)
})

onUnmounted(() => {
  stopStallWatch()
  chatStream.leave()
  chatStream.disconnect()
})
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <div class="shrink-0 border-b border-gray-200 dark:border-gray-700 px-4 py-3 flex items-center gap-2">
      <input
        v-if="isEditingTitle"
        ref="titleInputRef"
        v-model="editedTitle"
        data-testid="title-input"
        class="flex-1 min-w-0 font-semibold bg-transparent border-b border-primary focus-visible:outline-none"
        @keydown.enter="(e: KeyboardEvent) => (e.target as HTMLInputElement).blur()"
        @keydown.esc="cancelTitleEdit"
        @blur="submitTitleEdit"
      >
      <template v-else>
        <h2 class="font-semibold truncate flex-1 min-w-0">
          {{ session?.title ?? 'Chat' }}
        </h2>
        <UButton
          icon="i-lucide-pencil"
          variant="ghost"
          color="neutral"
          size="xs"
          title="Rename chat"
          :disabled="session?.status !== 'Active'"
          @click="startEditTitle"
        />
        <UButton
          icon="i-lucide-download"
          variant="ghost"
          color="neutral"
          size="xs"
          title="Export chat"
          @click="exportChat"
        />
        <UButton
          icon="i-lucide-search"
          variant="ghost"
          color="neutral"
          size="xs"
          title="Find in conversation"
          @click="toggleFind"
        />
      </template>
    </div>

    <div
      v-if="findOpen"
      class="shrink-0 border-b border-gray-200 dark:border-gray-700 px-4 py-2 flex items-center gap-2"
    >
      <input
        ref="findInputRef"
        v-model="findQuery"
        data-testid="find-input"
        placeholder="Find in conversation"
        class="flex-1 min-w-0 text-sm bg-transparent focus-visible:outline-none"
        @keydown.esc="closeFind"
      >
      <span
        v-if="findQuery"
        data-testid="find-count"
        class="text-xs text-muted shrink-0"
      >
        {{ findMatches.length ? `${findIndex + 1}/${findMatches.length}` : '0/0' }}
      </span>
      <UButton
        icon="i-lucide-chevron-up"
        variant="ghost"
        size="xs"
        title="Previous match"
        :disabled="!findMatches.length"
        @click="prevMatch"
      />
      <UButton
        icon="i-lucide-chevron-down"
        variant="ghost"
        size="xs"
        title="Next match"
        :disabled="!findMatches.length"
        @click="nextMatch"
      />
      <UButton
        icon="i-lucide-x"
        variant="ghost"
        size="xs"
        title="Close find"
        @click="closeFind"
      />
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
        :awaiting-reply="awaitingReply"
        :rollback-disabled="isRollingBack || awaitingReply"
        :highlight-message-id="highlightMessageId"
        @rollback="handleRollbackRequest"
      />

      <ChatInput
        ref="chatInputRef"
        :disabled="awaitingReply || session?.status !== 'Active'"
        @send="handleSend"
        @cancel="handleCancel"
      >
        <template
          v-if="awaitingReply"
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

    <ConfirmDialog
      v-model:open="showRollbackConfirm"
      :title="rollbackTarget?.role === MessageRole.User ? 'Edit and resend' : 'Regenerate reply'"
      :message="
        rollbackDiscardCount > 0
          ? `This will discard ${rollbackDiscardCount} message${rollbackDiscardCount === 1 ? '' : 's'} after this point. This cannot be undone.`
          : (rollbackTarget?.role === MessageRole.User
            ? 'Edit this message and resend?'
            : 'Regenerate this reply?')
      "
      :confirm-text="rollbackTarget?.role === MessageRole.User ? 'Edit' : 'Regenerate'"
      confirm-color="primary"
      @confirm="confirmRollback"
    />
  </div>
</template>
