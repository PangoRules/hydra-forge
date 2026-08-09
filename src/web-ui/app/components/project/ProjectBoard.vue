<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import CardCreateModal from '~/components/board/CardCreateModal.vue'
import BoardFilterBar from '~/components/board/BoardFilterBar.vue'
import BulkActionBar from '~/components/shared/BulkActionBar.vue'
import KeyboardShortcutOverlay from '~/components/shared/KeyboardShortcutOverlay.vue'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import { useCardMove } from '~/composables/useCardMove'
import { onBeforeUnmount, onMounted, watch } from 'vue'
import { useBoardKeyboardNav } from '~/composables/keyboard/useBoardKeyboardNav'
import { useBoardStore } from '~/stores/board'

const props = defineProps<{
  projectId: string
  projectArchived: boolean
  presence: ReturnType<typeof usePresence>
  externalModalOpen: boolean
}>()

type CardResponse = components['schemas']['CardResponse']

const boardStore = useBoardStore()
const api = useApi()
const toast = useAppToast()
const cardPopup = useCardPopupStore()

// Header (back button, title, members, narrative, presence) lives in the parent
// page (projects/[id]/index.vue) so it survives switching to the Docs tab —
// projectArchived/presence are owned there and passed down as props.
const projectArchived = computed(() => props.projectArchived)

const showCreateModal = ref(false)
const createColumnId = ref<string | null>(null)
const bulkTargetColumnId = ref<string | null>(null)
const showShortcutOverlay = ref(false)

const showArchiveConfirm = ref(false)
const archiveTargetCard = ref<CardResponse | null>(null)

const anyModalOpen = computed(() =>
  showCreateModal.value
  || showArchiveConfirm.value
  || showShortcutOverlay.value
  || props.externalModalOpen
)

