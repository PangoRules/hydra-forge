<script setup lang="ts">
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']

defineProps<{
  columns: ColumnResponse[]
  cardsByColumn: Map<string, CardResponse[]>
  projectId: string
}>()

const emit = defineEmits<{
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
  'card-click': [card: CardResponse]
}>()

function handleCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  emit('card-move', cardId, targetColumnId, targetPosition)
}

function handleCardClick(card: CardResponse) {
  emit('card-click', card)
}
</script>

<template>
  <div class="flex gap-4 overflow-x-auto pb-4 h-full">
    <BoardColumn
      v-for="col in columns"
      :key="col.id"
      :column="col"
      :cards="cardsByColumn.get(col.id) ?? []"
      :project-id="projectId"
      @card-move="handleCardMove"
      @card-click="handleCardClick"
    />
  </div>
</template>
