import { describe, it, expect, beforeEach } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import BoardFilterBar from '~/components/board/BoardFilterBar.vue'

describe('BoardFilterBar', () => {
  beforeEach(() => {
    const board = useBoardStore()
    board.boardFilters = { search: '', includeArchived: false, hideEmptyColumns: false, assigneeUserId: null, visibleColumnIds: [] }
  })

  it('renders search input', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { members: [], columns: [] }
    })
    expect(wrapper.find('input[placeholder*="Search"]').exists()).toBe(true)
  })

  it('renders column chips', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { members: [], columns: [
        { id: 'c1', name: 'Backlog', position: 0, wipLimit: null, color: null },
        { id: 'c2', name: 'In Dev', position: 1, wipLimit: null, color: null },
      ]}
    })
    const chips = wrapper.findAll('[data-testid="column-chip"]')
    expect(chips).toHaveLength(2)
    expect(chips.at(0)!.text()).toBe('Backlog')
  })

  it('toggles column selection on chip click', async () => {
    const board = useBoardStore()
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { members: [], columns: [
        { id: 'c1', name: 'Backlog', position: 0, wipLimit: null, color: null },
      ]}
    })
    const chip = wrapper.find('[data-testid="column-chip"]')
    await chip.trigger('click')
    expect(board.boardFilters.visibleColumnIds).toEqual(['c1'])
    await chip.trigger('click')
    expect(board.boardFilters.visibleColumnIds).toEqual([])
  })

  it('disables hideEmptyColumns when column selected', async () => {
    const board = useBoardStore()
    board.boardFilters.visibleColumnIds = ['c1']
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { members: [], columns: [
        { id: 'c1', name: 'Backlog', position: 0, wipLimit: null, color: null },
      ]}
    })
    const checkbox = wrapper.find('[data-testid="hide-empty-checkbox"]')
    expect(checkbox.attributes('disabled')).toBeDefined()
  })

  it('renders archived checkbox', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { members: [], columns: [] }
    })
    expect(wrapper.text()).toContain('Archived')
  })

  it('emits add-card on button click', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { members: [], columns: [] }
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('add-card')).toBeTruthy()
  })

  it('updates board store when search changes', async () => {
    const board = useBoardStore()
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { members: [], columns: [] }
    })
    const input = wrapper.find('input[placeholder*="Search"]')
    await input.setValue('test query')
    expect(board.boardFilters.search).toBe('test query')
  })

  it('updates board store when archived toggles', async () => {
    const board = useBoardStore()
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { members: [], columns: [] }
    })
    const checkbox = wrapper.find('input[type="checkbox"]')
    await checkbox.setValue(true)
    expect(board.boardFilters.includeArchived).toBe(true)
  })

  it('updates board store when hide empty toggles', async () => {
    const board = useBoardStore()
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { members: [], columns: [] }
    })
    const checkboxes = wrapper.findAll('input[type="checkbox"]')
    await checkboxes.at(1)!.setValue(true)
    expect(board.boardFilters.hideEmptyColumns).toBe(true)
  })
})