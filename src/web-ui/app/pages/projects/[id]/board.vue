<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import { ApiError } from '~/lib/api-error'
import CardCreateModal from '~/components/board/CardCreateModal.vue'
import BoardFilterBar from '~/components/board/BoardFilterBar.vue'
import BulkActionBar from '~/components/shared/BulkActionBar.vue'
import MemberManagementPanel from '~/components/project/MemberManagementPanel.vue'

definePageMeta({ middleware: ['auth'] })

// Prevent body scroll — columns handle their own overflow
useHead({
  bodyAttrs: {
    class: 'overflow-hidden'
  }
})

type CardResponse = components['schemas']['CardResponse']

const route = useRoute()
const projectId = route.params.id as string
const board = useBoardStore()
const api = useApi()
const toast = useAppToast()

const projectName = ref('')
const projectArchived = ref(false)

const showCardModal = ref(false)
const selectedCard = ref<CardResponse | null>(null)
const selectedCardId = ref<string | null>(null)
const showCreateModal = ref(false)
const createColumnId = ref<string | null>(null)
const bulkTargetColumnId = ref<string | null>(null)
const showMembersPanel = ref(false)

function handleAddCard(columnId?: string) {
  if (projectArchived.value) return
  createColumnId.value = columnId ?? null
  showCreateModal.value = true
}

async function handleCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  if (projectArchived.value) return

  const card = findCard(cardId)
  if (!card) return

  board.moveCard(cardId, targetColumnId, targetPosition)

  try {
    await api.POST(ApiRoutes.Cards.move(projectId, cardId), {
      body: {
        targetColumnId,
        targetPosition,
        confirmBlockedMove: false,
        version: card.version
      }
    })
  } catch (error: unknown) {
    board.rollbackMove(projectId)
    if (error instanceof ApiError && error.status === 409) {
      toast.error('Cannot move blocked card')
    } else {
      toast.error('Failed to move card')
    }
    return
  }
}

function findCard(cardId: string): CardResponse | undefined {
  for (const [, cards] of board.cardsByColumn) {
    const found = cards.find(c => c.id === cardId)
    if (found) return found
  }
  return undefined
}

function handleCardClick(card: CardResponse) {
  selectedCard.value = card
  selectedCardId.value = card.id
  showCardModal.value = true
}

function handleCardModalClose() {
  selectedCardId.value = null
  board.fetchBoard(projectId)
}

// Bulk handlers for desktop
async function handleBulkMove() {
  if (projectArchived.value) return
  if (!bulkTargetColumnId.value) return
  const ids = Object.keys(board.selectedCardIds).filter(k => (board.selectedCardIds as Record<string, boolean>)[k])
  for (const cardId of ids) {
    const card = findCard(cardId)
    if (!card) continue
    const targetPosition = 0
    // Optimistic update
    board.moveCard(cardId, bulkTargetColumnId.value, targetPosition)
    try {
      await api.POST(ApiRoutes.Cards.move(projectId, cardId), {
        body: {
          targetColumnId: bulkTargetColumnId.value,
          targetPosition,
          confirmBlockedMove: false,
          version: card.version
        }
      })
    } catch {
      board.rollbackMove(projectId)
      toast.error(`Failed to move card #${card.cardNumber}`)
    }
  }
  board.clearSelection()
  toast.success(`Moved ${ids.length} card(s)`)
}

async function handleBulkArchive() {
  if (projectArchived.value) return
  const ids = Object.keys(board.selectedCardIds).filter(k => (board.selectedCardIds as Record<string, boolean>)[k])
  for (const cardId of ids) {
    const card = findCard(cardId)
    if (!card) continue
    try {
      await api.POST(ApiRoutes.Cards.archive(projectId, cardId), {
        body: { version: card.version }
      })
      board.removeCard(cardId)
    } catch {
      toast.error(`Failed to archive #${card.cardNumber}`)
    }
  }
  board.clearSelection()
  toast.success(`Archived ${ids.length} card(s)`)
}

onMounted(async () => {
  board.fetchBoard(projectId)
  board.fetchMembers(projectId)
  const { data } = await api.GET(ApiRoutes.Projects.detail(projectId))
  if (data) {
    const project = data as components['schemas']['ProjectResponse']
    projectName.value = project.name
    projectArchived.value = !!project.archivedAt
  }
})

async function handleRestore() {
  try {
    await api.POST(ApiRoutes.Projects.toggleArchive(projectId))
    projectArchived.value = false
    toast.success('Project restored')
    board.fetchBoard(projectId)
  } catch {
    toast.error('Failed to restore project')
  }
}

// Only re-fetch when type or includeArchived actually change — not when search changes
watch(
  () => board.boardFilters.type,
  (newType, oldType) => {
    if (newType !== oldType) board.fetchBoard(projectId)
  }
)

watch(
  () => board.boardFilters.includeArchived,
  (newArchived, oldArchived) => {
    if (newArchived !== oldArchived) board.fetchBoard(projectId)
  }
)

