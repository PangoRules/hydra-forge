<script setup lang="ts">
import { useDraggable } from '@vueuse/core'
import { useChatDockStore } from '~/stores/chatDock'
import ChatSessionView from '~/components/chat/ChatSessionView.vue'
import ChatDockHistory from '~/components/chat/ChatDockHistory.vue'
import ChatInput from '~/components/chat/ChatInput.vue'
import type { ChatSessionDetailDto } from '~/types/chat'
import { ApiRoutes } from '~/lib/routes'

const dock = useChatDockStore()
const route = useRoute()
const api = useApi()

const isHidden = computed(() => route.path.startsWith('/chats'))

const dragHandle = ref<HTMLElement | null>(null)
const popupRef = ref<HTMLElement | null>(null)

const { x, y } = useDraggable(popupRef, {
  handle: dragHandle,
  initialValue: () => (
    dock.position.x !== 0 || dock.position.y !== 0
      ? { ...dock.position }
      : {
          x: typeof window !== 'undefined' ? Math.max(16, window.innerWidth - 400) : 0,
          // Popup height is h-[50vh] (see template) — anchor off half the
          // viewport height, not a stale fixed-pixel assumption from when
          // the popup was h-[560px]. Leaves a 16px margin above the bottom edge.
          y: typeof window !== 'undefined' ? Math.max(16, window.innerHeight * 0.5 - 16) : 0
        }
  ),
  preventDefault: true
})

watch([x, y], ([nx, ny]) => {
  dock.position = { x: nx, y: ny }
})

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && dock.isOpen) {
    dock.closeDock()
  }
}

const isClient = import.meta.client
onMounted(() => {
  if (isClient) window.addEventListener('keydown', onKeydown)
})
onUnmounted(() => {
  if (isClient) window.removeEventListener('keydown', onKeydown)
})

function onSessionRefreshed(_id: string, _title: string, _status: string) {
  // Title/status updated server-side — store refreshes on next open
}

async function sendDraftMessage(
  content: string,
  presetId?: string | null,
  modelId?: string | null,
  reasoningEffort?: string | null
) {
  if (!content.trim() || dock.isCreating) return
  await dock.startNewChat(content, presetId, modelId, reasoningEffort)
}

const isEditingTitle = ref(false)
const editTitle = ref('')
const titleInputRef = ref<HTMLInputElement | null>(null)

async function startEditTitle() {
  if (!dock.activeSessionId) return
  try {
    const { data } = await api.GET<ChatSessionDetailDto>(
      ApiRoutes.Chat.sessions.detail(dock.activeSessionId)
    )
    editTitle.value = data?.title ?? ''
    isEditingTitle.value = true
    nextTick(() => titleInputRef.value?.focus())
  } catch {
    // silently fail — user can retry
  }
}

async function submitTitleEdit() {
  if (!dock.activeSessionId || !editTitle.value.trim()) {
    isEditingTitle.value = false
    return
  }
  const newTitle = editTitle.value.trim()
  try {
    await api.PATCH(ApiRoutes.Chat.sessions.update(dock.activeSessionId), {
      body: { title: newTitle }
    })
    isEditingTitle.value = false
  } catch {
    isEditingTitle.value = false
  }
}
</script>

<template>
  <ClientOnly>
    <template v-if="!isHidden">
      <UButton
        v-if="!dock.isOpen"
        icon="i-lucide-messages-square"
        size="lg"
        color="primary"
        rounded="full"
        class="fixed bottom-4 right-4 z-40 shadow-lg"
        title="Open chat"
        aria-label="Open chat"
        @click="dock.openDock()"
      />

      <div
        v-if="dock.isOpen"
        ref="popupRef"
        class="fixed z-50 w-[380px] h-[50vh] max-w-[calc(100vw-1rem)] max-h-[calc(100vh-1rem)] bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg shadow-2xl flex flex-col overflow-hidden"
        :style="{ left: `${x}px`, top: `${y}px` }"
      >
        <!-- Header -->
        <div
          ref="dragHandle"
          class="shrink-0 flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-gray-700 cursor-move select-none"
        >
          <div class="flex items-center gap-2">
            <UButton
              v-if="dock.mode !== 'history'"
              icon="i-lucide-chevron-left"
              variant="ghost"
              size="xs"
              title="History"
              @click="dock.showHistory()"
            />
            <h2
              v-if="!isEditingTitle"
              class="font-semibold text-sm truncate cursor-pointer hover:text-primary"
              title="Click to rename"
              @click="startEditTitle"
            >
              {{ dock.activeSessionId ? 'Chat' : 'New Chat' }}
            </h2>
            <input
              v-else
              ref="titleInputRef"
              v-model="editTitle"
              class="text-sm font-semibold bg-transparent border-b border-primary outline-none w-full"
              @blur="submitTitleEdit"
              @keydown.enter="submitTitleEdit"
              @keydown.escape="isEditingTitle = false"
            >
          </div>
          <div class="flex items-center gap-1">
            <UButton
              icon="i-lucide-plus"
              variant="ghost"
              size="xs"
              title="New chat"
              :disabled="dock.isCreating"
              @click="dock.newChat()"
            />
            <UButton
              icon="i-lucide-x"
              variant="ghost"
              size="xs"
              title="Close"
              @click="dock.closeDock()"
            />
          </div>
        </div>

        <!-- Body -->
        <div class="flex-1 min-h-0 flex flex-col">
          <!-- Draft mode — same chrome as an active session (ChatInput, model
               picker, presets), just no messages above it yet. Keeps the UI
               identical before/after the first message instead of swapping
               to a different-looking composer. -->
          <div
            v-if="dock.mode === 'draft'"
            class="flex-1 flex flex-col min-h-0"
          >
            <div class="flex-1 flex items-center justify-center p-4">
              <p class="text-sm text-muted text-center">
                Start a new conversation.
              </p>
            </div>
            <ChatInput
              :disabled="dock.isCreating"
              :feature="dock.currentProjectId ? 'ProjectChat' : 'PersonalChat'"
              @send="sendDraftMessage"
            />
          </div>

          <!-- History mode -->
          <ChatDockHistory
            v-if="dock.mode === 'history'"
            @select="dock.loadSession"
            @back="dock.hideHistory"
          />

          <!-- Session mode -->
          <ChatSessionView
            v-if="dock.mode === 'session' && dock.activeSessionId"
            :key="dock.activeSessionId"
            :session-id="dock.activeSessionId"
            :feature="dock.currentProjectId ? 'ProjectChat' : 'PersonalChat'"
            :initial-message="dock.pendingMessage?.content ?? null"
            :auto-send-initial="!!dock.pendingMessage"
            :initial-preset-id="dock.pendingMessage?.presetId ?? null"
            :initial-model-id="dock.pendingMessage?.modelId ?? null"
            :initial-effort="dock.pendingMessage?.reasoningEffort ?? null"
            @session-refreshed="onSessionRefreshed"
            @initial-message-sent="dock.clearPendingMessage()"
          />
        </div>
      </div>
    </template>
  </ClientOnly>
</template>
