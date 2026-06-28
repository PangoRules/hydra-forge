import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CardMetadata from '~/components/card/CardMetadata.vue'
import { ApiRoutes } from '~/lib/routes'
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

const mockPUT = vi.fn()
const mockPOST = vi.fn()
const mockDELETE = vi.fn()
const mockBoardMembers = vi.fn()
const mockBoardColumns = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: vi.fn(),
  POST: mockPOST,
  PUT: mockPUT,
  DELETE: mockDELETE
}))

mockNuxtImport('useBoardStore', () => () => ({
  columns: mockBoardColumns(),
  members: mockBoardMembers(),
  cardsByColumn: new Map([['col1', []]]),
  fetchMembers: vi.fn(),
  fetchBoard: vi.fn()
}))

function makeCard(overrides: Partial<CardResponse> = {}): CardResponse {
  return {
    id: 'c1', projectId: 'p1', columnId: 'col1', cardNumber: 1,
    title: 'Test', description: '', type: 0, position: 0,
    dueAt: null, version: 1,
    createdAt: '2024-01-01T00:00:00Z', updatedAt: '2024-01-01T00:00:00Z',
    movedAt: '2024-01-01T00:00:00Z', archivedAt: null, parentCardId: null,
    assignees: [], watchers: [],
    ...overrides
  }
}

describe('CardMetadata', () => {
  beforeEach(() => {
    mockBoardColumns.mockReturnValue([{ id: 'col1', name: 'Backlog', position: 0, wipLimit: null, color: null }])
    mockBoardMembers.mockReturnValue([{ id: 'm1', userId: 'u1', username: 'Alice', role: 0, joinedAt: '2024-01-01T00:00:00Z' }])
    mockPUT.mockReset()
    mockPOST.mockReset()
    mockDELETE.mockReset()
  })

  it('renders the type badge label for an archived card', async () => {
    const wrapper = await mountSuspended(CardMetadata, {
      props: { card: makeCard({ archivedAt: '2024-02-01T00:00:00Z' }), projectId: 'p1', isArchived: true }
    })
    expect(wrapper.text()).toContain('Task')
  })

  it('shows None for missing due date', async () => {
    const wrapper = await mountSuspended(CardMetadata, { props: { card: makeCard(), projectId: 'p1' } })
    expect(wrapper.text()).toContain('None')
  })

  it('sends the full update body including the current version on a type change', async () => {
    mockPUT.mockResolvedValue({ data: makeCard({ version: 2 }), error: undefined })
    const wrapper = await mountSuspended(CardMetadata, {
      props: { card: makeCard({ version: 5 }), projectId: 'p1' }
    })

    await wrapper.findComponent({ name: 'USelect' }).vm.$emit('update:model-value', 'Issue')
    await flushPromises()

    expect(mockPUT).toHaveBeenCalledWith(
      ApiRoutes.Cards.update('p1', 'c1'),
      expect.objectContaining({ body: expect.objectContaining({ title: 'Test', version: 5, type: 'Issue' }) })
    )
    expect(wrapper.emitted('update:card')![0]).toEqual([makeCard({ version: 2 })])
  })

  it('assigns a member and emits the updated card', async () => {
    mockPOST.mockResolvedValue({
      data: makeCard({ assignees: [{ id: 'a1', userId: 'u1', username: 'Alice', assignedAt: '2024-01-01T00:00:00Z' }] }),
      error: undefined
    })
    const wrapper = await mountSuspended(CardMetadata, { props: { card: makeCard(), projectId: 'p1' } })

    await (wrapper.findAll('select')[1]!).setValue('u1')
    await flushPromises()

    expect(mockPOST).toHaveBeenCalledWith(ApiRoutes.Cards.assignees('p1', 'c1'), { body: { assigneeUserId: 'u1' } })
    expect(wrapper.emitted('update:card')).toBeTruthy()
  })
})