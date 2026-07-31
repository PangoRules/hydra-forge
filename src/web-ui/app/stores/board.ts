import { defineStore } from 'pinia'
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']
type CardListResponse = components['schemas']['CardListResponse']
type MemberResponse = components['schemas']['MemberResponse']

export interface BoardFilters {
  search: string
  includeArchived: boolean
  hideEmptyColumns: boolean
  assigneeUserId: string | null
  visibleColumnIds: string[]
}

export const useBoardStore = defineStore('board', () => {
  const project = ref<{ id: string, name: string } | null>(null)
  const columns = ref<ColumnResponse[]>([])
  const cardsByColumn = ref<Map<string, CardResponse[]>>(new Map())
  const loading = ref(false)
  const error = ref<string | null>(null)
  const boardFilters = ref<BoardFilters>({
    search: '',
    includeArchived: false,
    hideEmptyColumns: false,
    assigneeUserId: null,
    visibleColumnIds: []
  })

  const members = ref<MemberResponse[]>([])
  const selectedCardIds = ref<Record<string, boolean>>({})

  // Bridge for CardModal (and any other open-card UI) to react to realtime events without
  // owning its own SignalR connection — same pattern presence already uses (a shared
  // reactive store written by useRealtime.ts, read by whoever has that card open).
  const cardContentEvent = ref<{ cardId: string, entityType: string, action: string } | null>(null)
  function signalCardContentEvent(cardId: string, entityType: string, action: string) {
    cardContentEvent.value = { cardId, entityType, action }
  }

  const selectedCount = computed(() => Object.values(selectedCardIds.value).filter(Boolean).length)

  function toggleSelectCard(cardId: string) {
    // assign new object so Vue reactivity detects property change
    selectedCardIds.value = { ...selectedCardIds.value, [cardId]: !selectedCardIds.value[cardId] }
  }

  function clearSelection() {
    selectedCardIds.value = {}
  }

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
      // When includeArchived is true, fetch archived (up to 200) so per-column
      // "archived only" filtering works. When false, skip archived entirely.
      if (boardFilters.value.includeArchived) {
        searchParams.set('includeArchived', 'true')
        searchParams.set('archivedLimit', '200')
      }
      if (boardFilters.value.search) searchParams.set('search', boardFilters.value.search)
      if (boardFilters.value.assigneeUserId) searchParams.set('assigneeUserId', boardFilters.value.assigneeUserId)
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

  function setColumnOrder(newOrder: ColumnResponse[]) {
    columns.value = newOrder
  }

  function addColumn(column: ColumnResponse) {
    columns.value = [...columns.value, column]
  }

  function updateColumnInStore(columnId: string, updates: Partial<ColumnResponse>) {
    columns.value = columns.value.map(c => (c.id === columnId ? { ...c, ...updates } : c))
  }

  function removeColumnFromStore(columnId: string) {
    columns.value = columns.value.filter(c => c.id !== columnId)
    cardsByColumn.value.delete(columnId)
  }

  const visibleColumns = computed(() => {
    if (boardFilters.value.visibleColumnIds.length > 0) {
      return columns.value.filter(c => boardFilters.value.visibleColumnIds.includes(c.id))
    }
    if (!boardFilters.value.hideEmptyColumns) return columns.value
    const colIdsWithCards = new Set<string>()
    for (const [colId, cards] of cardsByColumn.value) {
      if (cards.length > 0) colIdsWithCards.add(colId)
    }
    return columns.value.filter(c => colIdsWithCards.has(c.id))
  })

  // Realtime board patching — routes a BoardHub event to a single-entity re-fetch +
  // targeted store mutation instead of a full fetchBoard(), so the rest of the board
  // doesn't blink on every remote card/column change. Created/Deleted/Archived/Restored
  // stay on the full-refresh fallback: whether a card should even be in view depends on
  // the current filter set (includeArchived etc.), and getting that filter check right
  // client-side isn't worth it for these comparatively rare transitions.
  async function applyRealtimeCardEvent(projectId: string, cardId: string, action: string) {
    if (action === 'Created' || action === 'Deleted' || action === 'Archived' || action === 'Restored') {
      await fetchBoard(projectId)
      return
    }

    // Best-effort background sync — on failure, leave the board as-is rather than
    // throwing unhandled out of a SignalR event handler (matches fetchBoard's own
    // try/catch; a single-card patch isn't worth a user-facing toast).
    try {
      const { data } = await api.GET(ApiRoutes.Cards.detail(projectId, cardId))
      if (!data) return
      const card = data as CardResponse

      for (const [, cards] of cardsByColumn.value) {
        const idx = cards.findIndex((c: CardResponse) => c.id === card.id)
        if (idx !== -1) {
          cards.splice(idx, 1)
          break
        }
      }

      const targetCards = cardsByColumn.value.get(card.columnId) ?? []
      const insertAt = Math.min(Number(card.position), targetCards.length)
      targetCards.splice(insertAt, 0, card)
      cardsByColumn.value.set(card.columnId, targetCards)
    } catch {
      // stale board state until the next successful sync — no user-facing toast
    }
  }

  async function applyRealtimeColumnEvent(projectId: string, columnId: string, action: string) {
    if (action === 'Created' || action === 'Deleted') {
      await fetchBoard(projectId)
      return
    }

    try {
      if (action === 'Moved') {
        const { data } = await api.GET(ApiRoutes.Columns.list(projectId))
        if (data) columns.value = (data as ColumnResponse[]) ?? []
        return
      }

      const { data } = await api.GET(ApiRoutes.Columns.detail(projectId, columnId))
      if (!data) return
      updateColumnInStore(columnId, data as ColumnResponse)
    } catch {
      // stale board state until the next successful sync — no user-facing toast
    }
  }

  async function fetchMembers(projectId: string) {
    try {
      const { data } = await api.GET(ApiRoutes.Projects.members(projectId))
      if (data) members.value = (data as MemberResponse[]) ?? []
    } catch {
      // members list stays as-is; not critical enough for a user-facing toast
    }
  }

  return {
    project, columns, cardsByColumn, loading, error,
    fetchBoard, moveCard, rollbackMove, addCard, updateCard, removeCard, setColumnOrder,
    addColumn, updateColumnInStore, removeColumnFromStore,
    applyRealtimeCardEvent, applyRealtimeColumnEvent,
    cardContentEvent, signalCardContentEvent,
    boardFilters, visibleColumns,
    members, fetchMembers,
    selectedCardIds, selectedCount, toggleSelectCard, clearSelection
  }
})
