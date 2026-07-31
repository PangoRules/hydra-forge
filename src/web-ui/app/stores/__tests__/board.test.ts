import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { useBoardStore } from '~/stores/board'

const mockGET = vi.fn()
mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn(),
  PATCH: vi.fn()
}))

const makeColumn = (id: string, name: string, position = 0) => ({
  id,
  name,
  position,
  wipLimit: null,
  color: null,
})

type CardType = 'Task' | 'Issue' | 'Idea' | 'Goal'
const makeCard = (id: string, columnId: string, title: string, type: CardType = 'Task') => ({
  id,
  projectId: 'p1',
  columnId,
  cardNumber: 1,
  title,
  description: '',
  type,
  position: 0,
  dueAt: null,
  version: 1,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  movedAt: new Date().toISOString(),
  archivedAt: null,
  parentCardId: null,
  assignees: [],
  watchers: [],
  relationshipBadges: [],
  relationshipCount: 0,
  parentCard: null,
  childCount: 0,
})

describe('useBoardStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    mockGET.mockReset()
  })

  it('starts with empty columns and empty cards', () => {
    const board = useBoardStore()
    expect(board.columns).toEqual([])
    expect(board.cardsByColumn.size).toBe(0)
    expect(board.loading).toBe(false)
    expect(board.error).toBeNull()
  })

  it('moveCard moves card from source to target column', () => {
    const board = useBoardStore()
    const col1 = makeColumn('col1', 'Todo')
    const col2 = makeColumn('col2', 'Done')
    board.columns = [col1, col2]
    board.cardsByColumn = new Map([
      ['col1', [makeCard('c1', 'col1', 'Task 1')]],
      ['col2', []],
    ])

    board.moveCard('c1', 'col2', 0)

    expect(board.cardsByColumn.get('col1')?.length).toBe(0)
    expect(board.cardsByColumn.get('col2')?.length).toBe(1)
    expect(board.cardsByColumn.get('col2')?.[0]?.id).toBe('c1')
  })

  it('moveCard is no-op for unknown card', () => {
    const board = useBoardStore()
    board.columns = [makeColumn('col1', 'Todo')]
    board.cardsByColumn = new Map([['col1', [makeCard('c1', 'col1', 'Task 1')]]])

    board.moveCard('nonexistent', 'col1', 0)

    expect(board.cardsByColumn.get('col1')!.length).toBe(1)
  })

  it('addCard pushes card to correct column', () => {
    const board = useBoardStore()
    board.columns = [makeColumn('col1', 'Todo')]
    board.cardsByColumn = new Map([['col1', []]])

    board.addCard('col1', makeCard('c1', 'col1', 'New Card'))

    expect(board.cardsByColumn.get('col1')?.length).toBe(1)
    expect(board.cardsByColumn.get('col1')?.[0]?.id).toBe('c1')
  })

  it('updateCard modifies card fields in place', () => {
    const board = useBoardStore()
    board.columns = [makeColumn('col1', 'Todo')]
    board.cardsByColumn = new Map([['col1', [makeCard('c1', 'col1', 'Old Title')]]])

    board.updateCard('c1', { title: 'New Title' })

    expect(board.cardsByColumn.get('col1')?.[0]?.title).toBe('New Title')
  })

  it('removeCard deletes card from column', () => {
    const board = useBoardStore()
    board.columns = [makeColumn('col1', 'Todo')]
    board.cardsByColumn = new Map([['col1', [makeCard('c1', 'col1', 'Task 1')]]])

    board.removeCard('c1')

    expect(board.cardsByColumn.get('col1')!.length).toBe(0)
  })

  it('addColumn appends a column to the end', () => {
    const board = useBoardStore()
    board.columns = [{ id: 'col1', name: 'Backlog', position: 0, wipLimit: null, color: null }]
    board.addColumn({ id: 'col2', name: 'Done', position: 1, wipLimit: null, color: null })
    expect(board.columns.map(c => c.id)).toEqual(['col1', 'col2'])
  })

  it('updateColumnInStore merges updates into the matching column', () => {
    const board = useBoardStore()
    board.columns = [{ id: 'col1', name: 'Backlog', position: 0, wipLimit: null, color: null }]
    board.updateColumnInStore('col1', { name: 'Renamed', color: '#ff0000' })
    expect(board.columns[0]).toMatchObject({ id: 'col1', name: 'Renamed', color: '#ff0000' })
  })

  it('removeColumnFromStore removes the column and its cards map entry', () => {
    const board = useBoardStore()
    board.columns = [
      { id: 'col1', name: 'Backlog', position: 0, wipLimit: null, color: null },
      { id: 'col2', name: 'Done', position: 1, wipLimit: null, color: null }
    ]
    board.cardsByColumn = new Map([['col1', []], ['col2', []]])
    board.removeColumnFromStore('col1')
    expect(board.columns.map(c => c.id)).toEqual(['col2'])
    expect(board.cardsByColumn.has('col1')).toBe(false)
  })

  it('applyRealtimeCardEvent Updated re-fetches the single card and patches it in place', async () => {
    const board = useBoardStore()
    board.columns = [makeColumn('col1', 'Todo')]
    board.cardsByColumn = new Map([['col1', [makeCard('c1', 'col1', 'Old Title')]]])
    mockGET.mockResolvedValueOnce({ data: { ...makeCard('c1', 'col1', 'New Title'), position: 0 }, error: null })

    await board.applyRealtimeCardEvent('p1', 'c1', 'Updated')

    expect(board.cardsByColumn.get('col1')?.[0]?.title).toBe('New Title')
    expect(mockGET).toHaveBeenCalledTimes(1)
  })

  it('applyRealtimeCardEvent Moved relocates the card into its new column at the fetched position', async () => {
    const board = useBoardStore()
    board.columns = [makeColumn('col1', 'Todo'), makeColumn('col2', 'Done')]
    board.cardsByColumn = new Map([
      ['col1', [makeCard('c1', 'col1', 'Task 1')]],
      ['col2', []],
    ])
    mockGET.mockResolvedValueOnce({ data: { ...makeCard('c1', 'col2', 'Task 1'), position: 0 }, error: null })

    await board.applyRealtimeCardEvent('p1', 'c1', 'Moved')

    expect(board.cardsByColumn.get('col1')?.length).toBe(0)
    expect(board.cardsByColumn.get('col2')?.map(c => c.id)).toEqual(['c1'])
  })

  it('applyRealtimeCardEvent Archived falls back to a full board refresh', async () => {
    const board = useBoardStore()
    board.columns = [makeColumn('col1', 'Todo')]
    board.cardsByColumn = new Map([['col1', [makeCard('c1', 'col1', 'Task 1')]]])
    mockGET.mockResolvedValueOnce({ data: [], error: null })
    mockGET.mockResolvedValueOnce({ data: { cards: [] }, error: null })

    await board.applyRealtimeCardEvent('p1', 'c1', 'Archived')

    // fetchBoard fires two GETs (columns + cards) — Archived never hits the single-card GET at all
    expect(mockGET).toHaveBeenCalledTimes(2)
  })

  it('applyRealtimeColumnEvent Updated re-fetches the single column and merges it', async () => {
    const board = useBoardStore()
    board.columns = [makeColumn('col1', 'Backlog')]
    mockGET.mockResolvedValueOnce({ data: makeColumn('col1', 'Renamed'), error: null })

    await board.applyRealtimeColumnEvent('p1', 'col1', 'Updated')

    expect(board.columns[0]?.name).toBe('Renamed')
  })

  it('applyRealtimeColumnEvent Moved re-fetches the full column order only (no card fetch)', async () => {
    const board = useBoardStore()
    board.columns = [makeColumn('col1', 'A', 0), makeColumn('col2', 'B', 1)]
    mockGET.mockResolvedValueOnce({ data: [makeColumn('col2', 'B', 0), makeColumn('col1', 'A', 1)], error: null })

    await board.applyRealtimeColumnEvent('p1', 'col2', 'Moved')

    expect(board.columns.map(c => c.id)).toEqual(['col2', 'col1'])
    expect(mockGET).toHaveBeenCalledTimes(1)
  })
})

