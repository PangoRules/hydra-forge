<script setup lang="ts">
import type { components } from '~/types/api'
import ColumnHeader from '~/components/board/ColumnHeader.vue'
import BoardCard from '~/components/board/BoardCard.vue'

type CardResponse = components['schemas']['CardResponse']
type ColumnResponse = components['schemas']['ColumnResponse']

const props = defineProps<{
  column: ColumnResponse
  cards: CardResponse[]
  projectId: string
  includeArchived: boolean
  readonly?: boolean
}>()

const emit = defineEmits<{
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
  'card-click': [card: CardResponse]
  'add-card': [columnId: string]
  'reorder': [draggedColumnId: string, targetColumnId: string]
}>()

// Per-column filter state
// columnArchived: null = show all (respect server fetch), false = non-archived only, true = archived only
const columnSearch = ref('')
const columnType = ref<number | null>(null)
const columnArchived = ref<boolean | null>(null)

const isDragOver = ref(false)

const filteredCards = computed(() => {
  let result = props.cards

  // Column text search
  if (columnSearch.value) {
    const q = columnSearch.value.toLowerCase()
    result = result.filter(c =>
      c.title.toLowerCase().includes(q)
      || String(c.cardNumber).includes(q)
    )
  }

  // Column type filter
  if (columnType.value !== null) {
    result = result.filter(c => c.type === columnType.value)
  }

  // Column archived filter: null = show all, false = non-archived, true = archived only
  if (columnArchived.value === false) {
    result = result.filter(c => !c.archivedAt)
  } else if (columnArchived.value === true) {
    result = result.filter(c => c.archivedAt)
  }

  return result
})

function handleFilterType(value: number | null) {
  columnType.value = value
}

function handleFilterArchived(value: boolean) {
  // false = reset to show all (respect server fetch); true = show archived only
  columnArchived.value = value ? true : null
}

function handleDragOver(event: DragEvent) {
  event.preventDefault()
  event.dataTransfer!.dropEffect = 'move'
  isDragOver.value = true
}

function handleDragLeave() {
  isDragOver.value = false
}

function handleDrop(event: DragEvent) {
  event.preventDefault()
  isDragOver.value = false
  const cardId = event.dataTransfer!.getData('text/plain')
  if (!cardId) return
  emit('card-move', cardId, props.column.id, filteredCards.value.length)
}
</script>

<template>
  <div class="flex flex-col bg-gray-50 dark:bg-gray-900 rounded-lg min-w-[320px] max-w-[360px] w-[340px] min-h-0 shrink-0">
    <ColumnHeader
      :column="column"
      :card-count="filteredCards.length"
      :include-archived="includeArchived"
      :readonly="readonly"
      @add-card="emit('add-card', column.id)"
      @filter-type="handleFilterType"
      @filter-archived="handleFilterArchived"
      @reorder="(a: string, b: string) => emit('reorder', a, b)"
    >
      <template #filter-row>
        <input
          v-model="columnSearch"
          placeholder="Filter cards in this column..."
          class="w-full mt-1 px-2 py-1 text-xs border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800 focus:outline-none focus:ring-1 focus:ring-primary"
        >
      </template>
    </ColumnHeader>

    <div class="flex-1 relative min-h-0">
      <div
        class="absolute inset-0 overflow-y-auto p-2 space-y-2"
        :class="{ 'ring-2 ring-primary/30': isDragOver }"
        @dragover.prevent="handleDragOver"
        @dragleave="handleDragLeave"
        @drop.prevent="handleDrop"
      >
        <BoardCard
          v-for="card in filteredCards"
          :key="card.id"
          :card="card"
          :project-id="projectId"
          :readonly="readonly"
          @click="emit('card-click', card)"
        />
      </div>
    </div>
  </div>
</template>
