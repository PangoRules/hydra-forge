<script setup lang="ts">
import type { components } from '~/types/api'
import BoardColumn from '~/components/board/BoardColumn.vue'
import { useColumnReorder } from '~/composables/useColumnReorder'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  columns: ColumnResponse[]
  cardsByColumn: Map<string, CardResponse[]>
  projectId: string
  includeArchived: boolean
  readonly?: boolean
}>()

const emit = defineEmits<{
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
  'card-click': [card: CardResponse]
  'add-card': [columnId: string]
}>()

const { reorderColumns, moveColumnLeft, moveColumnRight } = useColumnReorder(props.projectId)

function handleCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  emit('card-move', cardId, targetColumnId, targetPosition)
}

function handleCardClick(card: CardResponse) {
  emit('card-click', card)
}
</script>

<template>
  <div class="flex gap-4 pb-4 flex-1 min-h-0">
    <BoardColumn
      v-for="(col, idx) in columns"
      :key="col.id"
      :column="col"
      :cards="cardsByColumn.get(col.id) ?? []"
      :project-id="projectId"
      :include-archived="includeArchived"
      :readonly="readonly"
      :can-move-left="idx > 0"
      :can-move-right="idx < columns.length - 1"
      @card-move="handleCardMove"
      @card-click="handleCardClick"
      @add-card="(colId: string) => emit('add-card', colId)"
      @reorder="reorderColumns"
      @move-left="() => moveColumnLeft(col.id)"
      @move-right="() => moveColumnRight(col.id)"
    />
  </div>
</template>
