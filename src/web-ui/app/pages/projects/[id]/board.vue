<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'

definePageMeta({ middleware: ['auth'] })

type CardResponse = components['schemas']['CardResponse']

const route = useRoute()
const projectId = route.params.id as string
const board = useBoardStore()
const api = useApi()
const toast = useToast()

const projectName = ref('')

const showCardModal = ref(false)
const selectedCard = ref<CardResponse | null>(null)
const selectedCardId = ref<string | null>(null)

async function handleCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  const card = findCard(cardId)
  if (!card) return

  board.moveCard(cardId, targetColumnId, targetPosition)

  const result = await api.POST(ApiRoutes.Cards.move(projectId, cardId), {
    body: {
      targetColumnId,
      targetPosition,
      confirmBlockedMove: false,
      version: card.version
    }
  })

  if (result.error) {
    board.rollbackMove(projectId)
    const message = result.error instanceof Error ? result.error.message : 'Failed to move card'
    toast.add({ title: message, color: 'error' })
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
  selectedCardId.value = card.id
  showCardModal.value = true
}

onMounted(async () => {
  board.fetchBoard(projectId)
  const { data } = await api.GET(ApiRoutes.Projects.detail(projectId))
  if (data) {
    projectName.value = (data as components['schemas']['ProjectResponse']).name
  }
})
</script>

<template>
  <div class="h-full flex flex-col">
    <div class="flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-gray-700">
      <h1 class="text-xl font-bold truncate">
        {{ projectName || 'Board' }}
      </h1>
      <UButton
        variant="ghost"
        size="sm"
        @click="board.fetchBoard(projectId)"
      >
        <UIcon
          name="i-lucide-refresh-cw"
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
      <BoardView
        :columns="board.columns"
        :cards-by-column="board.cardsByColumn"
        :project-id="projectId"
        class="hidden md:flex"
        @card-move="handleCardMove"
        @card-click="handleCardClick"
      />
      <BoardMobileList
        :columns="board.columns"
        :cards-by-column="board.cardsByColumn"
        :project-id="projectId"
        class="md:hidden"
        @card-click="handleCardClick"
      />
    </div>

    <CardModal
      v-if="selectedCardId"
      :card-id="selectedCardId"
      :project-id="projectId"
      @close="selectedCardId = null"
      @archived="board.fetchBoard(projectId)"
      @restored="board.fetchBoard(projectId)"
    />
  </div>
</template>