// Debounced search — 300ms after user stops typing, fetch with search param
let searchTimer: ReturnType<typeof setTimeout> | null = null
watch(
  () => board.boardFilters.search,
  () => {
    if (searchTimer) clearTimeout(searchTimer)
    searchTimer = setTimeout(() => {
      board.fetchBoard(projectId)
      searchTimer = null
    }, 300)
  }
)

watch(
  () => board.boardFilters.assigneeUserId,
  (newAssignee, oldAssignee) => {
    if (newAssignee !== oldAssignee) board.fetchBoard(projectId)
  }
)
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <div class="flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-gray-700">
      <div class="flex items-center gap-2 min-w-0">
        <h1 class="text-xl font-bold truncate">
          {{ projectName || 'Board' }}
        </h1>
        <UBadge
          v-if="projectArchived"
          variant="subtle"
          size="xs"
          color="neutral"
        >
          Archived
        </UBadge>
      </div>
      <div class="flex items-center gap-1">
        <UButton
          v-if="projectArchived"
          variant="ghost"
          size="sm"
          icon="i-lucide-archive-restore"
          title="Restore project"
          @click="handleRestore"
        />
        <UButton
          variant="ghost"
          size="sm"
          icon="i-lucide-users"
          title="Members"
          @click="showMembersPanel = !showMembersPanel"
        />
        <UButton
          variant="ghost"
          size="sm"
          @click="board.fetchBoard(projectId)"
        >
          <UIcon
            name="i-lucide-refresh-cw"
            class="size-4"
          />
        </UButton>
      </div>
    </div>

    <!-- Filter bar — always visible above the board area -->
    <BoardFilterBar
      :members="board.members"
      :readonly="projectArchived"
      class="hidden md:flex"
      @add-card="handleAddCard()"
    />

    <!-- Members panel -->
    <div
      v-if="showMembersPanel"
      class="px-4 pt-3"
    >
      <MemberManagementPanel
        :project-id="projectId"
        @update="board.fetchMembers(projectId)"
      />
    </div>

    <!-- Board area — takes remaining height with its own scroll context -->
    <div class="flex-1 flex flex-col min-h-0">
      <!-- Error state — shown above all board content when present -->
      <div
        v-if="board.error"
        class="h-full flex items-center justify-center"
      >
        <div class="text-center">
          <p class="text-red-500 mb-2">
            {{ board.error }}
          </p>
          <UButton
            variant="outline"
            size="sm"
            @click="board.fetchBoard(projectId)"
          >
            Retry
          </UButton>
        </div>
      </div>

      <!-- Desktop board content — hidden during loading (desktop uses full-height spinner) -->
      <div
        v-else-if="!board.loading"
        class="hidden md:flex flex-1 flex-col min-h-0"
      >
        <!-- Bulk action bar for desktop (above board) -->
        <BulkActionBar
          :selected-count="board.selectedCount"
          :bulk-target-column-id="bulkTargetColumnId"
          :columns="board.columns"
          @update:bulk-target-column-id="val => bulkTargetColumnId = val"
          @move="handleBulkMove"
          @archive="handleBulkArchive"
          @clear="board.clearSelection()"
        />
        <div class="flex-1 min-h-0 overflow-x-auto p-4 flex flex-col">
          <BoardView
            :columns="board.visibleColumns"
            :cards-by-column="board.cardsByColumn"
            :project-id="projectId"
            :include-archived="board.boardFilters.includeArchived"
            :readonly="projectArchived"
            @card-move="handleCardMove"
            @card-click="handleCardClick"
            @add-card="handleAddCard"
          />
        </div>
      </div>

      <!-- Desktop: full-height loading spinner (only when loading) -->
      <div
        v-else
        class="hidden md:flex h-full items-center justify-center"
      >
        <UIcon
          name="i-lucide-loader"
          class="size-8 animate-spin"
        />
      </div>

      <!-- Mobile board content — always rendered so its inline spinner works -->
      <div
        v-if="!board.error"
        class="md:hidden flex-1 overflow-x-auto"
      >
        <BoardMobileList
          :columns="board.visibleColumns"
          :cards-by-column="board.cardsByColumn"
          :project-id="projectId"
          :members="board.members"
          :loading="board.loading"
          :readonly="projectArchived"
          @card-click="handleCardClick"
          @add-card="handleAddCard"
        />
      </div>
    </div>

    <CardModal
      v-if="selectedCardId"
      :card-id="selectedCardId"
      :project-id="projectId"
      :readonly="projectArchived"
      @close="handleCardModalClose"
      @archived="board.fetchBoard(projectId)"
      @restored="board.fetchBoard(projectId)"
    />
    <CardCreateModal
      v-if="showCreateModal"
      :project-id="projectId"
      :columns="board.columns"
      :members="board.members"
      :preselected-column-id="createColumnId ?? undefined"
      @close="showCreateModal = false"
      @created="board.fetchBoard(projectId)"
    />
  </div>
</template>
