<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import type { ChatSessionDto, CardChatLinkDto, ChatSessionPageDto } from '~/types/chat'
import { ApiRoutes } from '~/lib/routes'
import { ApiError } from '~/lib/api-error'
import { formatDateOnly } from '~/lib/date'

const props = defineProps<{ projectId: string }>()

const api = useApi()
const toast = useAppToast()
const chatDock = useChatDockStore()

const sessions = ref<ChatSessionDto[]>([])
const loading = ref(true)
const filter = ref<'all' | 'project' | 'card'>('all')
// Set of session IDs that have at least one CardChatLink in this project.
const cardLinkedSessionIds = ref<Set<string>>(new Set())

async function fetchSessions() {
  loading.value = true
  try {
    const url = ApiRoutes.Chat.sessions.list(undefined, props.projectId, undefined, undefined, 50)
    const { data } = await api.GET<ChatSessionPageDto>(url)
    sessions.value = data?.items ?? []

    // Build the set of sessions that have a CardChatLink by fetching links
    // for every unique card referenced in openCardId (parallel, 1 req per card).
    // Use allSettled so a single 404/5xx doesn't sink the whole batch.
    const cardIds = [...new Set(sessions.value.map(s => s.openCardId).filter(Boolean))] as string[]
    const linkResults = await Promise.allSettled(
      cardIds.map(cardId => api.GET<CardChatLinkDto[]>(ApiRoutes.Chat.cardLinks.byCard(cardId)))
    )
    const linked = new Set<string>()
    for (const result of linkResults) {
      if (result.status !== 'fulfilled') continue
      const payload = result.value.data
      if (!payload) continue
      for (const link of payload) {
        linked.add(link.chatSessionId)
      }
    }
    cardLinkedSessionIds.value = linked
  } catch (err) {
    toast.error(err instanceof ApiError ? err.message : 'Failed to load chats')
  } finally {
    loading.value = false
  }
}

const filteredSessions = computed(() => {
  switch (filter.value) {
    case 'project':
      return sessions.value.filter((s: ChatSessionDto) => !cardLinkedSessionIds.value.has(s.id))
    case 'card':
      return sessions.value.filter((s: ChatSessionDto) => cardLinkedSessionIds.value.has(s.id))
    default:
      return sessions.value
  }
})

function handleOpenSession(sessionId: string) {
  chatDock.loadSession(sessionId)
  chatDock.openDock('project-chats')
}

function formatDate(iso: string | null | undefined): string {
  return iso ? formatDateOnly(iso) : ''
}

onMounted(() => fetchSessions())

const columns: TableColumn<ChatSessionDto>[] = [
  { accessorKey: 'title', header: 'Title' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'updatedAt', header: 'Updated' }
]
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0 p-4">
    <!-- Filter bar -->
    <div class="flex items-center gap-2 mb-4">
      <USelect
        v-model="filter"
        :items="[
          { label: 'All', value: 'all' },
          { label: 'Project', value: 'project' },
          { label: 'Card', value: 'card' }
        ]"
        size="sm"
      />
      <UButton
        icon="i-lucide-refresh-cw"
        variant="ghost"
        size="sm"
        :loading="loading"
        @click="fetchSessions"
      />
    </div>

    <!-- Session list -->
    <div
      v-if="loading"
      class="flex-1 flex items-center justify-center"
    >
      <UIcon
        name="i-lucide-loader-circle"
        class="size-6 animate-spin"
      />
    </div>
    <div
      v-else-if="filteredSessions.length === 0"
      class="flex-1 flex items-center justify-center"
    >
      <p class="text-sm text-muted">
        No chats found.
      </p>
    </div>
    <UTable
      v-else
      :data="filteredSessions"
      :columns="columns"
      @select="(_e: unknown, row: { original: ChatSessionDto }) => handleOpenSession(row.original.id)"
    >
      <template #title-cell="{ row }">
        <span class="text-sm font-medium">{{ row.original.title || '(untitled)' }}</span>
      </template>
      <template #status-cell="{ row }">
        <UBadge
          :color="row.original.status === 'Active' ? 'success' : 'neutral'"
          variant="subtle"
          size="xs"
        >
          {{ row.original.status }}
        </UBadge>
      </template>
      <template #updatedAt-cell="{ row }">
        <span class="text-sm text-muted">{{ formatDate(row.original.updatedAt) }}</span>
      </template>
    </UTable>
  </div>
</template>
