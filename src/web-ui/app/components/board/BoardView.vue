<script setup lang="ts">
import type { components } from '~/types/api'
import BoardColumn from '~/components/board/BoardColumn.vue'
import { useApi } from '~/composables/useApi'
import { useBoardStore } from '~/stores/board'
import { ApiRoutes } from '~/lib/routes'

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

const api = useApi()
const boardStore = useBoardStore()

function handleCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  emit('card-move', cardId, targetColumnId, targetPosition)
}

function handleCardClick(card: CardResponse) {
  emit('card-click', card)
}

async function handleColumnReorder(draggedColumnId: string, targetColumnId: string) {
  const currentColumns = boardStore.visibleColumns
  const draggedIdx = currentColumns.findIndex(c => c.id === draggedColumnId)
  if (draggedIdx === -1) return

  const newOrder = [...currentColumns]
  const [dragged] = newOrder.splice(draggedIdx, 1)
  if (!dragged) return
  const targetIdx = newOrder.findIndex(c => c.id === targetColumnId)
  if (targetIdx === -1) return
  newOrder.splice(targetIdx, 0, dragged)

  try {
    await api.PUT(ApiRoutes.Columns.reorder(props.projectId), {
      body: { columnIds: newOrder.map(c => c.id) }
    })
    boardStore.setColumnOrder(newOrder)
  } catch (e: unknown) {
    const message = e instanceof Error ? e.message : 'Failed to reorder columns'
    const toast = useToast()
    toast.add({ title: message, color: 'error' })
  }
}
</script>

<template>
  <div class="flex gap-4 pb-4 h-full">
    <BoardColumn
      v-for="col in columns"
      :key="col.id"
      :column="col"
      :cards="cardsByColumn.get(col.id) ?? []"
      :project-id="projectId"
      :include-archived="includeArchived"
      :readonly="readonly"
      @card-move="handleCardMove"
      @card-click="handleCardClick"
      @add-card="(colId: string) => emit('add-card', colId)"
      @reorder="handleColumnReorder"
    />
  </div>
</template>
