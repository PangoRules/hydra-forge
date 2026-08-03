<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { ChatSessionDto, ChatSessionPageDto } from '~/types/chat'

definePageMeta({ middleware: ['auth'] })

const api = useApi()
const toast = useAppToast()

const sessions = ref<ChatSessionDto[]>([])
const loading = ref(true)
const creating = ref(false)
const activeSessionId = ref<string | null>(null)

async function fetchSessions() {
  loading.value = true
  try {
    const { data } = await api.GET<ChatSessionPageDto>(ApiRoutes.Chat.sessions.list())
    sessions.value = data?.items ?? []
  } catch {
    toast.error('Failed to load chat sessions')
  } finally {
    loading.value = false
  }
}

async function createSession() {
  creating.value = true
  try {
    const { data } = await api.POST<ChatSessionDto>(ApiRoutes.Chat.sessions.create(), {
      body: { title: `Test Chat ${new Date().toLocaleString()}` }
    })
    if (data) {
      sessions.value.unshift(data)
      activeSessionId.value = data.id
    }
  } catch {
    toast.error('Failed to create chat session')
  } finally {
    creating.value = false
  }
}

onMounted(fetchSessions)
</script>

<template>
  <div class="p-8 max-w-2xl mx-auto">
    <div class="flex items-center justify-between mb-6">
      <h1 class="text-2xl font-bold">
        Chats
      </h1>
      <UButton
        label="New Chat"
        icon="i-lucide-plus"
        :loading="creating"
        @click="createSession"
      />
    </div>

    <div
      v-if="loading"
      class="text-muted text-sm"
    >
      Loading…
    </div>
    <div
      v-else-if="sessions.length === 0"
      class="text-center text-muted text-sm py-12"
    >
      No chats yet. Start one with "New Chat".
    </div>
    <ul
      v-else
      class="divide-y divide-gray-200 dark:divide-gray-700"
    >
      <li
        v-for="s in sessions"
        :key="s.id"
      >
        <button
          class="w-full text-left py-3 px-2 hover:bg-gray-50 dark:hover:bg-gray-800 rounded-md flex items-center justify-between"
          @click="activeSessionId = s.id"
        >
          <span class="truncate">{{ s.title }}</span>
          <span class="text-xs text-muted shrink-0 ml-2">{{ s.status }}</span>
        </button>
      </li>
    </ul>

    <ChatSessionView
      v-if="activeSessionId"
      :session-id="activeSessionId"
      @close="activeSessionId = null"
    />
  </div>
</template>
