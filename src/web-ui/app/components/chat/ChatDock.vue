<script setup lang="ts">
import { useDraggable } from '@vueuse/core'
import { useChatDockStore } from '~/stores/chatDock'

const dock = useChatDockStore()
const route = useRoute()

const isHidden = computed(() => route.path.startsWith('/chats'))

const dragHandle = ref<HTMLElement | null>(null)
const popupRef = ref<HTMLElement | null>(null)

const { x, y } = useDraggable(popupRef, {
  handle: dragHandle,
  initialValue: { x: 0, y: 0 },
  preventDefault: true
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

watch(() => dock.isOpen, async (open) => {
  if (open && !dock.activeSessionId && !dock.isCreating) {
    await dock.startNewChat()
  }
})
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
        @click="dock.toggleDock()"
      />

      <div
        v-if="dock.isOpen"
        ref="popupRef"
        class="fixed z-50 w-[380px] h-[560px] max-w-[calc(100vw-1rem)] max-h-[calc(100vh-1rem)] bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg shadow-2xl flex flex-col overflow-hidden"
        :style="{ left: `${x}px`, top: `${y}px` }"
      >
        <div
          ref="dragHandle"
          class="shrink-0 flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-gray-700 cursor-move select-none"
        >
          <h2 class="font-semibold text-sm truncate">
            {{ dock.currentProjectId ? 'Project Chat' : 'Chat' }}
          </h2>
          <div class="flex items-center gap-1">
            <UButton
              icon="i-lucide-plus"
              variant="ghost"
              size="xs"
              title="New chat"
              :disabled="dock.isCreating"
              @click="dock.startNewChat()"
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

        <div class="flex-1 min-h-0 flex flex-col">
          <ChatSessionView
            v-if="dock.activeSessionId"
            :key="dock.activeSessionId"
            :session-id="dock.activeSessionId"
          />
          <div
            v-else
            class="flex-1 flex flex-col items-center justify-center gap-3 p-4"
          >
            <p class="text-sm text-muted text-center">
              Start a conversation.
            </p>
            <UButton
              size="sm"
              icon="i-lucide-plus"
              :disabled="dock.isCreating"
              @click="dock.startNewChat()"
            >
              New chat
            </UButton>
          </div>
        </div>
      </div>
    </template>
  </ClientOnly>
</template>
