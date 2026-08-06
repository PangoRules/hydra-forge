<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import ChatSessionList from '~/components/shared/ChatSessionList.vue'
import { type ChatSessionDto, ChatSessionStatus } from '~/types/chat'
import { useChatSessionList } from '~/composables/useChatSessionList'
import { useChatDockStore } from '~/stores/chatDock'

definePageMeta({ middleware: ['auth'] })

const TITLE_MAX_LENGTH = 60

const api = useApi()
const toast = useAppToast()

const route = useRoute()

const dock = useChatDockStore()
const { sessions, loading, hasMore, statusFilter, loadMore, refresh, patchSession, prependSession, removeSession } = useChatSessionList()
const starting = ref(false)
const activeSessionId = ref<string | null>(null)

const statusFilterItems = [
  { label: 'Active & Closed', value: 'ActiveAndClosed' },
  { label: 'Active', value: 'Active' },
  { label: 'Closed', value: 'Closed' },
  { label: 'Archived', value: 'Archived' }
]

watch(statusFilter, () => {
  refresh()
})

onMounted(() => {
  if (dock.activeSessionId) {
    activeSessionId.value = dock.activeSessionId
  } else {
    const saved = localStorage.getItem('hydraforge:chat:activeSessionId')
    if (saved) activeSessionId.value = saved
  }
})

// Arriving via the top-nav "+ New Chat" button (?compose=1) means "go straight to
// composing" — start collapsed. Arriving via the Chats menu item (no query) means
// "browse my chats" — start expanded. Either way the user can toggle it themselves.
const sidebarOpen = ref(route.query.compose !== '1')

// The "+ New Chat" nav link always points at this same route with a ?compose=1
// query, so clicking it while already on /chats is a same-route SPA navigation —
// Vue Router updates route.query but does not remount this page, so the sidebarOpen
// ref above (set once at mount) would otherwise never react. Watch the query
// directly so re-clicking "New Chat" from within the page still lands on a fresh
// draft with the sidebar closed, every time.
watch(() => route.query.compose, (compose) => {
  if (compose === '1') startCompose()
})

function toggleSidebar() {
  sidebarOpen.value = !sidebarOpen.value
}

function selectSession(id: string) {
  activeSessionId.value = id
}

// Starting a new chat — whether from the top-nav link or the sidebar's own "+" —
// always closes the sidebar so the user lands on a clean compose view instead of
// staring at the history list they just asked to leave.
function startCompose() {
  activeSessionId.value = null
  pendingMessage.value = null
  sidebarOpen.value = false
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
      // A new session always sorts newest-first — prepend locally instead of
      // refresh()ing, which would collapse back to page 1 and "disappear" any
      // older pages the user had already scrolled into.
      prependSession(data)
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
    removeSession(id)
  } catch {
    toast.error('Failed to archive chat')
  } finally {
    archiveTargetId.value = null
  }
}

async function reopenSession(id: string) {
  try {
    await api.POST(ApiRoutes.Chat.sessions.reopen(id))
    patchSession(id, { status: ChatSessionStatus.Active, archivedAt: null })
  } catch {
    toast.error('Failed to reopen chat')
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
      <div class="w-72 flex-1 min-h-0 flex flex-col">
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

        <div class="shrink-0 px-4 pb-3">
          <USelect
            v-model="statusFilter"
            :items="statusFilterItems"
            size="xs"
          />
        </div>

        <ChatSessionList
          :sessions="sessions"
          :loading="loading"
          :has-more="hasMore"
          :active-session-id="activeSessionId"
          @select="selectSession"
          @load-more="loadMore"
        >
          <template #empty>
            No chats yet — type below to start one.
          </template>
          <template #item="{ session, active }">
            <li class="group relative">
              <button
                class="w-full text-left px-4 py-3 pr-9 border-b border-gray-100 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800"
                :class="active ? 'bg-gray-100 dark:bg-gray-800' : ''"
                @click="selectSession(session.id)"
              >
                <p class="truncate text-sm font-medium">
                  {{ session.title }}
                </p>
                <!-- "Active" is every session's default state pre-close — showing it for
                     everything is just noise. Only surface the badge once it's meaningful. -->
                <p
                  v-if="session.status !== 'Active'"
                  class="text-xs text-muted"
                >
                  {{ session.status }}
                </p>
              </button>
              <UButton
                icon="i-lucide-archive"
                variant="ghost"
                color="neutral"
                size="xs"
                title="Archive chat"
                class="absolute right-1 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity"
                @click.stop="requestArchive(session.id)"
              />
              <UButton
                v-if="session.status !== 'Active' || session.archivedAt"
                icon="i-lucide-folder-open"
                variant="ghost"
                color="neutral"
                size="xs"
                title="Reopen chat"
                class="absolute right-9 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity"
                @click.stop="reopenSession(session.id)"
              />
            </li>
          </template>
        </ChatSessionList>
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
