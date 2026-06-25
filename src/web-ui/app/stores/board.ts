import { defineStore } from 'pinia'
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']
type CardListResponse = components['schemas']['CardListResponse']

export interface BoardFilters {
  search: string
  type: number | null
  includeArchived: boolean
  hideEmptyColumns: boolean
}

export const useBoardStore = defineStore('board', () => {
  const project = ref<{ id: string, name: string } | null>(null)
  const columns = ref<ColumnResponse[]>([])
  const cardsByColumn = ref<Map<string, CardResponse[]>>(new Map())
  const loading = ref(false)
  const error = ref<string | null>(null)
  const boardFilters = ref<BoardFilters>({
    search: '',
    type: null,
    includeArchived: false,
    hideEmptyColumns: false
  })

  const api = useApi()

  async function fetchBoard(projectId: string, filters?: Partial<BoardFilters>) {
    loading.value = true
    error.value = null
    if (filters) {
      Object.assign(boardFilters.value, filters)
    }
    try {
      const cardsUrl = ApiRoutes.Cards.list(projectId)
      const searchParams = new URLSearchParams()
      if (boardFilters.value.includeArchived) searchParams.set('includeArchived', 'true')
      if (boardFilters.value.type !== null) searchParams.set('type', String(boardFilters.value.type))
      if (boardFilters.value.search) searchParams.set('search', boardFilters.value.search)
      const cardsUrlWithParams = searchParams.size > 0 ? `${cardsUrl}?${searchParams}` : cardsUrl

      const [columnsResult, cardsResult] = await Promise.all([
        api.GET(ApiRoutes.Columns.list(projectId)),
        api.GET(cardsUrlWithParams)
      ])

      if (columnsResult.error) throw columnsResult.error
      if (cardsResult.error) throw cardsResult.error

      columns.value = (columnsResult.data as ColumnResponse[]) ?? []

      const cardList = cardsResult.data as CardListResponse
      const cards = cardList?.cards ?? []

      const map = new Map<string, CardResponse[]>()
      for (const col of columns.value) {
        map.set(col.id, [])
      }
      for (const card of cards) {
        const colCards = map.get(card.columnId) ?? []
        colCards.push(card)
        map.set(card.columnId, colCards)
      }
      cardsByColumn.value = map
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to load board'
    } finally {
      loading.value = false
    }
  }

  function moveCard(cardId: string, targetColumnId: string, targetPosition: number) {
    let card: CardResponse | undefined
    for (const [, cards] of cardsByColumn.value) {
      const idx = cards.findIndex((c: CardResponse) => c.id === cardId)
      if (idx !== -1) {
        card = cards[idx]
        cards.splice(idx, 1)
        break
      }
    }
    if (!card) return

    const targetCards = cardsByColumn.value.get(targetColumnId) ?? []
    targetCards.splice(targetPosition, 0, card)
    cardsByColumn.value.set(targetColumnId, targetCards)
  }

  function rollbackMove(projectId: string) {
    // Re-fetch board on rollback
    fetchBoard(projectId)
  }

  function addCard(columnId: string, card: CardResponse) {
    const cards = cardsByColumn.value.get(columnId) ?? []
    cards.push(card)
    cardsByColumn.value.set(columnId, cards)
  }

  function updateCard(cardId: string, updates: Partial<CardResponse>) {
    for (const [, cards] of cardsByColumn.value) {
      const card = cards.find((c: CardResponse) => c.id === cardId)
      if (card) {
        Object.assign(card, updates)
        break
      }
    }
  }

  function removeCard(cardId: string) {
    for (const [, cards] of cardsByColumn.value) {
      const idx = cards.findIndex((c: CardResponse) => c.id === cardId)
      if (idx !== -1) {
        cards.splice(idx, 1)
        break
      }
    }
  }

  const visibleColumns = computed(() => {
    if (!boardFilters.value.hideEmptyColumns) return columns.value
    const colIdsWithCards = new Set<string>()
    for (const [colId, cards] of cardsByColumn.value) {
      if (cards.length > 0) colIdsWithCards.add(colId)
    }
    return columns.value.filter(c => colIdsWithCards.has(c.id))
  })

  return {
    project, columns, cardsByColumn, loading, error,
    fetchBoard, moveCard, rollbackMove, addCard, updateCard, removeCard,
    boardFilters, visibleColumns
  }
})
