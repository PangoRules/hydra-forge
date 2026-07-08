import { useBoardStore } from '~/stores/board'
import { ApiRoutes } from '~/lib/routes'
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']

export function useColumnManage(projectId: string) {
  const boardStore = useBoardStore()
  const api = useApi()
  const toast = useAppToast()
  const saving = ref(false)

  async function createColumn(name: string, color: string | null, wipLimit: number | null): Promise<boolean> {
    saving.value = true
    try {
      const { data } = await api.POST(ApiRoutes.Columns.create(projectId), {
        body: { name, color, wipLimit }
      })
      boardStore.addColumn(data as ColumnResponse)
      return true
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : 'Failed to create column'
      toast.error(message)
      return false
    } finally {
      saving.value = false
    }
  }

  async function updateColumn(columnId: string, name: string, color: string | null, wipLimit: number | null): Promise<boolean> {
    saving.value = true
    try {
      const { data } = await api.PUT(ApiRoutes.Columns.update(projectId, columnId), {
        body: { name, color, wipLimit }
      })
      boardStore.updateColumnInStore(columnId, data as ColumnResponse)
      return true
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : 'Failed to update column'
      toast.error(message)
      return false
    } finally {
      saving.value = false
    }
  }

  async function deleteColumn(columnId: string): Promise<boolean> {
    saving.value = true
    try {
      await api.DELETE(ApiRoutes.Columns.delete(projectId, columnId))
      boardStore.removeColumnFromStore(columnId)
      return true
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : 'Failed to delete column'
      toast.error(message)
      return false
    } finally {
      saving.value = false
    }
  }

  return { createColumn, updateColumn, deleteColumn, saving }
}