<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import type { ChatSessionDto } from '~/types/chat'
import { useChatSessionList } from '~/composables/useChatSessionList'
import { useChatDockStore } from '~/stores/chatDock'

definePageMeta({ middleware: ['auth'] })

const TITLE_MAX_LENGTH = 60

const api = useApi()
const toast = useAppToast()

const route = useRoute()

const dock = useChatDockStore()
const { sessions, loading, hasMore, loadMore, refresh, patchSession } = useChatSessionList()
const starting = ref(false)
const activeSessionId = ref<string | null>(null)

const sentinel = ref<HTMLElement | null>(null)
const observer = ref<IntersectionObserver | null>(null)

onMounted(() => {
  if (dock.activeSessionId) {
    activeSessionId.value = dock.activeSessionId
  } else {
    const saved = localStorage.getItem('hydraforge:chat:activeSessionId')
    if (saved) activeSessionId.value = saved
  }

  observer.value = new IntersectionObserver(
    ([entry]) => {
      if (entry?.isIntersecting && !loading.value && hasMore.value) {
        loadMore()
      }
    },
    { rootMargin: '100px' }
  )
  if (sentinel.value) observer.value.observe(sentinel.value)
})

onUnmounted(() => {
  observer.value?.disconnect()
})

// Arriving via the top-nav "+ New Chat" button (?compose=1) means "go straight to
// composing" — start collapsed. Arriving via the Chats menu item (no query) means
// "browse my chats" — start expanded. Either way the user can toggle it themselves.
const sidebarOpen = ref(route.query.compose !== '1')

function toggleSidebar() {
  sidebarOpen.value = !sidebarOpen.value
}

// Selecting a chat or starting a new one never touches sidebarOpen — only the
// toggle button and the initial ?compose=1 landing state do. The user's own
// open/closed choice sticks until they change it themselves.
function selectSession(id: string) {
  activeSessionId.value = id
}

function startCompose() {
  activeSessionId.value = null
}

// Tracks the just-created session's first message for the auto-send flow.
// Set before activeSessionId changes; read in the template on the same tick.
const pendingMessage = ref<{
  content: string
  presetId: string | null
  modelId: string | null
  reasoningEffort: string | null
} | null>(null)

function deriveTitle(content: string): string {
  const firstLine = content.trim().split('\n')[0] ?? content.trim()
  return firstLine.length > TITLE_MAX_LENGTH
    ? `${firstLine.slice(0, TITLE_MAX_LENGTH)}…`
    : firstLine
}

async function startNewChat(content: string, presetId?: string | null, modelId?: string | null, reasoningEffort?: string | null) {
  starting.value = true
  try {
    const { data } = await api.POST<ChatSessionDto>(ApiRoutes.Chat.sessions.create(), {
      body: { title: deriveTitle(content) }
    })
    if (data) {
      pendingMessage.value = {
        content,
        presetId: presetId ?? null,
        modelId: modelId ?? null,
        reasoningEffort: reasoningEffort ?? null
      }
      activeSessionId.value = data.id
      await refresh()
    }
  } catch {
    toast.error('Failed to create chat session')
  } finally {
    starting.value = false
  }
}

function syncSession(id: string, title: string, status: string) {
  // Patch the item in place — a full refresh() would drop the user back to
  // page 1 and lose their scroll position if they've paged into older history.
  patchSession(id, { title, status: status as ChatSessionDto['status'] })
}

const archiveTargetId = ref<string | null>(null)

function requestArchive(id: string) {
  archiveTargetId.value = id
}

async function confirmArchive() {
  const id = archiveTargetId.value
  if (!id) return
  try {
    await api.DELETE(ApiRoutes.Chat.sessions.archive(id))
    if (activeSessionId.value === id) activeSessionId.value = null
    await refresh()
  } catch {
    toast.error('Failed to archive chat')
  } finally {
    archiveTargetId.value = null
  }
}
</script>

