<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import { useEventListener } from '@vueuse/core'
import { nextTick, watch } from 'vue'
import BulkActionBar from '~/components/shared/BulkActionBar.vue'
import { CARD_TYPE_FILTER_OPTIONS, cardTypeOption, cardTypeColorClass } from '~/lib/card-type'
import { formatDueDate, isOverdue } from '~/lib/date'
import { useColumnReorder } from '~/composables/useColumnReorder'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']
type MemberResponse = components['schemas']['MemberResponse']

const props = defineProps<{
  columns: ColumnResponse[]
  cardsByColumn: Map<string, CardResponse[]>
  projectId: string
  members?: MemberResponse[]
  loading?: boolean
  readonly?: boolean
}>()

const emit = defineEmits<{
  'card-click': [card: CardResponse]
  'add-card': []
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
}>()

const api = useApi()
const board = useBoardStore()
const toast = useAppToast()
const { search, type: filterType, assigneeUserId: filterAssignee, includeArchived, hideEmptyColumns } = useBoardFilters()

const showArchiveConfirm = ref(false)
const archiveTargetCard = ref<CardResponse | null>(null)
// Bulk selection (use board store)
const showBulkArchiveConfirm = ref(false)
const bulkTargetColumnId = ref<string | null>(null)
function toggleCardSelect(cardId: string, event?: Event) {
  event?.stopPropagation()
  board.toggleSelectCard(cardId)
}

function clearSelection() {
  board.clearSelection()
}

function confirmBulkArchive() {
  // open confirm dialog
  showBulkArchiveConfirm.value = true
}

async function moveSelectedToColumn() {
  // UI-only stub for bulk move. TODO: implement server-side batch move endpoint
  const ids = Object.keys(board.selectedCardIds).filter(k => (board.selectedCardIds as Record<string, boolean>)[k])
  toast.success(`Bulk move logged (TODO), ${ids}`)
}

async function archiveSelectedConfirmed() {
  // UI-only stub for bulk archive. TODO: implement server-side batch archive endpoint
  const ids = Object.keys(board.selectedCardIds).filter(k => (board.selectedCardIds as Record<string, boolean>)[k])
  toast.success(`Bulk archive logged (TODO), ${ids}`)
  showBulkArchiveConfirm.value = false
}

// Card three-dot menu state
const menuOpenFor = ref<string | null>(null)
const menuRef = ref<HTMLElement | null>(null)
const buttonRef = ref<HTMLElement | null>(null)

const { moveColumnLeft, moveColumnRight } = useColumnReorder(props.projectId)

const showMoveToColumn = ref<Record<string, boolean>>({})
const moveTargetCardId = ref<string | null>(null)

async function handleMoveUp(card: CardResponse) {
  const colCards = filteredCardsByColumn.value.get(card.columnId) ?? []
  const idx = colCards.findIndex(c => c.id === card.id)
  if (idx <= 0) return
  emit('card-move', card.id, card.columnId, idx - 1)
}

async function handleMoveDown(card: CardResponse) {
  const colCards = filteredCardsByColumn.value.get(card.columnId) ?? []
  const idx = colCards.findIndex(c => c.id === card.id)
  if (idx === -1 || idx >= colCards.length - 1) return
  emit('card-move', card.id, card.columnId, idx + 1)
}

function startMoveToColumn(cardId: string) {
  moveTargetCardId.value = cardId
  showMoveToColumn.value = { ...showMoveToColumn.value, [cardId]: true }
}

function moveToColumn(cardId: string, targetColumnId: string, card: CardResponse) {
  const targetPos = 0
  emit('card-move', cardId, targetColumnId, targetPos)
  showMoveToColumn.value = { ...showMoveToColumn.value, [cardId]: false }
  moveTargetCardId.value = null
}

// Function ref instead of string ref — avoids Vue 3 array-ref issue when ref is inside v-for
function setMenuRef(el: Element | null) {
  menuRef.value = el as HTMLElement | null
}

function openMenu(cardId: string) {
  menuOpenFor.value = menuOpenFor.value === cardId ? null : cardId
}

// close menu when click outside menu AND outside button (use click to avoid pointerdown race)
useEventListener(document, 'click', (e: Event) => {
  if (!menuOpenFor.value) return
  const target = e.target as Node | null
  if (!target) return
  const menuEl = menuRef.value
  const btnEl = buttonRef.value
  if (menuEl && menuEl.contains(target)) return
  if (btnEl && btnEl.contains(target)) return
  menuOpenFor.value = null
})

