/**
 * Shared composable for board filter state.
 * Reads/writes the Pinia board store's boardFilters directly — no duplicate state.
 * Used by both BoardFilterBar (desktop) and BoardMobileList (mobile).
 */
export function useBoardFilters() {
  const board = useBoardStore()

  const search = computed({
    get: () => board.boardFilters.search,
    set: (val: string) => { board.boardFilters.search = val }
  })

  const type = computed({
    get: () => board.boardFilters.type,
    set: (val: number | null) => { board.boardFilters.type = val }
  })

  const assigneeUserId = computed({
    get: () => board.boardFilters.assigneeUserId,
    set: (val: string | null) => { board.boardFilters.assigneeUserId = val }
  })

  const includeArchived = computed({
    get: () => board.boardFilters.includeArchived,
    set: (val: boolean) => { board.boardFilters.includeArchived = val }
  })

  const hideEmptyColumns = computed({
    get: () => board.boardFilters.hideEmptyColumns,
    set: (val: boolean) => { board.boardFilters.hideEmptyColumns = val }
  })

  return { search, type, assigneeUserId, includeArchived, hideEmptyColumns }
}
