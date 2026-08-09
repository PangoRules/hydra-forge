<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import type { CardChatLinkDto } from '~/types/chat'
import { ApiRoutes } from '~/lib/routes'
import { ApiError } from '~/lib/api-error'
import { formatDateOnly } from '~/lib/date'

const props = defineProps<{ cardId: string }>()
const emit = defineEmits<{ 'open-session': [sessionId: string] }>()

const api = useApi()
const toast = useAppToast()

const links = ref<CardChatLinkDto[]>([])
const loading = ref(true)
const isExpanded = ref(false)

async function fetchLinks() {
  loading.value = true
  try {
    const { data } = await api.GET(ApiRoutes.Chat.cardLinks.byCard(props.cardId))
    links.value = (data as CardChatLinkDto[]) ?? []
  } catch (err) {
    toast.error(err instanceof ApiError ? err.message : 'Failed to load linked chats')
  } finally {
    loading.value = false
  }
}

function handleOpenSession(sessionId: string) {
  emit('open-session', sessionId)
}

function formatDate(iso: string | null | undefined): string {
  return iso ? formatDateOnly(iso) : ''
}

function handleToggle() {
  if (!isExpanded.value) {
    void fetchLinks()
  }
  isExpanded.value = !isExpanded.value
}

const columns: TableColumn<CardChatLinkDto>[] = [
  { accessorKey: 'ownerUsername', header: 'Owner' },
  { accessorKey: 'summary', header: 'Summary' },
  { accessorKey: 'createdAt', header: 'Created' },
  { id: 'actions', header: '' }
]
</script>

<template>
  <div class="space-y-2">
    <UButton
      variant="ghost"
      size="sm"
      :icon="isExpanded ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'"
      @click="handleToggle"
    >
      Linked Chats ({{ links.length }})
    </UButton>

    <div v-if="isExpanded">
      <div
        v-if="loading"
        class="text-sm text-muted py-2"
      >
        Loading...
      </div>
      <div
        v-else-if="links.length === 0"
        class="text-sm text-muted py-2"
      >
        No chats linked to this card yet. Close a project chat to create a link.
      </div>
      <UTable
        v-else
        :data="links"
        :columns="columns"
      >
        <template #ownerUsername-cell="{ row }">
          <span class="text-sm">{{ row.original.ownerUsername ?? 'Unknown' }}</span>
        </template>
        <template #summary-cell="{ row }">
          <span class="text-sm truncate max-w-[200px] block">
            {{ row.original.summary ?? '(no summary)' }}
          </span>
        </template>
        <template #createdAt-cell="{ row }">
          <span class="text-sm text-muted">{{ formatDate(row.original.createdAt) }}</span>
        </template>
        <template #actions-cell="{ row }">
          <UButton
            variant="ghost"
            size="xs"
            icon="i-lucide-external-link"
            title="Open chat session"
            @click="handleOpenSession(row.original.chatSessionId)"
          />
        </template>
      </UTable>
    </div>
  </div>
</template>