// focus first menu item when menu opens; close on Escape
watch(menuOpenFor, async (val) => {
  if (!val) return
  await nextTick()
  const first = menuRef.value?.querySelector('button') as HTMLElement | null
  first?.focus()
})

useEventListener(document, 'keydown', (e: KeyboardEvent) => {
  if (!menuOpenFor.value) return
  if (e.key === 'Escape' || e.key === 'Esc') {
    menuOpenFor.value = null
  }
})

// Accordion state
const expandedColumns = ref<Record<string, boolean>>({})

function toggleColumn(colId: string) {
  expandedColumns.value[colId] = !expandedColumns.value[colId]
}

function onHeaderClick(e: Event, colId: string) {
  // ignore clicks coming from interactive children (select, button, inputs)
  if ((e.target as Node) !== (e.currentTarget as Node)) return
  toggleColumn(colId)
}

// header toggle handled by dedicated button (avoids interfering with inner interactive controls)

// Global filter panel visibility
const showFilters = ref(false)

// Per-column type filter state
const columnTypeFilters = ref<Record<string, string | null>>({})
// Per-column archived-only filter state (null = show all, false = non-archived, true = archived only)
const columnArchivedFilters = ref<Record<string, boolean | null>>({})

function getColumnFilteredCards(colId: string, cards: CardResponse[]) {
  let filtered = cards

  if (search.value) {
    const q = search.value.toLowerCase()
    filtered = filtered.filter(c =>
      c.title.toLowerCase().includes(q)
      || String(c.cardNumber).includes(q)
    )
  }
  // c.type is number per generated types, but API returns string via JsonStringEnumConverter
  if (filterType.value !== null) {
    filtered = filtered.filter(c => String(c.type) === filterType.value)
  }
  if (filterAssignee.value) {
    filtered = filtered.filter(c => c.assignees.some(a => a.userId === filterAssignee.value))
  }

  // Per-column type filter
  // c.type is number per generated types, but API returns string via JsonStringEnumConverter
  const colType = columnTypeFilters.value[colId]
  if (colType !== undefined && colType !== null) {
    filtered = filtered.filter(c => String(c.type) === colType)
  }

  // Per-column archived filter
  const colArchived = columnArchivedFilters.value[colId]
  if (colArchived === false) {
    filtered = filtered.filter(c => !c.archivedAt)
  } else if (colArchived === true) {
    filtered = filtered.filter(c => c.archivedAt)
  }

  return filtered
}

const filteredCardsByColumn = computed(() => {
  const result = new Map<string, CardResponse[]>()
  for (const [colId, cards] of props.cardsByColumn) {
    result.set(colId, getColumnFilteredCards(colId, cards))
  }
  return result
})

const filteredColumns = computed(() => {
  if (!hideEmptyColumns.value) return props.columns
  return props.columns.filter(c => (filteredCardsByColumn.value.get(c.id)?.length ?? 0) > 0)
})

async function confirmArchive() {
  const card = archiveTargetCard.value
  if (!card) return
  const { error } = await api.POST(ApiRoutes.Cards.archive(props.projectId, card.id), {
    body: { version: card.version }
  })
  if (error) {
    toast.error('Failed to archive card')
  } else {
    board.removeCard(card.id)
    toast.success('Card archived')
  }
  archiveTargetCard.value = null
  menuOpenFor.value = null
}

async function handleRestore(card: CardResponse) {
  menuOpenFor.value = null
  const { error } = await api.POST(ApiRoutes.Cards.restore(props.projectId, card.id), {
    body: { version: card.version }
  })
  if (error) {
    toast.error('Failed to restore card')
  } else {
    board.fetchBoard(props.projectId)
    toast.success('Card restored')
  }
}

function handleArchive(card: CardResponse) {
  menuOpenFor.value = null
  archiveTargetCard.value = card
  showArchiveConfirm.value = true
}

function stripHtml(text: string): string {
  return text.replace(/<[^>]*>/g, '')
}
</script>

