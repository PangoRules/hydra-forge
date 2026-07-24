import { useBoardStore } from '~/stores/board'
import { ApiRoutes } from '~/lib/routes'
import { ApiError } from '~/lib/api-error'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

export function useCardMove(projectId: string) {
  const board = useBoardStore()
  const api = useApi()
  const toast = useAppToast()
  const isMoving = ref(false)

  async function moveCardToColumn(cardId: string, targetColumnId: string, targetPosition: number) {
    let card: CardResponse | undefined
    for (const [, cards] of board.cardsByColumn) {
      card = cards.find(c => c.id === cardId)
      if (card) break
    }
    if (!card) return

    board.moveCard(cardId, targetColumnId, targetPosition)
    isMoving.value = true

    try {
      const { data } = await api.POST(ApiRoutes.Cards.move(projectId, cardId), {
        body: {
          targetColumnId,
          targetPosition,
          confirmBlockedMove: false,
          version: card.version
        }
      })
      if (data) {
        board.updateCard(cardId, data as CardResponse)
      }
    } catch (error: unknown) {
      board.rollbackMove(projectId)
      if (error instanceof ApiError && error.status === 409) {
        toast.error('Cannot move blocked card')
      } else {
        toast.error('Failed to move card')
      }
    } finally {
      isMoving.value = false
    }
  }

  return { moveCardToColumn, isMoving }
}
