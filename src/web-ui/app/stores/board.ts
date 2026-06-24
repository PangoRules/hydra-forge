import { defineStore } from 'pinia'

interface CardAssignee {
  userId: string
  username: string
}

interface CardResponse {
  id: string
  title: string
  description: string | null
  type: string
  position: number
  columnId: string
  projectId: string
  parentCardId: string | null
  isBlocked: boolean
  cardNumber: number
  version: number
  assignees: CardAssignee[]
}

interface ColumnResponse {
  id: string
  name: string
  color: string | null
  position: number
  projectId: string
  wipLimit: number | null
  cards: CardResponse[]
}

interface ProjectResponse {
  id: string
  name: string
  description: string | null
  isArchived: boolean
  cardNumberSeed: number
  createdAt: string
  updatedAt: string
}

interface ProjectSnapshotResponse {
  project: ProjectResponse
  columns: ColumnResponse[]
}

export const useBoardStore = defineStore('board', () => {
  const project = ref<ProjectResponse | null>(null)
  const columns = ref<ColumnResponse[]>([])
  const cardsByColumn = ref<Map<string, CardResponse[]>>(new Map())
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchBoard(projectId: string) {
    loading.value = true
    error.value = null
    try {
      const { data, error: apiError } = await api.GET('/api/projects/{projectId}/ProjectSnapshot', {
        params: { path: { projectId } }
      })
      if (apiError) throw apiError
      if (!data) throw new Error('No data returned')

      const snapshot = data as unknown as ProjectSnapshotResponse
      project.value = snapshot.project
      columns.value = snapshot.columns ?? []

      const map = new Map<string, CardResponse[]>()
      for (const col of columns.value) {
        map.set(col.id, col.cards ?? [])
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

  function rollbackMove(_cardId: string, _sourceColumnId: string, _sourcePosition: number) {
    if (project.value) {
      fetchBoard(project.value.id)
    }
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

  return {
    project, columns, cardsByColumn, loading, error,
    fetchBoard, moveCard, rollbackMove, addCard, updateCard, removeCard
  }
})
