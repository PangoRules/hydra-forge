import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useBoardStore } from '~/stores/board'

const makeColumn = (id: string, name: string, position = 0) => ({
  id,
  name,
  position,
  wipLimit: null,
  color: null,
})

const makeCard = (id: string, columnId: string, title: string, type = 0) => ({
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
})

describe('useBoardStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
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
})

describe('BoardStore filters', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('has default filter state', () => {
    const store = useBoardStore()
    expect(store.boardFilters.search).toBe('')
    expect(store.boardFilters.type).toBe(null)
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
      ['c1', [{ id: 'card1', columnId: 'c1', title: 'Task', type: 0, cardNumber: 1, position: 0, version: 1, dueAt: null, parentCardId: null, projectId: 'p1', archivedAt: null, assignees: [], watchers: [], createdAt: '', updatedAt: '', movedAt: '', description: '' }]],
      ['c2', []]
    ])
    expect(store.visibleColumns.length).toBe(2)
  })

  it('visibleColumns hides empty columns when set', () => {
    const store = useBoardStore()
    store.columns = [
      { id: 'c1', name: 'Todo', position: 0, wipLimit: null, color: null },
      { id: 'c2', name: 'Done', position: 1, wipLimit: null, color: null }
    ]
    store.cardsByColumn = new Map([
      ['c1', [{ id: 'card1', columnId: 'c1', title: 'Task', type: 0, cardNumber: 1, position: 0, version: 1, dueAt: null, parentCardId: null, projectId: 'p1', archivedAt: null, assignees: [], watchers: [], createdAt: '', updatedAt: '', movedAt: '', description: '' }]],
      ['c2', []]
    ])
    store.boardFilters.hideEmptyColumns = true
    expect(store.visibleColumns.length).toBe(1)
    expect(store.visibleColumns.at(0)?.id).toBe('c1')
  })
})
