import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { ApiError } from '~/lib/api-error'

const mockPOST = vi.fn()
const mockPUT = vi.fn()
const mockDELETE = vi.fn()
const mockToastError = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: mockPUT,
  DELETE: mockDELETE
}))

mockNuxtImport('useAppToast', () => () => ({
  success: vi.fn(),
  error: mockToastError,
  remove: vi.fn(),
  clear: vi.fn()
}))

describe('useColumnManage', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    mockPOST.mockReset()
    mockPUT.mockReset()
    mockDELETE.mockReset()
    mockToastError.mockReset()
  })

  it('createColumn adds the returned column to the store on success', async () => {
    const { useColumnManage } = await import('~/composables/useColumnManage')
    const { useBoardStore } = await import('~/stores/board')
    const newColumn = { id: 'col2', name: 'Done', position: 1, wipLimit: null, color: null }
    mockPOST.mockResolvedValue({ data: newColumn, error: undefined })

    const board = useBoardStore()
    const { createColumn } = useColumnManage('p1')
    const ok = await createColumn('Done', null, null)

    expect(ok).toBe(true)
    expect(board.columns).toContainEqual(newColumn)
  })

  it('createColumn toasts and returns false on failure', async () => {
    const { useColumnManage } = await import('~/composables/useColumnManage')
    mockPOST.mockRejectedValue(new ApiError(400, 'VALIDATION_ERROR', 'Bad Request', 'Name required', 'about:blank', 'corr-1'))

    const { createColumn } = useColumnManage('p1')
    const ok = await createColumn('', null, null)

    expect(ok).toBe(false)
    expect(mockToastError).toHaveBeenCalledWith('Bad Request')
  })

  it('deleteColumn removes the column from the store on success', async () => {
    const { useColumnManage } = await import('~/composables/useColumnManage')
    const { useBoardStore } = await import('~/stores/board')
    mockDELETE.mockResolvedValue({ data: undefined, error: undefined })

    const board = useBoardStore()
    board.columns = [{ id: 'col1', name: 'Backlog', position: 0, wipLimit: null, color: null }]
    const { deleteColumn } = useColumnManage('p1')
    const ok = await deleteColumn('col1')

    expect(ok).toBe(true)
    expect(board.columns).toEqual([])
  })

  it('deleteColumn toasts the server message and returns false when the column has cards', async () => {
    const { useColumnManage } = await import('~/composables/useColumnManage')
    mockDELETE.mockRejectedValue(new ApiError(400, 'COLUMN_DELETE_NON_EMPTY', 'Cannot delete column with cards.', null, 'about:blank', 'corr-2'))

    const { deleteColumn } = useColumnManage('p1')
    const ok = await deleteColumn('col1')

    expect(ok).toBe(false)
    expect(mockToastError).toHaveBeenCalledWith('Cannot delete column with cards.')
  })
})