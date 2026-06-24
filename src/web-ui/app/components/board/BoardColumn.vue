<script setup lang="ts">
import { VueDraggable } from 'vue-draggable-plus'
import type { SortableEvent } from 'sortablejs'
import type { components } from '~/types/api'

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
}>()

const isClient = import.meta.client
const localCards = ref([...props.cards])

watch(() => props.cards, (newCards) => {
  localCards.value = [...newCards]
}, { deep: true })

function onDragEnd(event: SortableEvent) {
  const newIndex = event.newIndex
  if (newIndex === undefined) return

  const cardId = localCards.value[newIndex]?.id
  if (!cardId) return

  const targetColumnId = (event.to as HTMLElement)?.dataset?.columnId
  if (!targetColumnId) return

  emit('card-move', cardId, targetColumnId, newIndex)
}
</script>

<template>
  <div class="flex flex-col bg-gray-50 dark:bg-gray-900 rounded-lg min-w-[280px] max-w-[320px] w-[300px] shrink-0">
    <ColumnHeader
      :column="column"
      :card-count="localCards.length"
    />

    <VueDraggable
      v-if="isClient"
      v-model="localCards"
      class="flex-1 p-2 space-y-2 min-h-[100px]"
      group="cards"
      item-key="id"
      :data-column-id="column.id"
      ghost-class="opacity-50"
      drag-class="rotate-2"
      @end="onDragEnd"
    >
      <template #item="{ element }">
        <BoardCard
          :card="element"
          @click="emit('card-click', element)"
        />
      </template>
    </VueDraggable>
    <div
      v-else
      class="flex-1 p-2 space-y-2 min-h-[100px]"
    >
      <BoardCard
        v-for="card in localCards"
        :key="card.id"
        :card="card"
        @click="emit('card-click', card)"
      />
    </div>
  </div>
</template>
