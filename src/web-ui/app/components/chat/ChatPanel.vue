<script setup lang="ts">
import { ApiError } from '~/lib/api-error'
import { ApiRoutes } from '~/lib/routes'
import type { ChatSessionDto } from '~/types/chat'

const TITLE_MAX_LENGTH = 60

const props = defineProps<{
  projectId: string
  cardId?: string
  cardNumber?: number | string
  cardTitle?: string
}>()

const toast = useAppToast()
const api = useApi()

const isOpen = ref(false)
const activeSessionId = ref<string | null>(null)
const newChatContent = ref('')
const isCreating = ref(false)
const isDesktop = ref(false)

function updateDesktop() {
  if (import.meta.client) {
    isDesktop.value = window.innerWidth >= 768
  }
}

onMounted(() => {
  updateDesktop()
  window.addEventListener('resize', updateDesktop)
  if (props.cardId) {
    openPanel()
  }
})

onUnmounted(() => {
  window.removeEventListener('resize', updateDesktop)
})

function deriveTitle(content: string): string {
  const firstLine = content.trim().split('\n')[0] ?? content.trim()
  return firstLine.length > TITLE_MAX_LENGTH
    ? `${firstLine.slice(0, TITLE_MAX_LENGTH)}…`
    : firstLine
}

async function createSession(cardId: string | undefined, prefillMessage: string | null) {
  const body: Record<string, unknown> = {
    title: prefillMessage ? deriveTitle(prefillMessage) : 'New chat'
  }
  if (props.projectId) body.projectId = props.projectId
  if (cardId) body.openCardId = cardId

  try {
    const { data } = await api.POST<ChatSessionDto>(ApiRoutes.Chat.sessions.create(), {
      body: body as Record<string, string>
    })
    if (data) {
      activeSessionId.value = data.id
    }
  } catch (err) {
    toast.error(err instanceof ApiError ? err.message : 'Failed to create chat session')
  }
}

function startNewChat() {
  isCreating.value = true
  const content = `Card #${props.cardNumber} ${props.cardTitle} opened — what are we doing?`
  createSession(props.cardId, content).finally(() => {
    isCreating.value = false
  })
}

function handleNewChatTextareaKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
    e.preventDefault()
    void submitNewChatInline()
  }
}

async function submitNewChatInline() {
  const content = newChatContent.value.trim()
  if (!content || isCreating.value) return
  isCreating.value = true
  // Pass cardId so F6 implicit-close fires on the card-bound session,
  // not just the project-scoped one this call creates.
  await createSession(props.cardId, content)
  newChatContent.value = ''
  isCreating.value = false
}

function openPanel() {
  isOpen.value = true
  if (!activeSessionId.value) {
    startNewChat()
  }
}

function closePanel() {
  isOpen.value = false
}

defineExpose({ openPanel, closePanel })
</script>

<template>
  <Teleport to="body">
    <!-- Mobile slide-over backdrop -->
    <Transition name="fade">
      <div
        v-if="isOpen && !isDesktop"
        class="fixed inset-0 z-40 bg-black/30"
        @click="closePanel"
      />
    </Transition>

    <!-- Chat panel -->
    <Transition name="slide">
      <div
        v-if="isOpen"
        class="fixed top-0 right-0 h-full z-50 flex flex-col bg-white dark:bg-gray-900 border-l border-gray-200 dark:border-gray-700 shadow-xl"
        :class="isDesktop ? 'md:static md:shadow-none md:border-l-0 md:border-t md:h-full md:z-auto md:w-96 md:flex-shrink-0' : 'w-80'"
      >
        <!-- Header -->
        <div class="shrink-0 flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-gray-700">
          <h2 class="font-semibold truncate text-sm">
            {{ cardId ? `Card #${cardNumber}` : 'Project Chat' }}
          </h2>
          <div class="flex items-center gap-1">
            <UButton
              icon="i-lucide-plus"
              variant="ghost"
              size="xs"
              title="New chat"
              :disabled="isCreating"
              @click="startNewChat"
            />
            <UButton
              icon="i-lucide-x"
              variant="ghost"
              size="xs"
              title="Close panel"
              @click="closePanel"
            />
          </div>
        </div>

        <!-- Body -->
        <div class="flex-1 min-h-0 flex flex-col">
          <!-- Active chat -->
          <ChatSessionView
            v-if="activeSessionId"
            :key="activeSessionId"
            :session-id="activeSessionId"
          />

          <!-- Empty state when no session -->
          <div
            v-else
            class="flex-1 min-h-0 flex flex-col items-center justify-center px-4 gap-3"
          >
            <p class="text-sm text-muted text-center">
              Start a conversation about this {{ cardId ? 'card' : 'project' }}.
            </p>
            <UButton
              size="sm"
              icon="i-lucide-plus"
              :disabled="isCreating"
              @click="startNewChat"
            >
              New chat
            </UButton>
          </div>

          <!-- Inline new-chat form shown above messages when session is active -->
          <div
            v-if="activeSessionId"
            class="shrink-0 border-t border-gray-200 dark:border-gray-700 p-3 flex gap-2"
          >
            <textarea
              v-model="newChatContent"
              rows="2"
              placeholder="Ask about this card…"
              class="flex-1 min-w-0 resize-none rounded-md border border-gray-200 dark:border-gray-700 bg-transparent px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
              @keydown="handleNewChatTextareaKeydown"
            />
            <UButton
              icon="i-lucide-send"
              size="sm"
              :disabled="!newChatContent.trim() || isCreating"
              @click="submitNewChatInline"
            />
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.slide-enter-active,
.slide-leave-active {
  transition: transform 0.2s ease;
}
.slide-enter-from,
.slide-leave-to {
  transform: translateX(100%);
}

@media (min-width: 768px) {
  .slide-enter-from,
  .slide-leave-to {
    transform: translateX(0);
  }
}
</style>
