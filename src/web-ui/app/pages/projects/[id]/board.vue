<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import CardCreateModal from '~/components/board/CardCreateModal.vue'
import BoardFilterBar from '~/components/board/BoardFilterBar.vue'
import BulkActionBar from '~/components/shared/BulkActionBar.vue'
import MemberManagementPanel from '~/components/project/MemberManagementPanel.vue'
import KeyboardShortcutOverlay from '~/components/shared/KeyboardShortcutOverlay.vue'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import { useCardMove } from '~/composables/useCardMove'
import { onBeforeUnmount, onMounted } from 'vue'
import { useKeyboard } from '~/composables/keyboard/useKeyboard'

definePageMeta({ middleware: ['auth'] })

// Desktop: kanban columns handle own scroll. Mobile: page scrolls naturally.
useHead({
  bodyAttrs: {
    class: 'md:overflow-hidden'
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
const showShortcutOverlay = ref(false)

// Board-level archive state
const showArchiveConfirm = ref(false)
const archiveTargetCard = ref<CardResponse | null>(null)

// Block board shortcuts when any modal overlay is open
const anyModalOpen = computed(() =>
  !!selectedCardId.value
  || showCreateModal.value
  || showArchiveConfirm.value
  || showShortcutOverlay.value
  || showMembersPanel.value
)

async function confirmArchive() {
  const card = archiveTargetCard.value
  if (!card) return
  try {
    await api.POST(ApiRoutes.Cards.archive(projectId, card.id), {
      body: { version: card.version }
    })
    toast.success('Card archived')
    board.fetchBoard(projectId)
  } catch {
    toast.error('Failed to archive card')
  }
  showArchiveConfirm.value = false
  archiveTargetCard.value = null
}

function handleAddCard(columnId?: string) {
  if (projectArchived.value) return
  createColumnId.value = columnId ?? null
  showCreateModal.value = true
}

// Board navigation tracking
const selectedColumnIndex = ref(0)
const selectedCardIndex = ref(0)

const selectedCardIdForKeyboard = computed(() => {
  const columns = board.visibleColumns
  if (!columns.length) return null
  const col = columns[selectedColumnIndex.value]
  if (!col) return null
  const cards = board.cardsByColumn.get(col.id) ?? []
  const card = cards[selectedCardIndex.value]
  return card?.id ?? null
})

const { moveCardToColumn } = useCardMove(projectId)
const realtime = useRealtime()
const presence = usePresence()
const keyboard = useKeyboard()

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
  // Sync keyboard selection state to match the clicked card
  for (const [colIdx, col] of board.visibleColumns.entries()) {
    const cards = board.cardsByColumn.get(col.id) ?? []
    const cardIdx = cards.findIndex(c => c.id === card.id)
    if (cardIdx !== -1) {
      selectedColumnIndex.value = colIdx
      selectedCardIndex.value = cardIdx
      break
    }
  }
}

function handleColumnClick(columnId: string) {
  const idx = board.visibleColumns.findIndex(c => c.id === columnId)
  if (idx !== -1) {
    selectedColumnIndex.value = idx
    selectedCardIndex.value = 0
  }
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
  realtime.connect(projectId)
  presence.connect(projectId)
  const { data } = await api.GET(ApiRoutes.Projects.detail(projectId))
  if (data) {
    const project = data as components['schemas']['ProjectResponse']
    projectName.value = project.name
    projectArchived.value = !!project.archivedAt
  }

  // Register board shortcuts
  // All Board shortcuts guarded by anyModalOpen — prevent background navigation when overlay open
  function isModalOpen() {
    return anyModalOpen.value
  }

  keyboard.register('Board', 'j', (e) => {
    if (isModalOpen()) return
    e.preventDefault()
    const columns = board.visibleColumns
    if (columns.length === 0) return
    const currentColumn = columns[selectedColumnIndex.value]
    if (!currentColumn) return
    const cards = board.cardsByColumn.get(currentColumn.id) || []
    if (cards.length === 0) return
    selectedCardIndex.value = (selectedCardIndex.value + 1) % cards.length
  }, 'Next card')

  keyboard.register('Board', 'k', (e) => {
    if (isModalOpen()) return
    e.preventDefault()
    const columns = board.visibleColumns
    if (columns.length === 0) return
    const currentColumn = columns[selectedColumnIndex.value]
    if (!currentColumn) return
    const cards = board.cardsByColumn.get(currentColumn.id) || []
    if (cards.length === 0) return
    selectedCardIndex.value = (selectedCardIndex.value - 1 + cards.length) % cards.length
  }, 'Previous card')

  keyboard.register('Board', 'l', (e) => {
    if (isModalOpen()) return
    e.preventDefault()
    const columns = board.visibleColumns
    if (columns.length === 0) return
    selectedColumnIndex.value = (selectedColumnIndex.value + 1) % columns.length
    selectedCardIndex.value = 0
  }, 'Next column')

  keyboard.register('Board', 'h', (e) => {
    if (isModalOpen()) return
    e.preventDefault()
    const columns = board.visibleColumns
    if (columns.length === 0) return
    selectedColumnIndex.value = (selectedColumnIndex.value - 1 + columns.length) % columns.length
    selectedCardIndex.value = 0
  }, 'Previous column')

  keyboard.register('Board', '?', (e) => {
    if (isModalOpen()) return
    e.preventDefault()
    showShortcutOverlay.value = true
  }, 'Show keyboard shortcuts')

  keyboard.register('Board', 'n', (e) => {
    if (isModalOpen()) return
    e.preventDefault()
    const columns = board.visibleColumns
    if (columns.length === 0) return
    const currentColumn = columns[selectedColumnIndex.value]
    if (currentColumn && !projectArchived.value) {
      handleAddCard(currentColumn.id)
    }
  }, 'Create new card')

  keyboard.register('Board', 'Enter', (e) => {
    if (isModalOpen()) return
    e.preventDefault()
    const columns = board.visibleColumns
    if (columns.length === 0) return
    const currentColumn = columns[selectedColumnIndex.value]
    if (!currentColumn) return
    const cards = board.cardsByColumn.get(currentColumn.id) || []
    if (cards.length === 0) return
    const card = cards[selectedCardIndex.value]
    if (card) {
      handleCardClick(card)
    }
  }, 'Open card')

  keyboard.register('Board', 'a', (e) => {
    if (isModalOpen()) return
    e.preventDefault()
    if (projectArchived.value) return
    const columns = board.visibleColumns
    if (columns.length === 0) return
    const currentColumn = columns[selectedColumnIndex.value]
    if (!currentColumn) return
    const cards = board.cardsByColumn.get(currentColumn.id) || []
    if (cards.length === 0) return
    const card = cards[selectedCardIndex.value]
    if (card && !card.archivedAt) {
      archiveTargetCard.value = card
      showArchiveConfirm.value = true
    }
  }, 'Archive card')

  // Move highlighted card between columns: Ctrl+Shift+Left/Right
  function moveSelectedCard(direction: -1 | 1) {
    if (anyModalOpen.value || projectArchived.value) return
    const columns = board.visibleColumns
    if (columns.length < 2) return
    const fromCol = columns[selectedColumnIndex.value]
    if (!fromCol) return
    const cards = board.cardsByColumn.get(fromCol.id) ?? []
    const card = cards[selectedCardIndex.value]
    if (!card) return
    const targetIdx = selectedColumnIndex.value + direction
    if (targetIdx < 0 || targetIdx >= columns.length) return
    const toCol = columns[targetIdx]
    if (!toCol) return
    const targetCards = board.cardsByColumn.get(toCol.id) ?? []
    const newPos = targetCards.length // append at end
    moveCardToColumn(card.id, toCol.id, newPos)
    // Select the moved card at its new position
    selectedColumnIndex.value = targetIdx
    selectedCardIndex.value = newPos
  }

  // Reorder card within column: Ctrl+Shift+Up/Down
  function reorderSelectedCard(direction: -1 | 1) {
    if (anyModalOpen.value || projectArchived.value) return
    const col = board.visibleColumns[selectedColumnIndex.value]
    if (!col) return
    const cards = board.cardsByColumn.get(col.id) ?? []
    if (cards.length < 2) return
    const card = cards[selectedCardIndex.value]
    if (!card) return
    const newPos = selectedCardIndex.value + direction
    if (newPos < 0 || newPos >= cards.length) return
    moveCardToColumn(card.id, col.id, newPos)
    selectedCardIndex.value = newPos
  }

  keyboard.register('Board', 'ArrowRight', (e) => {
    if (!e.ctrlKey || !e.shiftKey) return
    e.preventDefault()
    moveSelectedCard(1)
  }, 'Move card right')

  keyboard.register('Board', 'ArrowLeft', (e) => {
    if (!e.ctrlKey || !e.shiftKey) return
    e.preventDefault()
    moveSelectedCard(-1)
  }, 'Move card left')

  keyboard.register('Board', 'ArrowUp', (e) => {
    if (!e.ctrlKey || !e.shiftKey) return
    e.preventDefault()
    reorderSelectedCard(-1)
  }, 'Move card up')

  keyboard.register('Board', 'ArrowDown', (e) => {
    if (!e.ctrlKey || !e.shiftKey) return
    e.preventDefault()
    reorderSelectedCard(1)
  }, 'Move card down')
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

// Only re-fetch when includeArchived actually change — not when search changes
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

watch(selectedCardId, (cardId) => {
  if (cardId) presence.focusCard(projectId, cardId)
  else presence.unfocusCard(projectId)
})

onBeforeUnmount(() => {
  realtime.disconnect(projectId)
  presence.disconnect(projectId)
  // Unregister board shortcuts
  keyboard.unregister('Board')
})

// Presence indicator
const presenceStore = usePresenceStore()
const onlineUsers = computed(() => {
  const users = presenceStore.onlineUsers.get(projectId)
  return users ? users : []
})

// Helper function to generate consistent avatar colors based on user ID
const AVATAR_COLORS = ['#6366f1', '#8b5cf6', '#ec4899', '#f43f5e', '#f97316', '#eab308', '#22c55e', '#14b8a6', '#06b6d4', '#3b82f6']
function hashColor(id: string): string {
  let hash = 0
  for (let i = 0; i < id.length; i++) hash += id.charCodeAt(i)
  return AVATAR_COLORS[hash % AVATAR_COLORS.length]!
}
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
        <div
          v-if="onlineUsers.length > 0"
          class="flex items-center -space-x-1.5 ml-2"
        >
          <span
            v-for="u in onlineUsers"
            :key="u.userId"
            :title="u.username"
            class="inline-flex items-center justify-center size-6 rounded-full text-xs font-medium text-white ring-2 ring-white dark:ring-gray-900"
            :style="{ backgroundColor: hashColor(u.userId) }"
          >
            {{ (u.username[0] ?? '').toUpperCase() }}
          </span>
        </div>
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
      :columns="board.columns"
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
            :selected-card-id="selectedCardIdForKeyboard"
            :selected-column-index="selectedColumnIndex"
            @card-move="moveCardToColumn"
            @card-click="handleCardClick"
            @column-click="handleColumnClick"
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
        class="md:hidden flex-1 min-h-0 overflow-auto"
      >
        <BoardMobileList
          :columns="board.columns"
          :cards-by-column="board.cardsByColumn"
          :project-id="projectId"
          :members="board.members"
          :loading="board.loading"
          :readonly="projectArchived"
          @card-click="handleCardClick"
          @add-card="handleAddCard"
          @card-move="moveCardToColumn"
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

    <KeyboardShortcutOverlay
      v-if="showShortcutOverlay"
      :open="showShortcutOverlay"
      @close="showShortcutOverlay = false"
    />

    <ConfirmDialog
      v-model:open="showArchiveConfirm"
      title="Archive card"
      :message="archiveTargetCard ? `Archive #${archiveTargetCard.cardNumber} ${archiveTargetCard.title}?` : ''"
      confirm-text="Archive"
      @confirm="confirmArchive"
    />
  </div>
</template>
