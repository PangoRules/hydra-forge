<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import { useEventListener } from '@vueuse/core'
import { nextTick, watch } from 'vue'
import { CARD_TYPE_ICONS, CARD_TYPE_OPTIONS } from '~/lib/card-type'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']
type MemberResponse = components['schemas']['MemberResponse']

const props = defineProps<{
  columns: ColumnResponse[]
  cardsByColumn: Map<string, CardResponse[]>
  projectId: string
  members?: MemberResponse[]
  loading?: boolean
}>()

const emit = defineEmits<{
  'card-click': [card: CardResponse]
  'add-card': []
}>()

const api = useApi()
const board = useBoardStore()
const toast = useToast()
const { search, type: filterType, assigneeUserId: filterAssignee, includeArchived, hideEmptyColumns } = useBoardFilters()

const showArchiveConfirm = ref(false)
const archiveTargetCard = ref<CardResponse | null>(null)

// Card three-dot menu state
const menuOpenFor = ref<string | null>(null)
const menuRef = ref<HTMLElement | null>(null)
const buttonRef = ref<HTMLElement | null>(null)

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

// Global filter panel visibility
const showFilters = ref(false)

// Per-column type filter state
const columnTypeFilters = ref<Record<string, number | null>>({})
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
  if (filterType.value !== null) {
    filtered = filtered.filter(c => c.type === filterType.value)
  }
  if (filterAssignee.value) {
    filtered = filtered.filter(c => c.assignees.some(a => a.userId === filterAssignee.value))
  }

  // Per-column type filter
  const colType = columnTypeFilters.value[colId]
  if (colType !== undefined && colType !== null) {
    filtered = filtered.filter(c => c.type === colType)
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
    toast.add({ title: 'Failed to archive card', color: 'error' })
  } else {
    board.removeCard(card.id)
    toast.add({ title: 'Card archived', color: 'success' })
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
    toast.add({ title: 'Failed to restore card', color: 'error' })
  } else {
    board.fetchBoard(props.projectId)
    toast.add({ title: 'Card restored', color: 'success' })
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
        size="sm"
        icon="i-lucide-plus"
        @click="emit('add-card')"
      />
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
          v-for="opt in CARD_TYPE_OPTIONS"
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
        v-for="column in filteredColumns"
        :key="column.id"
        class="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700"
      >
        <!-- Column header (accordion toggle) -->
        <div
          class="flex items-center justify-between px-3 py-2 cursor-pointer"
          @click="toggleColumn(column.id)"
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
            <!-- Type filter for this column -->
            <span class="text-xs text-gray-500 shrink-0">Type:</span>
            <select
              :value="columnTypeFilters[column.id] ?? ''"
              class="text-xs px-1.5 py-0.5 border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-700"
              @click.stop
              @change="columnTypeFilters[column.id] = ($event.target as HTMLSelectElement).value !== '' ? Number(($event.target as HTMLSelectElement).value) : null"
            >
              <option
                v-for="opt in CARD_TYPE_OPTIONS"
                :key="opt.label"
                :value="opt.value ?? ''"
              >
                {{ opt.label }}
              </option>
            </select>
            <span class="text-xs text-gray-400">{{ expandedColumns[column.id] ? '▼' : '▶' }}</span>
          </div>
        </div>

        <!-- Expanded accordion content -->
        <div
          v-if="expandedColumns[column.id]"
          class="px-3 pb-3 space-y-3"
        >
          <div
            v-for="card in filteredCardsByColumn.get(column.id) ?? []"
            :key="card.id"
            class="bg-gray-50 dark:bg-gray-700 rounded p-3 cursor-pointer"
            @click="emit('card-click', card)"
          >
            <!-- Card header row -->
            <div class="flex items-center gap-2">
              <UIcon
                :name="CARD_TYPE_ICONS[card.type] ?? 'i-lucide-square'"
                class="size-4 shrink-0 text-gray-400"
              />
              <span class="text-xs font-medium text-gray-500">#{{ card.cardNumber }}</span>
              <p class="text-sm font-medium truncate flex-1 min-w-0">
                {{ card.title }}
              </p>
              <span
                v-if="card.archivedAt"
                class="text-xs text-gray-400 shrink-0"
              >archived</span>

              <!-- Relative wrapper anchors the absolute dropdown -->
              <div class="relative">
                <!-- Three-dot menu button -->
                <button
                  :ref="(el: Element | null) => { if (el) buttonRef = el as HTMLElement }"
                  class="shrink-0 p-1 rounded hover:bg-gray-200 dark:hover:bg-gray-600"
                  aria-label="Card options"
                  :aria-expanded="menuOpenFor === card.id"
                  :aria-controls="`card-menu-${card.id}`"
                  @click.stop="openMenu(card.id)"
                  @keydown.down.prevent="openMenu(card.id)"
                  @keydown.enter.prevent="openMenu(card.id)"
                >
                  <UIcon
                    name="i-lucide-more-horizontal"
                    class="size-4"
                  />
                </button>

                <!-- Dropdown menu (only one renders since menuOpenFor is a single value) -->
                <div
                  v-if="menuOpenFor === card.id"
                  :ref="setMenuRef"
                  :id="`card-menu-${card.id}`"
                  role="menu"
                  class="absolute right-0 z-20 mt-1 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-600 rounded-lg shadow-lg py-1 min-w-30"
                >
                  <template v-if="card.archivedAt">
                    <button
                      role="menuitem"
                      tabindex="0"
                      class="w-full flex items-center gap-2 px-3 py-2 text-sm text-primary hover:bg-gray-100 dark:hover:bg-gray-700"
                      @click="handleRestore(card)"
                      @keydown.enter.prevent="handleRestore(card)"
                      @keydown.space.prevent="handleRestore(card)"
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
                      @click="handleArchive(card)"
                      @keydown.enter.prevent="handleArchive(card)"
                      @keydown.space.prevent="handleArchive(card)"
                    >
                      <UIcon
                        name="i-lucide-archive"
                        class="size-4"
                      />
                      Archive
                    </button>
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
          </div>
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
  </div>
</template>
