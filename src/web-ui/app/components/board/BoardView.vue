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
  'move-left': [columnId: string]
  'move-right': [columnId: string]
}>()

const api = useApi()
const boardStore = useBoardStore()

function handleCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  emit('card-move', cardId, targetColumnId, targetPosition)
}

function handleCardClick(card: CardResponse) {
  emit('card-click', card)
}

async function handleColumnReorder(draggedColumnId: string, targetColumnId: string, insertBefore: boolean = true) {
  const currentColumns = boardStore.visibleColumns
  const newOrder = [...currentColumns]
  const draggedIdx = newOrder.findIndex(c => c.id === draggedColumnId)
  if (draggedIdx === -1) return
  const [dragged] = newOrder.splice(draggedIdx, 1)
  if (!dragged) return
  const targetIdx = newOrder.findIndex(c => c.id === targetColumnId)
  if (targetIdx === -1) return

  if (insertBefore) {
    newOrder.splice(targetIdx, 0, dragged)
  } else {
    newOrder.splice(targetIdx + 1, 0, dragged)
  }

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

async function handleMoveLeft(columnId: string) {
  const currentColumns = boardStore.visibleColumns
  const idx = currentColumns.findIndex(c => c.id === columnId)
  if (idx <= 0) return
  const newOrder = [...currentColumns]
  const left = newOrder[idx - 1]!
  const right = newOrder[idx]!
  newOrder[idx - 1] = right
  newOrder[idx] = left
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

async function handleMoveRight(columnId: string) {
  const currentColumns = boardStore.visibleColumns
  const idx = currentColumns.findIndex(c => c.id === columnId)
  if (idx === -1 || idx >= currentColumns.length - 1) return
  const newOrder = [...currentColumns]
  const left = newOrder[idx]!
  const right = newOrder[idx + 1]!
  newOrder[idx] = right
  newOrder[idx + 1] = left
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
      @reorder="handleColumnReorder"
      @move-left="handleMoveLeft(col.id)"
      @move-right="handleMoveRight(col.id)"
    />
  </div>
</template>
