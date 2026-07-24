import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest'
import { ref, type Ref } from 'vue'
import { setActivePinia, createPinia } from 'pinia'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { useBoardKeyboardNav } from '~/composables/keyboard/useBoardKeyboardNav'
import { useKeyboard } from '~/composables/keyboard/useKeyboard'
import { useBoardStore } from '~/stores/board'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

// useBoardKeyboardNav explicitly imports useBoardStore and (via useCardMove) reads
// useApi/useAppToast through auto-import — mock only the auto-imported leaves and
// exercise the REAL Pinia store, matching the pattern in useColumnManage.test.ts.
const mockPOST = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn().mockResolvedValue({ data: undefined, error: undefined }),
  POST: mockPOST
}))

mockNuxtImport('useAppToast', () => () => ({
  success: vi.fn(),
  error: vi.fn(),
  remove: vi.fn(),
  clear: vi.fn()
}))

function makeCard(id: string, columnId: string): CardResponse {
  return {
    id,
    projectId: 'p1',
    columnId,
    cardNumber: 1,
    title: id,
    description: '',
    type: 0,
    position: 0,
    dueAt: null,
    version: 1,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    movedAt: new Date().toISOString(),
    archivedAt: null,
    parentCardId: null,
    assignees: [],
    watchers: []
  } as unknown as CardResponse
}

function press(key: string, opts: Partial<KeyboardEventInit> = {}) {
  window.dispatchEvent(new KeyboardEvent('keydown', { key, ...opts }))
}

describe('useBoardKeyboardNav', () => {
  let onOpenCard: Mock<(card: CardResponse) => void>
  let onCreateCard: Mock<(columnId?: string) => void>
  let onArchiveCard: Mock<(card: CardResponse) => void>
  let onShowShortcuts: Mock<() => void>
  let anyModalOpen: Ref<boolean>
  let projectArchived: Ref<boolean>
  let board: ReturnType<typeof useBoardStore>

  beforeEach(() => {
    setActivePinia(createPinia())
    useKeyboard().clear()
    mockPOST.mockReset().mockResolvedValue({ data: undefined, error: undefined })

    board = useBoardStore()
    board.columns = [
      { id: 'col-1', name: 'Backlog', position: 0, wipLimit: null, color: null },
      { id: 'col-2', name: 'In Progress', position: 1, wipLimit: null, color: null }
    ] as never
    board.cardsByColumn = new Map([
      ['col-1', [makeCard('card-1', 'col-1'), makeCard('card-2', 'col-1')]],
      ['col-2', [makeCard('card-3', 'col-2')]]
    ])

    onOpenCard = vi.fn<(card: CardResponse) => void>()
    onCreateCard = vi.fn<(columnId?: string) => void>()
    onArchiveCard = vi.fn<(card: CardResponse) => void>()
    onShowShortcuts = vi.fn<() => void>()
    anyModalOpen = ref(false)
    projectArchived = ref(false)
  })

  function setup() {
    const nav = useBoardKeyboardNav({
      projectId: 'p1',
      anyModalOpen,
      projectArchived,
      onOpenCard,
      onCreateCard,
      onArchiveCard,
      onShowShortcuts
    })
    nav.activate()
    return nav
  }

  it('starts selecting the first column and card', () => {
    const nav = setup()
    expect(nav.selectedColumnIndex.value).toBe(0)
    expect(nav.selectedCardIndex.value).toBe(0)
    expect(nav.selectedCardId.value).toBe('card-1')
  })

  it('j/k move the card selection within the current column', () => {
    const nav = setup()
    press('j')
    expect(nav.selectedCardIndex.value).toBe(1)
    press('k')
    expect(nav.selectedCardIndex.value).toBe(0)
  })

  it('l/h move the column selection and reset the card selection to 0', () => {
    const nav = setup()
    press('j')
    expect(nav.selectedCardIndex.value).toBe(1)
    press('l')
    expect(nav.selectedColumnIndex.value).toBe(1)
    expect(nav.selectedCardIndex.value).toBe(0)
    press('h')
    expect(nav.selectedColumnIndex.value).toBe(0)
    expect(nav.selectedCardIndex.value).toBe(0)
  })

  it('Enter opens the currently selected card', () => {
    const nav = setup()
    void nav
    press('l')
    press('Enter')
    expect(onOpenCard).toHaveBeenCalledWith(expect.objectContaining({ id: 'card-3' }))
  })

  it('n creates a card in the currently selected column, unless archived', () => {
    setup()
    press('n')
    expect(onCreateCard).toHaveBeenCalledWith('col-1')
    onCreateCard.mockClear()
    projectArchived.value = true
    press('n')
    expect(onCreateCard).not.toHaveBeenCalled()
  })

  it('a archives the currently selected card, unless already archived', () => {
    setup()
    press('a')
    expect(onArchiveCard).toHaveBeenCalledWith(expect.objectContaining({ id: 'card-1' }))
    onArchiveCard.mockClear()
    board.cardsByColumn.get('col-1')![0]!.archivedAt = '2026-01-01'
    press('a')
    expect(onArchiveCard).not.toHaveBeenCalled()
  })

  it('? shows the shortcut overlay', () => {
    setup()
    press('?')
    expect(onShowShortcuts).toHaveBeenCalledTimes(1)
  })

  it('Board shortcuts are disabled while a modal is open', () => {
    const nav = setup()
    anyModalOpen.value = true
    press('j')
    expect(nav.selectedCardIndex.value).toBe(0)
  })

  it('deactivate unregisters the Board shortcuts', () => {
    const nav = setup()
    nav.deactivate()
    press('j')
    expect(nav.selectedCardIndex.value).toBe(0)
  })

  it('Ctrl+Shift+L moves the selected card to the next column and follows it', () => {
    const nav = setup()
    press('L', { ctrlKey: true, shiftKey: true })
    expect(board.cardsByColumn.get('col-2')?.map(c => c.id)).toEqual(['card-3', 'card-1'])
    expect(board.cardsByColumn.get('col-1')?.map(c => c.id)).toEqual(['card-2'])
    expect(nav.selectedColumnIndex.value).toBe(1)
    expect(nav.selectedCardIndex.value).toBe(1)
  })

  it('Ctrl+Shift+J reorders the selected card down within its column', () => {
    const nav = setup()
    press('J', { ctrlKey: true, shiftKey: true })
    expect(board.cardsByColumn.get('col-1')?.map(c => c.id)).toEqual(['card-2', 'card-1'])
    expect(nav.selectedCardIndex.value).toBe(1)
  })

  it('plain L (no modifiers) does not move a card', () => {
    setup()
    press('L')
    expect(board.cardsByColumn.get('col-1')?.map(c => c.id)).toEqual(['card-1', 'card-2'])
  })

  it('syncToCard selects the column/card matching the given card', () => {
    const nav = setup()
    nav.syncToCard(makeCard('card-3', 'col-2'))
    expect(nav.selectedColumnIndex.value).toBe(1)
    expect(nav.selectedCardIndex.value).toBe(0)
  })

  it('syncToColumn selects the given column and resets the card index', () => {
    const nav = setup()
    press('j')
    nav.syncToColumn('col-2')
    expect(nav.selectedColumnIndex.value).toBe(1)
    expect(nav.selectedCardIndex.value).toBe(0)
  })

  it('clampSelection pulls an out-of-range card index back in bounds', () => {
    const nav = setup()
    press('l') // move to col-2, which has only 1 card
    board.cardsByColumn.set('col-2', [])
    nav.clampSelection()
    expect(nav.selectedCardIndex.value).toBe(0)
  })
})
