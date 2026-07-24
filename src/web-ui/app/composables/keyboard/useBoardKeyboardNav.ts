import type { Ref, ComputedRef } from 'vue'
import type { components } from '~/types/api'
import { useBoardStore } from '~/stores/board'
import { useCardMove } from '~/composables/useCardMove'
import { useKeyboard } from './useKeyboard'
import { useRovingFocus } from './useRovingFocus'

type CardResponse = components['schemas']['CardResponse']

export function useBoardKeyboardNav(options: {
  projectId: string
  anyModalOpen: Ref<boolean> | ComputedRef<boolean>
  projectArchived: Ref<boolean>
  onOpenCard: (card: CardResponse) => void
  onCreateCard: (columnId?: string) => void
  onArchiveCard: (card: CardResponse) => void
  onShowShortcuts: () => void
}) {
  const board = useBoardStore()
  const keyboard = useKeyboard()
  const { moveCardToColumn } = useCardMove(options.projectId)

  const columnAxis = useRovingFocus(() => board.visibleColumns.map(c => c.id))
  const cardAxis = useRovingFocus(() => {
    const col = board.visibleColumns[columnAxis.selectedIndex.value]
    if (!col) return []
    return (board.cardsByColumn.get(col.id) ?? []).map(c => c.id)
  })

  function currentColumn() {
    return board.visibleColumns[columnAxis.selectedIndex.value]
  }

  function currentCards(): CardResponse[] {
    const col = currentColumn()
    if (!col) return []
    return board.cardsByColumn.get(col.id) ?? []
  }

  function currentCard(): CardResponse | undefined {
    return currentCards()[cardAxis.selectedIndex.value]
  }

  function syncToCard(card: CardResponse) {
    for (const [colIdx, col] of board.visibleColumns.entries()) {
      const cards = board.cardsByColumn.get(col.id) ?? []
      const cardIdx = cards.findIndex(c => c.id === card.id)
      if (cardIdx !== -1) {
        columnAxis.select(colIdx)
        cardAxis.select(cardIdx)
        break
      }
    }
  }

  function syncToColumn(columnId: string) {
    const idx = board.visibleColumns.findIndex(c => c.id === columnId)
    if (idx !== -1) {
      columnAxis.select(idx)
      cardAxis.select(0)
    }
  }

  function clampSelection() {
    const columns = board.visibleColumns
    if (columns.length === 0) return
    if (columnAxis.selectedIndex.value >= columns.length) {
      columnAxis.select(columns.length - 1)
    }
    const cards = currentCards()
    if (cards.length > 0 && cardAxis.selectedIndex.value >= cards.length) {
      cardAxis.select(cards.length - 1)
    }
  }

  function moveSelectedCard(direction: -1 | 1) {
    if (options.anyModalOpen.value || options.projectArchived.value) return
    const columns = board.visibleColumns
    if (columns.length < 2) return
    const card = currentCard()
    if (!card) return
    const targetIdx = columnAxis.selectedIndex.value + direction
    if (targetIdx < 0 || targetIdx >= columns.length) return
    const toCol = columns[targetIdx]
    if (!toCol) return
    const targetCards = board.cardsByColumn.get(toCol.id) ?? []
    const newPos = targetCards.length
    moveCardToColumn(card.id, toCol.id, newPos)
    columnAxis.select(targetIdx)
    cardAxis.select(newPos)
  }

  function reorderSelectedCard(direction: -1 | 1) {
    if (options.anyModalOpen.value || options.projectArchived.value) return
    const cards = currentCards()
    if (cards.length < 2) return
    const card = currentCard()
    if (!card) return
    const newPos = cardAxis.selectedIndex.value + direction
    if (newPos < 0 || newPos >= cards.length) return
    const col = currentColumn()
    if (!col) return
    moveCardToColumn(card.id, col.id, newPos)
    cardAxis.select(newPos)
  }

  const boardEnabled = () => !options.anyModalOpen.value

  function activate() {
    keyboard.register('Board', 'j', (e) => {
      e.preventDefault()
      if (currentCards().length === 0) return
      cardAxis.next()
    }, 'Next card', false, boardEnabled)

    keyboard.register('Board', 'k', (e) => {
      e.preventDefault()
      if (currentCards().length === 0) return
      cardAxis.prev()
    }, 'Previous card', false, boardEnabled)

    keyboard.register('Board', 'l', (e) => {
      e.preventDefault()
      if (board.visibleColumns.length === 0) return
      columnAxis.next()
      cardAxis.select(0)
    }, 'Next column', false, boardEnabled)

    keyboard.register('Board', 'h', (e) => {
      e.preventDefault()
      if (board.visibleColumns.length === 0) return
      columnAxis.prev()
      cardAxis.select(0)
    }, 'Previous column', false, boardEnabled)

    keyboard.register('Board', '?', (e) => {
      e.preventDefault()
      options.onShowShortcuts()
    }, 'Show keyboard shortcuts', false, boardEnabled)

    keyboard.register('Board', 'n', (e) => {
      e.preventDefault()
      if (options.projectArchived.value) return
      const col = currentColumn()
      if (col) options.onCreateCard(col.id)
    }, 'Create new card', false, boardEnabled)

    keyboard.register('Board', 'Enter', (e) => {
      e.preventDefault()
      const card = currentCard()
      if (card) options.onOpenCard(card)
    }, 'Open card', false, boardEnabled)

    keyboard.register('Board', 'a', (e) => {
      e.preventDefault()
      if (options.projectArchived.value) return
      const card = currentCard()
      if (card && !card.archivedAt) options.onArchiveCard(card)
    }, 'Archive card', false, boardEnabled)

    // hjkl chords for move/reorder, standardized alongside the plain hjkl
    // nav shortcuts above (l/h = column right/left, j/k = card down/up).
    // Registered by their Shift-produced uppercase letter (event.key is
    // 'H'/'J'/'K'/'L' when Shift is held) so they don't collide with the
    // plain lowercase nav registrations in useKeyboard's key-only match —
    // see useKeyboard.ts. Known edge case: with Caps Lock on, a plain
    // h/j/k/l press also reports an uppercase key, so it matches these
    // handlers instead of nav and (lacking ctrlKey) silently no-ops rather
    // than falling back to navigation. Not worth a bigger keybinding
    // rework for that edge case today.
    keyboard.register('Board', 'L', (e) => {
      if (!e.ctrlKey || !e.shiftKey) return
      e.preventDefault()
      moveSelectedCard(1)
    }, 'Move card right', false, boardEnabled)

    keyboard.register('Board', 'H', (e) => {
      if (!e.ctrlKey || !e.shiftKey) return
      e.preventDefault()
      moveSelectedCard(-1)
    }, 'Move card left', false, boardEnabled)

    keyboard.register('Board', 'K', (e) => {
      if (!e.ctrlKey || !e.shiftKey) return
      e.preventDefault()
      reorderSelectedCard(-1)
    }, 'Move card up', false, boardEnabled)

    keyboard.register('Board', 'J', (e) => {
      if (!e.ctrlKey || !e.shiftKey) return
      e.preventDefault()
      reorderSelectedCard(1)
    }, 'Move card down', false, boardEnabled)
  }

  function deactivate() {
    keyboard.unregister('Board')
  }

  return {
    selectedColumnIndex: columnAxis.selectedIndex,
    selectedCardIndex: cardAxis.selectedIndex,
    selectedCardId: cardAxis.selectedId,
    syncToCard,
    syncToColumn,
    clampSelection,
    activate,
    deactivate
  }
}