<template>
  <div class="flex-1 flex min-h-0 relative">
    <!-- Session list -->
    <div
      class="shrink-0 border-r border-gray-200 dark:border-gray-700 flex flex-col min-h-0 overflow-hidden transition-[width] duration-200"
      :class="sidebarOpen ? 'w-72' : 'w-0 border-r-0'"
    >
      <div class="w-72 h-full flex flex-col min-h-0">
        <div class="shrink-0 flex items-center justify-between p-4">
          <h1 class="text-lg font-bold">
            Chats
          </h1>
          <UButton
            icon="i-lucide-plus"
            size="sm"
            title="Start a new chat"
            @click="startCompose"
          />
        </div>

        <div
          v-if="loading"
          class="text-muted text-sm px-4"
        >
          Loading…
        </div>
        <div
          v-else-if="sessions.length === 0"
          class="text-center text-muted text-sm px-4 py-8"
        >
          No chats yet — type below to start one.
        </div>
        <ul
          v-else
          class="flex-1 min-h-0 overflow-y-auto"
        >
          <li
            v-for="s in sessions"
            :key="s.id"
            class="group relative"
          >
            <button
              class="w-full text-left px-4 py-3 pr-9 border-b border-gray-100 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800"
              :class="s.id === activeSessionId ? 'bg-gray-100 dark:bg-gray-800' : ''"
              @click="selectSession(s.id)"
            >
              <p class="truncate text-sm font-medium">
                {{ s.title }}
              </p>
              <!-- "Active" is every session's default state pre-close — showing it for
                   everything is just noise. Only surface the badge once it's meaningful. -->
              <p
                v-if="s.status !== 'Active'"
                class="text-xs text-muted"
              >
                {{ s.status }}
              </p>
            </button>
            <UButton
              icon="i-lucide-archive"
              variant="ghost"
              color="neutral"
              size="xs"
              title="Archive chat"
              class="absolute right-1 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity"
              @click.stop="requestArchive(s.id)"
            />
          </li>
          <div
            v-if="hasMore"
            ref="sentinel"
            class="h-4 shrink-0"
          />
        </ul>
      </div>
    </div>

    <UButton
      :icon="sidebarOpen ? 'i-lucide-chevron-left' : 'i-lucide-chevron-right'"
      size="xs"
      color="neutral"
      variant="solid"
      :title="sidebarOpen ? 'Hide chat list' : 'Show chat list'"
      class="absolute top-1/2 -translate-y-1/2 z-10 rounded-full shadow transition-[left] duration-200"
      :style="{ left: sidebarOpen ? '272px' : '0px' }"
      @click="toggleSidebar"
    />

    <!-- Active chat -->
    <ChatSessionView
      v-if="activeSessionId"
      :key="activeSessionId"
      :session-id="activeSessionId"
      :initial-message="pendingMessage?.content ?? null"
      :auto-send-initial="!!pendingMessage"
      :initial-preset-id="pendingMessage?.presetId ?? null"
      :initial-model-id="pendingMessage?.modelId ?? null"
      :initial-effort="pendingMessage?.reasoningEffort ?? null"
      @initial-message-sent="pendingMessage = null"
      @session-refreshed="syncSession"
    />
    <div
      v-else
      class="flex-1 flex flex-col min-h-0"
    >
      <div class="flex-1 flex items-center justify-center text-muted text-sm">
        Type a message below to start a new chat.
      </div>
      <ChatInput
        :disabled="starting"
        @send="startNewChat"
      />
    </div>

    <ConfirmDialog
      :open="archiveTargetId !== null"
      title="Archive chat"
      message="This chat will be archived and removed from your list. This can't be undone from here."
      confirm-text="Archive"
      confirm-color="error"
      @update:open="(v: boolean) => { if (!v) archiveTargetId = null }"
      @confirm="confirmArchive"
    />
  </div>
</template>
