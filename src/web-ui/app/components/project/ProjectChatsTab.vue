<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import type { ChatSessionDto, ChatSessionPageDto } from '~/types/chat'
import { ApiRoutes } from '~/lib/routes'
import { ApiError } from '~/lib/api-error'
import { formatDateOnly } from '~/lib/date'
import { getChatType, CHAT_TYPE_BADGE } from '~/lib/chat-type'
import ClientDataTable from '~/components/shared/ClientDataTable.vue'

const props = defineProps<{ projectId: string }>()

const api = useApi()
const toast = useAppToast()
const chatDock = useChatDockStore()

const sessions = ref<ChatSessionDto[]>([])
const loading = ref(true)
const filter = ref<'all' | 'project' | 'card'>('all')

async function fetchSessions() {
  loading.value = true
  try {
    const url = ApiRoutes.Chat.sessions.list(undefined, props.projectId, undefined, undefined, 50, undefined, 'participated')
    const { data } = await api.GET<ChatSessionPageDto>(url)
    sessions.value = data?.items ?? []
  } catch (err) {
    toast.error(err instanceof ApiError ? err.message : 'Failed to load chats')
  } finally {
    loading.value = false
  }
}

// Card vs Project is already fully determined by openCardId on the DTO — no
// need to round-trip through CardChatLink (which also lags a manually-linked
// session until its chat closes; see ChatSessionService.LinkCardAsync).
const filteredSessions = computed(() => {
  switch (filter.value) {
    case 'project':
      return sessions.value.filter((s: ChatSessionDto) => getChatType(s) !== 'card')
    case 'card':
      return sessions.value.filter((s: ChatSessionDto) => getChatType(s) === 'card')
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
  { accessorKey: 'type', header: 'Type' },
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
    <ClientDataTable
      :data="filteredSessions"
      :columns="columns"
      :row-key="(s: ChatSessionDto) => s.id"
      :loading="loading"
      :fill-height="true"
      :default-page-size="20"
      @select="(s: ChatSessionDto) => handleOpenSession(s.id)"
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
      <template #type-cell="{ row }">
        <UBadge
          :color="CHAT_TYPE_BADGE[getChatType(row.original)].color"
          variant="subtle"
          size="xs"
        >
          {{ CHAT_TYPE_BADGE[getChatType(row.original)].label }}
        </UBadge>
      </template>
      <template #updatedAt-cell="{ row }">
        <span class="text-sm text-muted">{{ formatDate(row.original.updatedAt) }}</span>
      </template>
    </ClientDataTable>
  </div>
</template>
