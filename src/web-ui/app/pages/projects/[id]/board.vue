<script setup lang="ts">
import type { components } from '~/types/api'

definePageMeta({ middleware: ['auth'] })

type CardResponse = components['schemas']['CardResponse']

const route = useRoute()
const projectId = route.params.id as string
const board = useBoardStore()
const api = useApi()

const showCardModal = ref(false)
const selectedCard = ref<CardResponse | null>(null)

async function handleCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  const card = findCard(cardId)
  if (!card) return

  board.moveCard(cardId, targetColumnId, targetPosition)

  const result = await api.POST(`/api/projects/{projectId}/Cards/{cardId}/move`, {
    params: { path: { projectId, cardId } },
    body: {
      targetColumnId,
      targetPosition,
      confirmBlockedMove: false,
      version: card.version
    }
  })

  if (result.error) {
    board.rollbackMove(projectId)
    return
  }
}

function findCard(cardId: string): CardResponse | undefined {
  for (const [, cards] of board.cardsByColumn) {
    const found = cards.find(c => c.id === cardId)
    if (found) return found
  }
  return undefined
}

function handleCardClick(card: CardResponse) {
  selectedCard.value = card
  showCardModal.value = true
}

onMounted(() => {
  board.fetchBoard(projectId)
})
</script>

<template>
  <div class="h-full flex flex-col">
    <div class="flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-gray-700">
      <h1 class="text-xl font-bold">
        Board
      </h1>
      <UButton
        variant="ghost"
        size="sm"
        @click="board.fetchBoard(projectId)"
      >
        <UIcon
          name="i-lucide-refresh"
          class="size-4"
        />
      </UButton>
    </div>

    <div
      v-if="board.loading"
      class="flex-1 flex items-center justify-center"
    >
      <UIcon
        name="i-lucide-loader"
        class="size-8 animate-spin"
      />
    </div>

    <div
      v-else-if="board.error"
      class="flex-1 flex items-center justify-center"
    >
      <div class="text-center">
        <p class="text-red-500 mb-2">
          {{ board.error }}
        </p>
        <UButton
          variant="outline"
          size="sm"
          @click="board.fetchBoard(projectId)"
        >
          Retry
        </UButton>
      </div>
    </div>

    <div
      v-else
      class="flex-1 overflow-x-auto p-4"
    >
      <ClientOnly>
        <BoardView
          v-if="board.columns.length > 0"
          :columns="board.columns"
          :cards-by-column="board.cardsByColumn"
          :project-id="projectId"
          class="hidden md:flex"
          @card-move="handleCardMove"
          @card-click="handleCardClick"
        />
      </ClientOnly>
      <BoardMobileList
        :columns="board.columns"
        :cards-by-column="board.cardsByColumn"
        class="md:hidden"
        @card-click="handleCardClick"
      />
    </div>
  </div>
</template>