<template>
  <div>
    <!-- Global mobile bar -->
    <div class="flex items-center gap-2 px-4 py-2 border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900">
      <input
        v-model="search"
        placeholder="Search cards..."
        class="flex-1 px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
      >
      <UButton
        variant="ghost"
        size="sm"
        @click="showFilters = !showFilters"
      >
        Filter
      </UButton>
      <UButton
        v-if="!readonly"
        size="sm"
        type="button"
        @click="emit('add-card')"
      >
        <UIcon
          name="i-lucide-plus"
          class="mr-2 hidden sm:inline-block"
        />
        <span>Add card</span>
      </UButton>
    </div>

    <!-- Filter slide-out -->
    <div
      v-if="showFilters"
      class="flex flex-wrap gap-2 px-4 py-2 bg-gray-50 dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700"
    >
      <span class="text-xs text-gray-500 self-center">Type:</span>
      <select
        v-model="filterType"
        class="text-xs px-2 py-1 border rounded bg-white dark:bg-gray-800 dark:border-gray-600"
      >
        <option
          v-for="opt in CARD_TYPE_FILTER_OPTIONS"
          :key="opt.label"
          :value="opt.value"
        >
          {{ opt.label }}
        </option>
      </select>
      <select
        v-model="filterAssignee"
        class="text-xs px-2 py-1 border rounded bg-white dark:bg-gray-800 dark:border-gray-600"
      >
        <option :value="null">
          All assignees
        </option>
        <option
          v-for="m in members"
          :key="m.userId"
          :value="m.userId"
        >
          {{ m.username }}
        </option>
      </select>
      <label class="flex items-center gap-1 text-xs">
        <input
          v-model="includeArchived"
          type="checkbox"
          class="rounded"
        >
        Include archived
      </label>
      <label class="flex items-center gap-1 text-xs">
        <input
          v-model="hideEmptyColumns"
          type="checkbox"
          class="rounded"
        >
        Hide empty
      </label>
    </div>

    <!-- Bulk action bar (shared component) -->
    <BulkActionBar
      :selected-count="board.selectedCount"
      :bulk-target-column-id="bulkTargetColumnId"
      :columns="board.columns"
      @update:bulk-target-column-id="val => bulkTargetColumnId = val"
      @move="moveSelectedToColumn"
      @archive="confirmBulkArchive"
      @clear="clearSelection"
    />

    <!-- Columns as accordion -->
    <div class="relative p-4 space-y-2">
      <!-- Loading spinner -->
      <div
        v-if="loading"
        class="absolute inset-0 flex items-center justify-center bg-white/60 dark:bg-gray-900/60 rounded-lg z-10"
      >
        <UIcon
          name="i-lucide-loader"
          class="size-6 animate-spin"
        />
      </div>
      <div
        v-for="(column, colIdx) in filteredColumns"
        :key="column.id"
        class="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700"
      >
        <!-- Column header (accordion toggle) -->
        <div
          class="flex items-center justify-between px-3 py-2 cursor-pointer"
          @click="onHeaderClick($event, column.id)"
        >
          <div class="flex items-center gap-2">
            <div
              v-if="column.color"
              class="size-3 rounded-full shrink-0"
              :style="{ backgroundColor: column.color }"
            />
            <h3 class="text-sm font-semibold">
              {{ column.name }}
            </h3>
            <span class="text-xs text-gray-400 bg-gray-100 dark:bg-gray-700 rounded px-1.5 py-0.5">
              {{ filteredCardsByColumn.get(column.id)?.length ?? 0 }}
            </span>
            <span
              v-if="column.wipLimit && (filteredCardsByColumn.get(column.id)?.length ?? 0) >= Number(column.wipLimit)"
              class="text-xs text-red-500 font-medium"
            >
              WIP {{ column.wipLimit }}
            </span>
          </div>
          <div class="flex items-center gap-2 shrink-0">
            <button
              type="button"
              class="text-xs text-gray-400 hover:text-gray-600 disabled:opacity-30 disabled:cursor-not-allowed"
              title="Move column up"
              :disabled="colIdx === 0"
              @click.stop="moveColumnLeft(column.id)"
            >
              <UIcon name="i-lucide-chevron-up" class="size-3" />
            </button>
            <button
              type="button"
              class="text-xs text-gray-400 hover:text-gray-600 disabled:opacity-30 disabled:cursor-not-allowed"
              title="Move column down"
              :disabled="colIdx === filteredColumns.length - 1"
              @click.stop="moveColumnRight(column.id)"
            >
              <UIcon name="i-lucide-chevron-down" class="size-3" />
            </button>
            <!-- Type filter for this column -->
            <span class="text-xs text-gray-500 shrink-0">Type:</span>
            <select
              :value="columnTypeFilters[column.id] ?? ''"
              class="text-xs px-1.5 py-0.5 border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-700"
              @click.stop
              @change="columnTypeFilters[column.id] = ($event.target as HTMLSelectElement).value || null"
            >
              <option
                v-for="opt in CARD_TYPE_FILTER_OPTIONS"
                :key="opt.label"
                :value="opt.value ?? ''"
              >
                {{ opt.label }}
              </option>
            </select>
            <button
              type="button"
              class="text-xs text-gray-400"
              :aria-expanded="!!expandedColumns[column.id]"
              aria-label="Toggle column"
              @click="toggleColumn(column.id)"
              @keydown.enter.prevent="toggleColumn(column.id)"
              @keydown.space.prevent="toggleColumn(column.id)"
            >
              {{ expandedColumns[column.id] ? '▼' : '▶' }}
            </button>
          </div>
        </div>

        <!-- Expanded accordion content -->
        <div
          v-if="expandedColumns[column.id]"
          class="px-3 pb-3 space-y-3"
        >
          <template v-if="(filteredCardsByColumn.get(column.id) ?? []).length === 0">
            <div class="text-xs text-gray-500 p-3">
              No cards
            </div>
          </template>
          <template v-else>
            <div
              v-for="card in filteredCardsByColumn.get(column.id) ?? []"
              :key="card.id"
              class="bg-gray-50 dark:bg-gray-700 rounded p-3 cursor-pointer"
              role="button"
              tabindex="0"
              :aria-label="`Open card #${card.cardNumber} ${card.title}`"
              @click="emit('card-click', card)"
              @keydown.enter.prevent.stop="emit('card-click', card)"
              @keydown.space.prevent.stop="emit('card-click', card)"
            >
              <!-- Card header row -->
              <div class="flex items-center gap-2">
                <input
                  v-if="!readonly"
                  type="checkbox"
                  class="mr-2 shrink-0"
                  :checked="!!board.selectedCardIds[card.id]"
                  aria-label="Select card"
                  @click.stop="toggleCardSelect(card.id, $event)"
                  @keydown.stop.prevent="toggleCardSelect(card.id, $event)"
                >
                <span class="text-xs font-medium text-gray-500 shrink-0">#{{ card.cardNumber }}</span>
                <UIcon
                  :name="cardTypeOption(card.type).icon"
                  class="size-3.5 shrink-0"
                  :class="cardTypeColorClass(cardTypeOption(card.type))"
                />
                <span
                  class="text-xs font-medium shrink-0"
                  :class="cardTypeColorClass(cardTypeOption(card.type))"
                >
                  {{ cardTypeOption(card.type).label }}
                </span>
                <p class="text-sm font-medium truncate flex-1 min-w-0">
                  {{ card.title }}
                </p>
                <span
                  v-if="card.archivedAt"
                  class="text-xs text-gray-400 shrink-0"
                >archived</span>

                <!-- Relative wrapper anchors the absolute dropdown -->
                <div
                  v-if="!readonly"
                  class="relative"
                >
                  <!-- Three-dot menu button -->
                  <button
                    :ref="(el: Element | null) => { if (el) buttonRef = el as HTMLElement }"
                    type="button"
                    class="shrink-0 p-1 rounded hover:bg-gray-200 dark:hover:bg-gray-600"
                    aria-label="Card options"
                    :aria-expanded="menuOpenFor === card.id"
                    :aria-controls="`card-menu-${card.id}`"
                    @click.stop="openMenu(card.id)"
                    @keydown.down.prevent.stop="openMenu(card.id)"
                    @keydown.enter.prevent.stop="openMenu(card.id)"
                    @keydown.space.prevent.stop="openMenu(card.id)"
                  >
                    <UIcon
                      name="i-lucide-more-horizontal"
                      class="size-4"
                    />
                  </button>

                  <!-- Dropdown menu (only one renders since menuOpenFor is a single value) -->
                  <div
                    v-if="menuOpenFor === card.id"
                    :id="`card-menu-${card.id}`"
                    :ref="setMenuRef"
                    role="menu"
                    class="absolute right-0 z-20 mt-1 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-600 rounded-lg shadow-lg py-1 min-w-30"
                  >
                    <template v-if="card.archivedAt">
                      <button
                        role="menuitem"
                        tabindex="0"
                        class="w-full flex items-center gap-2 px-3 py-2 text-sm text-primary hover:bg-gray-100 dark:hover:bg-gray-700"
                        @click.stop="handleRestore(card)"
                        @keydown.enter.prevent.stop="handleRestore(card)"
                        @keydown.space.prevent.stop="handleRestore(card)"
                      >
                        <UIcon
                          name="i-lucide-archive-restore"
                          class="size-4"
                        />
                        Restore
                      </button>
                    </template>
                    <template v-else>
                      <button
                        role="menuitem"
                        tabindex="0"
                        class="w-full flex items-center gap-2 px-3 py-2 text-sm text-red-600 hover:bg-gray-100 dark:hover:bg-gray-700"
                        @click.stop="handleArchive(card)"
                        @keydown.enter.prevent.stop="handleArchive(card)"
                        @keydown.space.prevent.stop="handleArchive(card)"
                      >
                        <UIcon
                          name="i-lucide-archive"
                          class="size-4"
                        />
                        Archive
                      </button>
                      <USeparator class="my-1" />
                      <button
                        role="menuitem"
                        tabindex="0"
                        class="w-full flex items-center gap-2 px-3 py-2 text-sm hover:bg-gray-100 dark:hover:bg-gray-700"
                        @click.stop="handleMoveUp(card)"
                        @keydown.enter.prevent.stop="handleMoveUp(card)"
                      >
                        <UIcon name="i-lucide-chevron-up" class="size-4" />
                        Move up
                      </button>
                      <button
                        role="menuitem"
                        tabindex="0"
                        class="w-full flex items-center gap-2 px-3 py-2 text-sm hover:bg-gray-100 dark:hover:bg-gray-700"
                        @click.stop="handleMoveDown(card)"
                        @keydown.enter.prevent.stop="handleMoveDown(card)"
                      >
                        <UIcon name="i-lucide-chevron-down" class="size-4" />
                        Move down
                      </button>
                      <button
                        role="menuitem"
                        tabindex="0"
                        class="w-full flex items-center gap-2 px-3 py-2 text-sm hover:bg-gray-100 dark:hover:bg-gray-700"
                        @click.stop="startMoveToColumn(card.id)"
                        @keydown.enter.prevent.stop="startMoveToColumn(card.id)"
                      >
                        <UIcon name="i-lucide-arrow-right" class="size-4" />
                        Move to column...
                      </button>
                      <div v-if="showMoveToColumn[card.id]" class="border-t mt-1 pt-1">
                        <button
                          v-for="col in columns"
                          :key="col.id"
                          role="menuitem"
                          tabindex="0"
                          class="w-full flex items-center gap-2 px-3 py-1.5 text-xs hover:bg-gray-100 dark:hover:bg-gray-700"
                          :class="{ 'font-semibold': col.id === card.columnId }"
                          :disabled="col.id === card.columnId"
                          @click.stop="moveToColumn(card.id, col.id, card)"
                        >
                          <div v-if="col.color" class="size-2 rounded-full" :style="{ backgroundColor: col.color }" />
                          {{ col.name }}
                        </button>
                      </div>
                    </template>
                  </div>
                </div>
              </div>

              <p
                v-if="card.description"
                class="text-xs text-gray-500 mt-1 line-clamp-2"
              >
                {{ card.description ? stripHtml(card.description) : '' }}
              </p>
              <p
                v-if="formatDueDate(card.dueAt)"
                class="text-xs mt-1"
                :class="isOverdue(card.dueAt) ? 'text-red-500 font-medium' : 'text-gray-400'"
              >
                <UIcon
                  name="i-lucide-calendar"
                  class="size-3 inline mr-0.5"
                />
                {{ formatDueDate(card.dueAt) }}
              </p>
              <p
                v-if="card.parentCardId"
                class="text-xs mt-1 text-primary flex items-center gap-1"
              >
                <UIcon name="i-lucide-layers" class="size-3" />
                Epic
              </p>
            </div>
          </template>
        </div>
      </div>
    </div>

    <ConfirmDialog
      v-model:open="showArchiveConfirm"
      title="Archive card"
      :message="archiveTargetCard ? `Archive #${archiveTargetCard.cardNumber} ${archiveTargetCard.title}?` : ''"
      confirm-text="Archive"
      @confirm="confirmArchive"
    />

    <ConfirmDialog
      v-model:open="showBulkArchiveConfirm"
      title="Archive selected cards"
      :message="`Archive ${board.selectedCount} selected card(s)?`"
      confirm-text="Archive"
      @confirm="archiveSelectedConfirmed"
    />
  </div>
</template>
