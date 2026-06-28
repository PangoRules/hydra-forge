import { useBoardStore } from '~/stores/board'
import { ApiRoutes } from '~/lib/routes'

export function useColumnReorder(projectId: string) {
  const boardStore = useBoardStore()
  const api = useApi()
  const toast = useAppToast()
  const isReordering = ref(false)

  async function reorderColumns(draggedColumnId: string, targetColumnId: string) {
    const currentColumns = boardStore.visibleColumns
    const draggedIdx = currentColumns.findIndex(c => c.id === draggedColumnId)
    const targetIdx = currentColumns.findIndex(c => c.id === targetColumnId)
    if (draggedIdx === -1 || targetIdx === -1 || draggedIdx === targetIdx) return

    const newOrder = [...currentColumns]
    const dragged = newOrder[draggedIdx]
    const target = newOrder[targetIdx]
    if (!dragged || !target) return
    newOrder[draggedIdx] = target
    newOrder[targetIdx] = dragged

    isReordering.value = true
    try {
      await api.PUT(ApiRoutes.Columns.reorder(projectId), {
        body: { columnIds: newOrder.map(c => c.id) }
      })
      boardStore.setColumnOrder(newOrder)
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : 'Failed to reorder columns'
      toast.error(message)
    } finally {
      isReordering.value = false
    }
  }

  function moveColumnLeft(columnId: string) {
    const currentColumns = boardStore.visibleColumns
    const idx = currentColumns.findIndex(c => c.id === columnId)
    if (idx <= 0) return
    const newOrder = [...currentColumns]
    const left = newOrder[idx - 1]!
    const right = newOrder[idx]!
    newOrder[idx - 1] = right
    newOrder[idx] = left
    api.PUT(ApiRoutes.Columns.reorder(projectId), {
      body: { columnIds: newOrder.map(c => c.id) }
    }).then(() => {
      boardStore.setColumnOrder(newOrder)
    }).catch((e: unknown) => {
      const message = e instanceof Error ? e.message : 'Failed to reorder columns'
      const t = useAppToast()
      t.error(message)
    })
  }

  function moveColumnRight(columnId: string) {
    const currentColumns = boardStore.visibleColumns
    const idx = currentColumns.findIndex(c => c.id === columnId)
    if (idx === -1 || idx >= currentColumns.length - 1) return
    const newOrder = [...currentColumns]
    const left = newOrder[idx]!
    const right = newOrder[idx + 1]!
    newOrder[idx] = right
    newOrder[idx + 1] = left
    api.PUT(ApiRoutes.Columns.reorder(projectId), {
      body: { columnIds: newOrder.map(c => c.id) }
    }).then(() => {
      boardStore.setColumnOrder(newOrder)
    }).catch((e: unknown) => {
      const message = e instanceof Error ? e.message : 'Failed to reorder columns'
      const t = useAppToast()
      t.error(message)
    })
  }

  return { reorderColumns, moveColumnLeft, moveColumnRight, isReordering }
}
