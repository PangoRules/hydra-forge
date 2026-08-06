<script setup lang="ts">
import { ApiError } from '~/lib/api-error'
import { randomId } from '~/lib/id'
import { ApiRoutes, UiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import ChatSessionHeader from '~/components/chat/ChatSessionHeader.vue'
import ChatDocAttach from '~/components/chat/ChatDocAttach.vue'
import type { AiEditMode, ChatMessageDto, ChatSessionDetailDto, ChatSessionDto } from '~/types/chat'
import { ChatSessionStatus, MessageRole } from '~/types/chat'

const props = withDefaults(
  defineProps<{
    sessionId: string
    /** A message to pre-fill into the input on mount — user edits then sends manually. */
    initialMessage?: string | null
    /** When true, the initial message is sent automatically (compose-first flow). */
    autoSendInitial?: boolean
    initialPresetId?: string | null
    initialModelId?: string | null
    initialEffort?: string | null
    feature?: string
    compact?: boolean
  }>(),
  {
    initialMessage: null,
    autoSendInitial: false,
    initialPresetId: null,
    initialModelId: null,
    initialEffort: null,
    feature: 'PersonalChat',
    compact: false
  }
)

const emit = defineEmits<{
  initialMessageSent: []
  /** Fires after every successful fetch — lets the sidebar list stay in sync with
   * this session's title (e.g. the AI-generated title landing after the first
   * exchange) without polling or a shared store. */
  sessionRefreshed: [id: string, title: string, status: string]
  dismiss: []
}>()

const toast = useAppToast()
const api = useApi()
const authStore = useAuthStore()

const resolvedInitialModelId = computed(() => props.initialModelId ?? session.value?.preferredModelConfigId ?? null)
const resolvedInitialEffort = computed(() => props.initialEffort ?? session.value?.preferredEffort ?? null)

const session = ref<ChatSessionDetailDto | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

const isOwner = computed(() =>
  session.value?.ownerId != null && authStore.user?.userId != null
    ? session.value.ownerId === authStore.user!.userId
    : false
)
const isActive = computed(() => session.value?.status === 'Active')

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

// Title editing
const isEditingTitle = ref(false)
const editedTitle = ref('')
const titleInputRef = ref<HTMLInputElement | null>(null)

// Find bar
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

// Title generation runs as its own decoupled background job (see
// ChatTitleGenerationJob) so it doesn't block the reply's own StreamDone —
// without this, a connected client had no way to learn the title changed
// short of a manual refresh. Patch in place; no refetch needed, the payload
// already has everything.
chatStream.onSessionUpdated((updatedSessionId, title, status) => {
  if (updatedSessionId !== props.sessionId || !session.value) return
  session.value.title = title
  session.value.status = status as ChatSessionDetailDto['status']
  emit('sessionRefreshed', session.value.id, title, status)
})

async function fetchSession(silent = false) {
  if (!silent) loading.value = true
  error.value = null
  try {
    const { data } = await api.GET<ChatSessionDetailDto>(
      ApiRoutes.Chat.sessions.detail(props.sessionId)
    )
    session.value = data as ChatSessionDetailDto
    // Hide System-role messages (e.g. the Hydra identity prompt) from the UI —
    // they flow to the LLM via history but aren't for the user to read.
    session.value.messages = session.value.messages.filter(m => m.role !== 'System')
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

// Shared PATCH helper — keeps title, folderId, personalityId, aiEditMode, searchAllMyDocs
// in sync with whatever the caller wants to change.
async function updateSessionSettings(overrides: {
  title?: string
  folderId?: string | null
  personalityId?: string | null
  aiEditMode?: AiEditMode
  searchAllMyDocs?: boolean
}) {
  if (!session.value) return
  try {
    const { data } = await api.PATCH<ChatSessionDto>(
      ApiRoutes.Chat.sessions.update(props.sessionId),
      {
        body: {
          title: session.value.title,
          folderId: session.value.folderId,
          personalityId: session.value.personalityId,
          aiEditMode: session.value.aiEditMode,
          searchAllMyDocs: session.value.searchAllMyDocs,
          ...overrides
        }
      }
    )
    if (data && session.value) {
      // Sync back any server-authored fields
      session.value.title = data.title
      session.value.personalityId = data.personalityId
      session.value.aiEditMode = data.aiEditMode
      session.value.searchAllMyDocs = data.searchAllMyDocs
      emit('sessionRefreshed', session.value.id, data.title, session.value.status)
    }
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to update session')
  }
}

async function handleEditPersonality(personalityId: string | null) {
  if (!session.value) return
  await updateSessionSettings({ personalityId })
}

async function handleEditMode(mode: AiEditMode) {
  if (!session.value) return
  await updateSessionSettings({ aiEditMode: mode })
}

async function handleCloseSession() {
  if (!session.value) return
  try {
    await api.POST(ApiRoutes.Chat.sessions.close(props.sessionId))
    session.value.status = ChatSessionStatus.Closed
    emit('sessionRefreshed', session.value.id, session.value.title, session.value.status)
    toast.success('Chat closed')
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to close chat')
  }
}

async function handleArchiveSession() {
  if (!session.value) return
  try {
    await api.DELETE(ApiRoutes.Chat.sessions.archive(props.sessionId))
    // ArchivedAt is a separate axis from Status (Active/Closed) — archiving
    // never changes Status. Setting status here would fake a third status
    // value the backend enum doesn't have (see ChatSessionStatus.cs: only
    // Active/Closed exist; "Archived" on the frontend enum is a display-only
    // convenience, never a real Status the server sends).
    session.value.archivedAt = new Date().toISOString()
    emit('sessionRefreshed', session.value.id, session.value.title, session.value.status)
    toast.success('Chat archived')
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to archive chat')
  }
}

function handleDismiss() {
  emit('dismiss')
}

async function handleReopen() {
  if (!session.value) return
  try {
    await api.POST(ApiRoutes.Chat.sessions.reopen(props.sessionId))
    session.value.status = ChatSessionStatus.Active
    session.value.archivedAt = null
    emit('sessionRefreshed', session.value.id, session.value.title, session.value.status)
    toast.success('Chat reopened')
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to reopen chat')
  }
}

const router = useRouter()

async function handleFork() {
  if (!session.value) return
  try {
    const { data } = await api.POST<ChatSessionDto>(
      ApiRoutes.Chat.sessions.create(),
      {
        body: {
          title: `${session.value.title} (fork)`,
          projectId: session.value.projectId,
          forkedFromSessionId: session.value.id
        }
      }
    )
    if (data) {
      toast.success('Chat forked — navigating...')
      await router.push(UiRoutes.ChatSessions.Detail(data.id))
    }
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to fork chat')
  }
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

// Title editing
function startEditTitle() {
  if (!session.value || !isActive.value) return
  editedTitle.value = session.value.title
  isEditingTitle.value = true
  nextTick(() => titleInputRef.value?.focus())
}

function cancelTitleEdit() {
  isEditingTitle.value = false
  editedTitle.value = ''
}

async function submitTitleEdit() {
  if (!session.value || !isActive.value) return
  const trimmed = editedTitle.value.trim()
  if (!trimmed || trimmed === session.value.title) {
    cancelTitleEdit()
    return
  }
  await updateSessionSettings({ title: trimmed })
  cancelTitleEdit()
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

onMounted(async () => {
  await fetchSession()

  if (props.initialMessage) {
    if (props.autoSendInitial) {
      // Compose-first flow from chats/index.vue: user already typed the message,
      // expect it to be sent automatically.
      await handleSend(
        props.initialMessage,
        props.initialPresetId,
        props.initialModelId,
        props.initialEffort
      )
      emit('initialMessageSent')
    } else {
      // ChatPanel flow: pre-fill input so the user reviews, edits, then sends.
      chatInputRef.value?.setContent(props.initialMessage)
      if (props.initialEffort) lastUsedEffort.value = props.initialEffort
    }
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

// In compact mode ChatSessionView renders no header of its own (see the
// v-if="session && !compact" on <ChatSessionHeader> below) — ChatDock owns
// the single dock header row and drives it through this exposed surface via
// a ref to the mounted ChatSessionView instance, instead of duplicating a
// second header/title-edit implementation in ChatDock.vue.
defineExpose({
  session,
  isOwner,
  handleEditPersonality,
  handleEditMode,
  handleDismiss,
  handleCloseSession,
  handleArchiveSession,
  handleReopen,
  handleFork,
  startEditTitle,
  exportChat,
  toggleFind
})
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <ChatSessionHeader
      v-if="session && !compact"
      :session="session"
      :is-owner="isOwner"
      :compact="compact"
      @edit-personality="handleEditPersonality"
      @edit-mode="handleEditMode"
      @close-session="handleCloseSession"
      @archive-session="handleArchiveSession"
      @reopen-session="handleReopen"
      @dismiss="handleDismiss"
      @fork="handleFork"
      @start-edit-title="startEditTitle"
      @export-chat="exportChat"
      @toggle-find="toggleFind"
    />

    <!-- Inline title edit input (rendered alongside/after the header) -->
    <div
      v-if="isEditingTitle && session"
      class="shrink-0 border-b border-gray-200 dark:border-gray-700 px-4 py-3 flex items-center gap-2"
    >
      <input
        ref="titleInputRef"
        v-model="editedTitle"
        type="text"
        data-testid="title-input"
        class="flex-1 min-w-0 text-sm border border-gray-300 dark:border-gray-600 rounded px-2 py-1 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
        @keydown.enter.prevent="submitTitleEdit"
        @keydown.escape.prevent="cancelTitleEdit"
        @blur="submitTitleEdit"
      >
    </div>

    <!-- Find bar -->
    <div
      v-if="findOpen && session"
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
    <template v-else-if="session">
      <ChatMessageList
        :messages="session.messages"
        :streaming-message="chatStream.streamingMessage.value"
        :stream-error="streamError"
        :awaiting-reply="awaitingReply"
        :rollback-disabled="isRollingBack || awaitingReply"
        :highlight-message-id="highlightMessageId"
        :find-query="findQuery"
        @rollback="handleRollbackRequest"
      />

      <ChatDocAttach
        v-if="isOwner && isActive"
        :session-id="sessionId"
        class="shrink-0 border-t border-gray-200 dark:border-gray-700 px-4 py-2"
        @attached="fetchSession(true)"
      />

      <ChatInput
        ref="chatInputRef"
        :disabled="awaitingReply || !isActive"
        :feature="feature"
        :initial-model-id="resolvedInitialModelId"
        :initial-effort="resolvedInitialEffort"
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
