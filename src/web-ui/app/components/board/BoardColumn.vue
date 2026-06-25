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
}>()

const emit = defineEmits<{
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
  'card-click': [card: CardResponse]
  'add-card': [columnId: string]
}>()

// Per-column filter state
const columnSearch = ref('')
const columnType = ref<number | null>(null)
const columnArchived = ref(false)

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

  // Column archived filter: unchecked = show non-archived only; checked = show archived only
  if (columnArchived.value) {
    result = result.filter(c => c.archivedAt)
  } else {
    result = result.filter(c => !c.archivedAt)
  }

  return result
})

function handleFilterType(value: number | null) {
  columnType.value = value
}

function handleFilterArchived(value: boolean) {
  columnArchived.value = value
}
</script>

<template>
  <div class="flex flex-col bg-gray-50 dark:bg-gray-900 rounded-lg min-w-[280px] max-w-[320px] w-[300px] shrink-0">
    <ColumnHeader
      :column="column"
      :card-count="filteredCards.length"
      @add-card="emit('add-card', column.id)"
      @filter-type="handleFilterType"
      @filter-archived="handleFilterArchived"
    >
      <template #filter-row>
        <input
          v-model="columnSearch"
          placeholder="Filter cards in this column..."
          class="w-full mt-1 px-2 py-1 text-xs border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800 focus:outline-none focus:ring-1 focus:ring-primary"
        >
      </template>
    </ColumnHeader>

    <div class="flex-1 p-2 space-y-2 min-h-[100px]">
      <BoardCard
        v-for="card in filteredCards"
        :key="card.id"
        :card="card"
        :project-id="projectId"
        @click="emit('card-click', card)"
      />
    </div>
  </div>
</template>
