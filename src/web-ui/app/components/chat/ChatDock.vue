<script setup lang="ts">
import { useDraggable } from '@vueuse/core'
import { useChatDockStore } from '~/stores/chatDock'
import ChatSessionView from '~/components/chat/ChatSessionView.vue'
import ChatSessionHeader from '~/components/chat/ChatSessionHeader.vue'
import ChatDockHistory from '~/components/chat/ChatDockHistory.vue'
import ChatInput from '~/components/chat/ChatInput.vue'

const dock = useChatDockStore()
const route = useRoute()

const isHidden = computed(() => route.path.startsWith('/chats'))

// Single source of truth for the popup's width — used both for the inline
// style below and for anchoring the initial x position, so the two can never
// drift out of sync again (this drifted once already: the width moved from
// 380px to 440px but the initial-x margin stayed hardcoded at the old value,
// pushing the popup partly off the right edge of the screen on first open).
const DOCK_WIDTH_PX = 480

const dragHandle = ref<HTMLElement | null>(null)
const popupRef = ref<HTMLElement | null>(null)
const sessionViewRef = ref<InstanceType<typeof ChatSessionView> | null>(null)

const { x, y } = useDraggable(popupRef, {
  handle: dragHandle,
  initialValue: () => (
    dock.position.x !== 0 || dock.position.y !== 0
      ? { ...dock.position }
      : {
          x: typeof window !== 'undefined' ? Math.max(16, window.innerWidth - DOCK_WIDTH_PX - 16) : 0,
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

// Full-height mode forces top:1rem, bypassing the normal top:${y}px binding —
// remember where the popup was right before entering it and restore that
// exact x/y on the way back out, rather than assuming useDraggable's x/y refs
// are untouched by whatever happened on-screen while full-height was active.
const preFullHeightPosition = ref<{ x: number, y: number } | null>(null)

function handleToggleFullHeight() {
  if (!dock.isFullHeight) {
    preFullHeightPosition.value = { x: x.value, y: y.value }
    dock.toggleFullHeight()
  } else {
    dock.toggleFullHeight()
    if (preFullHeightPosition.value) {
      x.value = preFullHeightPosition.value.x
      y.value = preFullHeightPosition.value.y
      preFullHeightPosition.value = null
    }
  }
}

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

async function sendDraftMessage(
  content: string,
  presetId?: string | null,
  modelId?: string | null,
  reasoningEffort?: string | null,
  personalityId?: string | null
) {
  if (!content.trim() || dock.isCreating) return
  await dock.startNewChat(content, presetId, modelId, reasoningEffort, personalityId)
}

// Archive fired from inside the open session view — the API call already
// happened in ChatSessionView.handleArchiveSession(). The archived session
// must not stay open in the dock: drop back to draft mode (this also clears
// the localStorage resume key via the store's activeSessionId watcher). The
// history list needs no patch here — it remounts with a fresh fetch every
// time the user opens it (v-if mode branches), so it can never be stale.
function onArchiveFromSession() {
  dock.newChat()
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
        class="fixed z-50 max-w-[calc(100vw-1rem)] max-h-[calc(100vh-1rem)] bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg shadow-2xl flex flex-col overflow-hidden"
        :class="dock.isFullHeight ? 'h-[calc(100vh-2rem)]' : 'h-[50vh]'"
        :style="{
          width: `${DOCK_WIDTH_PX}px`,
          left: `${x}px`,
          top: dock.isFullHeight ? '1rem' : `${y}px`
        }"
      >
        <!-- Header — single row, drag handle wraps whichever header is showing.
             Session mode drives the real ChatSessionHeader through a ref to the
             mounted ChatSessionView; draft/history modes (no session yet) get a
             lightweight placeholder row with the same back/new-chat/full-height/
             dismiss controls. -->
        <div
          ref="dragHandle"
          data-testid="dock-header-row"
          class="shrink-0 cursor-move select-none"
        >
          <ChatSessionHeader
            v-if="dock.mode === 'session' && sessionViewRef?.session"
            :session="sessionViewRef.session"
            :is-owner="sessionViewRef.isOwner"
            compact
            show-back-button
            show-new-chat-button
            show-full-height-toggle
            :is-full-height="dock.isFullHeight"
            @back="dock.showHistory()"
            @new-chat="dock.newChat()"
            @dismiss="dock.closeDock()"
            @toggle-full-height="handleToggleFullHeight"
            @close-session="sessionViewRef.handleCloseSession()"
            @archive-session="sessionViewRef.handleArchiveSession()"
            @reopen-session="sessionViewRef.handleReopen()"
            @edit-personality="sessionViewRef.handleEditPersonality($event)"
            @edit-mode="sessionViewRef.handleEditMode($event)"
            @start-edit-title="sessionViewRef.startEditTitle()"
            @export-chat="sessionViewRef.exportChat()"
            @toggle-find="sessionViewRef.toggleFind()"
          />
          <div
            v-else
            class="flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-gray-700"
          >
            <div class="flex items-center gap-2 min-w-0 flex-1">
              <UButton
                v-if="dock.mode !== 'history'"
                icon="i-lucide-chevron-left"
                variant="ghost"
                size="xs"
                title="History"
                @click="dock.showHistory()"
              />
              <span class="font-semibold text-sm truncate">
                {{ dock.mode === 'history' ? 'History' : 'New Chat' }}
              </span>
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
                :icon="dock.isFullHeight ? 'i-lucide-minimize-2' : 'i-lucide-maximize-2'"
                variant="ghost"
                size="xs"
                :title="dock.isFullHeight ? 'Exit full height' : 'Full height'"
                @click="handleToggleFullHeight"
              />
              <UButton
                icon="i-lucide-x"
                variant="ghost"
                size="xs"
                title="Dismiss"
                @click="dock.closeDock()"
              />
            </div>
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
            ref="sessionViewRef"
            :key="dock.activeSessionId"
            :session-id="dock.activeSessionId"
            :feature="dock.currentProjectId ? 'ProjectChat' : 'PersonalChat'"
            :initial-message="dock.pendingMessage?.content ?? null"
            :auto-send-initial="!!dock.pendingMessage"
            :initial-preset-id="dock.pendingMessage?.presetId ?? null"
            :initial-model-id="dock.pendingMessage?.modelId ?? null"
            :initial-effort="dock.pendingMessage?.reasoningEffort ?? null"
            compact
            @initial-message-sent="dock.clearPendingMessage()"
            @archive-session="onArchiveFromSession"
          />
        </div>
      </div>
    </template>
  </ClientOnly>
</template>
