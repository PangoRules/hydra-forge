<script setup lang="ts">
import type { components } from '~/types/api'
import ColumnHeader from '~/components/board/ColumnHeader.vue'
import BoardCard from '~/components/board/BoardCard.vue'

type CardResponse = components['schemas']['CardResponse']
type ColumnResponse = components['schemas']['ColumnResponse']

defineProps<{
  column: ColumnResponse
  cards: CardResponse[]
  projectId: string
}>()

const emit = defineEmits<{
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
  'card-click': [card: CardResponse]
}>()
</script>

<template>
  <div class="flex flex-col bg-gray-50 dark:bg-gray-900 rounded-lg min-w-[280px] max-w-[320px] w-[300px] shrink-0">
    <ColumnHeader
      :column="column"
      :card-count="cards.length"
    />

    <div class="flex-1 p-2 space-y-2 min-h-[100px]">
      <BoardCard
        v-for="card in cards"
        :key="card.id"
        :card="card"
        :project-id="projectId"
        @click="emit('card-click', card)"
      />
    </div>
  </div>
</template>