describe('BoardStore filters', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('has default filter state', () => {
    const store = useBoardStore()
    expect(store.boardFilters.search).toBe('')
    expect(store.boardFilters.visibleColumnIds).toEqual([])
    expect(store.boardFilters.includeArchived).toBe(false)
    expect(store.boardFilters.hideEmptyColumns).toBe(false)
  })

  it('visibleColumns returns all columns by default', () => {
    const store = useBoardStore()
    store.columns = [
      { id: 'c1', name: 'Todo', position: 0, wipLimit: null, color: null },
      { id: 'c2', name: 'Done', position: 1, wipLimit: null, color: null }
    ]
    store.cardsByColumn = new Map([
      ['c1', [{ id: 'card1', columnId: 'c1', title: 'Task', type: 'Task', cardNumber: 1, position: 0, version: 1, dueAt: null, parentCardId: null, projectId: 'p1', archivedAt: null, assignees: [], watchers: [], createdAt: '', updatedAt: '', movedAt: '', description: '', relationshipBadges: [], relationshipCount: 0, parentCard: null, childCount: 0 }]],
      ['c2', []]
    ])
    expect(store.visibleColumns.length).toBe(2)
  })

  it('visibleColumns hides empty columns when set and no active selection', () => {
    const store = useBoardStore()
    store.columns = [
      { id: 'c1', name: 'Todo', position: 0, wipLimit: null, color: null },
      { id: 'c2', name: 'Done', position: 1, wipLimit: null, color: null }
    ]
    store.cardsByColumn = new Map([
      ['c1', [{ id: 'card1', columnId: 'c1', title: 'Task', type: 'Task', cardNumber: 1, position: 0, version: 1, dueAt: null, parentCardId: null, projectId: 'p1', archivedAt: null, assignees: [], watchers: [], createdAt: '', updatedAt: '', movedAt: '', description: '', relationshipBadges: [], relationshipCount: 0, parentCard: null, childCount: 0 }]],
      ['c2', []]
    ])
    store.boardFilters.hideEmptyColumns = true
    store.boardFilters.visibleColumnIds = []
    expect(store.visibleColumns.length).toBe(1)
    expect(store.visibleColumns.at(0)?.id).toBe('c1')
  })

  it('returns only selected columns when visibleColumnIds set (ignores hideEmptyColumns)', () => {
    const store = useBoardStore()
    store.columns = [
      { id: 'c1', name: 'Todo', position: 0, wipLimit: null, color: null },
      { id: 'c2', name: 'Done', position: 1, wipLimit: null, color: null }
    ]
    store.cardsByColumn = new Map([
      ['c1', [{ id: 'card1', columnId: 'c1', title: 'Task', type: 'Task', cardNumber: 1, position: 0, version: 1, dueAt: null, parentCardId: null, projectId: 'p1', archivedAt: null, assignees: [], watchers: [], createdAt: '', updatedAt: '', movedAt: '', description: '', relationshipBadges: [], relationshipCount: 0, parentCard: null, childCount: 0 }]],
      ['c2', []]
    ])
    store.boardFilters.visibleColumnIds = ['c2']
    store.boardFilters.hideEmptyColumns = true
    // hideEmptyColumns ignored when selection active
    expect(store.visibleColumns.map(c => c.id)).toEqual(['c2'])
  })
})
