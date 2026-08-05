<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes, UiRoutes } from '~/lib/routes'
import CardCreateModal from '~/components/board/CardCreateModal.vue'
import BoardFilterBar from '~/components/board/BoardFilterBar.vue'
import BulkActionBar from '~/components/shared/BulkActionBar.vue'
import MemberManagementPanel from '~/components/project/MemberManagementPanel.vue'
import ProjectNarrativeModal from '~/components/project/ProjectNarrativeModal.vue'
import KeyboardShortcutOverlay from '~/components/shared/KeyboardShortcutOverlay.vue'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import { useCardMove } from '~/composables/useCardMove'
import { onBeforeUnmount, onMounted, watch } from 'vue'
import { useBoardKeyboardNav } from '~/composables/keyboard/useBoardKeyboardNav'
import { useBoardStore } from '~/stores/board'

const props = defineProps<{
  projectId: string
}>()

type CardResponse = components['schemas']['CardResponse']

const boardStore = useBoardStore()
const api = useApi()
const toast = useAppToast()

const projectName = ref('')
const projectArchived = ref(false)

const showCardModal = ref(false)
const selectedCard = ref<CardResponse | null>(null)
const selectedCardId = computed({
  get: () => boardStore.openCardId,
  set: val => boardStore.setOpenCardId(val)
})
const showCreateModal = ref(false)
const createColumnId = ref<string | null>(null)
const bulkTargetColumnId = ref<string | null>(null)
const showMembersPanel = ref(false)
const showShortcutOverlay = ref(false)
const showNarrativeModal = ref(false)

const showArchiveConfirm = ref(false)
const archiveTargetCard = ref<CardResponse | null>(null)

const anyModalOpen = computed(() =>
  !!selectedCardId.value
  || showCreateModal.value
  || showArchiveConfirm.value
  || showShortcutOverlay.value
  || showMembersPanel.value
  || showNarrativeModal.value
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
const presence = usePresence()

function findCard(cardId: string): CardResponse | undefined {
  for (const [, cards] of boardStore.cardsByColumn) {
    const found = cards.find(c => c.id === cardId)
    if (found) return found
  }
  return undefined
}

function openCardModal(card: CardResponse) {
  selectedCard.value = card
  selectedCardId.value = card.id
  showCardModal.value = true
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

function handleCardModalClose() {
  selectedCardId.value = null
  selectedCard.value = null
  boardStore.fetchBoard(props.projectId)
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

onMounted(async () => {
  boardStore.fetchBoard(props.projectId)
  boardStore.fetchMembers(props.projectId)
  realtime.connect(props.projectId)
  presence.connect(props.projectId)
  try {
    const { data } = await api.GET(ApiRoutes.Projects.detail(props.projectId))
    if (data) {
      const project = data as components['schemas']['ProjectResponse']
      projectName.value = project.name
      projectArchived.value = !!project.archivedAt
    }
  } catch {
    toast.error('Failed to load project details')
  }
  nav.activate()
})

async function handleRestore() {
  try {
    await api.POST(ApiRoutes.Projects.toggleArchive(props.projectId))
    projectArchived.value = false
    toast.success('Project restored')
    boardStore.fetchBoard(props.projectId)
  } catch {
    toast.error('Failed to restore project')
  }
}

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

watch(selectedCardId, (cardId) => {
  if (cardId) {
    presence.focusCard(props.projectId, cardId)
  } else {
    presence.unfocusCard(props.projectId)
  }
})

onBeforeUnmount(() => {
  realtime.disconnect(props.projectId)
  presence.disconnect(props.projectId)
  nav.deactivate()
})

const presenceStore = usePresenceStore()
const onlineUsers = computed(() => {
  const users = presenceStore.onlineUsers.get(props.projectId)
  return users ? users : []
})

function viewingCardNumber(userId: string): number | string | null {
  const cardId = presenceStore.focusedCards.get(userId)
  if (!cardId) return null
  for (const cards of boardStore.cardsByColumn.values()) {
    const found = cards.find(c => c.id === cardId)
    if (found) return found.cardNumber
  }
  return null
}

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
        <UButton
          icon="i-lucide-arrow-left"
          variant="ghost"
          size="sm"
          :to="UiRoutes.Projects.List"
          aria-label="Back to projects"
        />
        <h1 class="text-xl font-bold truncate">
          {{ projectName || 'Board' }}
        </h1>
        <UButton
          variant="ghost"
          size="sm"
          icon="i-lucide-sparkles"
          title="View AI narrative"
          @click="showNarrativeModal = true"
        />
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
        <UPopover v-if="onlineUsers.length > 0">
          <button
            type="button"
            class="flex items-center -space-x-1.5 ml-2 cursor-pointer"
            :aria-label="`${onlineUsers.length} online`"
          >
            <span
              v-for="u in onlineUsers"
              :key="u.userId"
              class="inline-flex items-center justify-center size-6 rounded-full text-xs font-medium text-white ring-2 ring-white dark:ring-gray-900"
              :style="{ backgroundColor: hashColor(u.userId) }"
            >
              {{ (u.username[0] ?? '').toUpperCase() }}
            </span>
          </button>

          <template #content>
            <div class="w-56 py-1">
              <div class="px-3 py-1.5 text-xs font-medium text-muted uppercase">
                Online — {{ onlineUsers.length }}
              </div>
              <div
                v-for="u in onlineUsers"
                :key="u.userId"
                class="px-3 py-1.5 flex items-center gap-2 text-sm"
              >
                <span
                  class="inline-flex items-center justify-center size-5 rounded-full text-xs font-medium text-white shrink-0"
                  :style="{ backgroundColor: hashColor(u.userId) }"
                >
                  {{ (u.username[0] ?? '').toUpperCase() }}
                </span>
                <span class="truncate">{{ u.username }}</span>
                <span
                  v-if="viewingCardNumber(u.userId)"
                  class="text-xs text-muted shrink-0 ml-auto"
                >viewing #{{ viewingCardNumber(u.userId) }}</span>
              </div>
            </div>
          </template>
        </UPopover>
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
          @click="boardStore.fetchBoard(props.projectId)"
        >
          <UIcon
            name="i-lucide-refresh-cw"
            class="size-4"
          />
        </UButton>
      </div>
    </div>

    <BoardFilterBar
      :members="boardStore.members"
      :columns="boardStore.columns"
      :readonly="projectArchived"
      class="hidden md:flex"
      @add-card="handleAddCard()"
    />

    <div
      v-if="showMembersPanel"
      class="px-4 pt-3"
    >
      <MemberManagementPanel
        :project-id="projectId"
        @update="boardStore.fetchMembers(props.projectId)"
      />
    </div>

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

    <CardModal
      v-if="selectedCardId"
      :card-id="selectedCardId"
      :project-id="props.projectId"
      :readonly="projectArchived"
      @close="handleCardModalClose"
      @archived="boardStore.fetchBoard(props.projectId)"
      @restored="boardStore.fetchBoard(props.projectId)"
    />
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

    <ProjectNarrativeModal
      v-if="showNarrativeModal"
      :project-id="props.projectId"
      @close="showNarrativeModal = false"
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