async function confirmArchive() {
  const card = archiveTargetCard.value
  if (!card) return
  try {
    await api.POST(ApiRoutes.Cards.archive(props.projectId, card.id), {
      body: { version: card.version }
    })
    toast.success('Card archived')
    boardStore.fetchBoard(props.projectId)
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

const { moveCardToColumn } = useCardMove(props.projectId)
const realtime = useRealtime()

function findCard(cardId: string): CardResponse | undefined {
  for (const [, cards] of boardStore.cardsByColumn) {
    const found = cards.find(c => c.id === cardId)
    if (found) return found
  }
  return undefined
}

function openCardModal(card: CardResponse) {
  cardPopup.openCard(card.id, props.projectId)
  boardStore.setOpenCardId(card.id)
}

function requestArchive(card: CardResponse) {
  archiveTargetCard.value = card
  showArchiveConfirm.value = true
}

const nav = useBoardKeyboardNav({
  projectId: props.projectId,
  anyModalOpen,
  projectArchived,
  onOpenCard: openCardModal,
  onCreateCard: handleAddCard,
  onArchiveCard: requestArchive,
  onShowShortcuts: () => { showShortcutOverlay.value = true }
})

function handleCardClick(card: CardResponse) {
  openCardModal(card)
  nav.syncToCard(card)
}

async function handleBulkMove() {
  if (projectArchived.value) return
  if (!bulkTargetColumnId.value) return
  const ids = Object.keys(boardStore.selectedCardIds).filter(k => (boardStore.selectedCardIds as Record<string, boolean>)[k])
  for (const cardId of ids) {
    const card = findCard(cardId)
    if (!card) continue
    const targetPosition = 0
    boardStore.moveCard(cardId, bulkTargetColumnId.value, targetPosition)
    try {
      await api.POST(ApiRoutes.Cards.move(props.projectId, cardId), {
        body: {
          targetColumnId: bulkTargetColumnId.value,
          targetPosition,
          confirmBlockedMove: false,
          version: card.version
        }
      })
    } catch {
      boardStore.rollbackMove(props.projectId)
      toast.error(`Failed to move card #${card.cardNumber}`)
    }
  }
  boardStore.clearSelection()
  toast.success(`Moved ${ids.length} card(s)`)
}

async function handleBulkArchive() {
  if (projectArchived.value) return
  const ids = Object.keys(boardStore.selectedCardIds).filter(k => (boardStore.selectedCardIds as Record<string, boolean>)[k])
  for (const cardId of ids) {
    const card = findCard(cardId)
    if (!card) continue
    try {
      await api.POST(ApiRoutes.Cards.archive(props.projectId, cardId), {
        body: { version: card.version }
      })
      boardStore.removeCard(cardId)
    } catch {
      toast.error(`Failed to archive #${card.cardNumber}`)
    }
  }
  boardStore.clearSelection()
  toast.success(`Archived ${ids.length} card(s)`)
}

onMounted(() => {
  boardStore.fetchBoard(props.projectId)
  boardStore.fetchMembers(props.projectId)
  realtime.connect(props.projectId)
  nav.activate()
})

watch(
  () => boardStore.boardFilters.includeArchived,
  (newArchived, oldArchived) => {
    if (newArchived !== oldArchived) boardStore.fetchBoard(props.projectId)
  }
)

let searchTimer: ReturnType<typeof setTimeout> | null = null
watch(
  () => boardStore.boardFilters.search,
  () => {
    if (searchTimer) clearTimeout(searchTimer)
    searchTimer = setTimeout(() => {
      boardStore.fetchBoard(props.projectId)
      searchTimer = null
    }, 300)
  }
)

watch(
  () => boardStore.boardFilters.assigneeUserId,
  (newAssignee, oldAssignee) => {
    if (newAssignee !== oldAssignee) boardStore.fetchBoard(props.projectId)
  }
)

// Watch cardPopup.openCardIds — when a card popup is closed, refresh the board
// to pick up any changes made in the popup (description edits, metadata, etc.)
// and update open-card presence tracking: only clear openCardId if the closed
// card was the tracked one; otherwise update to the remaining activeCardId.
watch(
  () => [...cardPopup.openCardIds],
  (newIds, oldIds) => {
    const removed = oldIds.filter(id => !newIds.includes(id))
    if (removed.length === 0) return
    if (boardStore.openCardId !== null && removed.includes(boardStore.openCardId)) {
      boardStore.setOpenCardId(cardPopup.activeCardId)
    }
    boardStore.fetchBoard(props.projectId)
  }
)

// Track the currently open card for presence
const selectedCardId = computed(() => boardStore.openCardId)
watch(selectedCardId, (cardId) => {
  if (cardId) {
    props.presence.focusCard(props.projectId, cardId)
  } else {
    props.presence.unfocusCard(props.projectId)
  }
})

onBeforeUnmount(() => {
  realtime.disconnect(props.projectId)
  nav.deactivate()
})
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <BoardFilterBar
      :members="boardStore.members"
      :columns="boardStore.columns"
      :readonly="projectArchived"
      class="hidden md:flex"
      @add-card="handleAddCard()"
    />

    <div class="flex-1 flex flex-col min-h-0">
      <div
        v-if="boardStore.error"
        class="h-full flex items-center justify-center"
      >
        <div class="text-center">
          <p class="text-red-500 mb-2">
            {{ boardStore.error }}
          </p>
          <UButton
            variant="outline"
            size="sm"
            @click="boardStore.fetchBoard(props.projectId)"
          >
            Retry
          </UButton>
        </div>
      </div>

      <div
        v-else-if="!boardStore.loading"
        class="hidden md:flex flex-1 flex-col min-h-0"
      >
        <BulkActionBar
          :selected-count="boardStore.selectedCount"
          :bulk-target-column-id="bulkTargetColumnId"
          :columns="boardStore.columns"
          @update:bulk-target-column-id="val => bulkTargetColumnId = val"
          @move="handleBulkMove"
          @archive="handleBulkArchive"
          @clear="boardStore.clearSelection()"
        />
        <div class="flex-1 min-h-0 overflow-x-auto p-4 flex flex-col">
          <BoardView
            :columns="boardStore.visibleColumns"
            :cards-by-column="boardStore.cardsByColumn"
            :project-id="props.projectId"
            :include-archived="boardStore.boardFilters.includeArchived"
            :readonly="projectArchived"
            :selected-card-id="nav.selectedCardId.value"
            :selected-column-index="nav.selectedColumnIndex.value"
            @card-move="moveCardToColumn"
            @card-click="handleCardClick"
            @column-click="nav.syncToColumn"
            @add-card="handleAddCard"
            @container-focus="nav.clampSelection"
          />
        </div>
      </div>

      <div
        v-else
        class="hidden md:flex h-full items-center justify-center"
      >
        <UIcon
          name="i-lucide-loader-circle"
          class="size-8 animate-spin"
        />
      </div>

      <div
        v-if="!boardStore.error"
        class="md:hidden flex-1 min-h-0 overflow-auto"
      >
        <BoardMobileList
          :columns="boardStore.columns"
          :cards-by-column="boardStore.cardsByColumn"
          :project-id="props.projectId"
          :members="boardStore.members"
          :loading="boardStore.loading"
          :readonly="projectArchived"
          @card-click="handleCardClick"
          @add-card="handleAddCard"
          @card-move="moveCardToColumn"
        />
      </div>
    </div>

    <CardCreateModal
      v-if="showCreateModal"
      :project-id="props.projectId"
      :columns="boardStore.columns"
      :members="boardStore.members"
      :preselected-column-id="createColumnId ?? undefined"
      @close="showCreateModal = false"
      @created="boardStore.fetchBoard(props.projectId)"
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
