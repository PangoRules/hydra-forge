<script setup lang="ts">
import { VueDraggable } from 'vue-draggable-plus'
import type { SortableEvent } from 'sortablejs'
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  columns: ColumnResponse[]
  cardsByColumn: Map<string, CardResponse[]>
  projectId: string
}>()

const emit = defineEmits<{
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
  'card-click': [card: CardResponse]
}>()

const localColumns = ref([...props.columns])

watch(() => props.columns, (newColumns) => {
  localColumns.value = [...newColumns]
}, { deep: true })

function onColumnDragEnd(event: SortableEvent) {
  const oldIndex = event.oldIndex
  const newIndex = event.newIndex
  if (oldIndex === undefined || newIndex === undefined) return

  const moved = localColumns.value.splice(oldIndex, 1)[0]
  if (!moved) return
  localColumns.value.splice(newIndex, 0, moved)
}

function handleCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  emit('card-move', cardId, targetColumnId, targetPosition)
}

function handleCardClick(card: CardResponse) {
  emit('card-click', card)
}
</script>

<template>
  <VueDraggable
    v-model="localColumns"
    class="flex gap-4 overflow-x-auto pb-4 h-full"
    group="columns"
    item-key="id"
    handle=".column-drag-handle"
    ghost-class="opacity-50"
    @end="onColumnDragEnd"
  >
    <template #item="{ element }">
      <BoardColumn
        :column="element"
        :cards="cardsByColumn.get(element!.id) ?? []"
        :project-id="projectId"
        @card-move="handleCardMove"
        @card-click="handleCardClick"
      />
    </template>
  </VueDraggable>
</template>
