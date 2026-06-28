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

  const visibleColumnIds = computed({
    get: () => board.boardFilters.visibleColumnIds,
    set: (val: string[]) => { board.boardFilters.visibleColumnIds = val }
  })

  const columnSelectionActive = computed(() => board.boardFilters.visibleColumnIds.length > 0)

  return { search, assigneeUserId, includeArchived, hideEmptyColumns, visibleColumnIds, columnSelectionActive }
}
