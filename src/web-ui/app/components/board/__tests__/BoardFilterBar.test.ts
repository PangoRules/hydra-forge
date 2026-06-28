import { describe, it, expect, beforeEach } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import BoardFilterBar from '~/components/board/BoardFilterBar.vue'

describe('BoardFilterBar', () => {
  beforeEach(() => {
    const board = useBoardStore()
    board.boardFilters = { search: '', type: null, includeArchived: false, hideEmptyColumns: false, assigneeUserId: null }
  })

  it('renders search input', async () => {
    const wrapper = await mountSuspended(BoardFilterBar)
    expect(wrapper.find('input[placeholder*="Search"]').exists()).toBe(true)
  })

  it('renders type dropdown with All option', async () => {
    const wrapper = await mountSuspended(BoardFilterBar)
    const select = wrapper.find('select')
    expect(select.exists()).toBe(true)
    expect(select.text()).toContain('All')
  })

  it('renders archived checkbox', async () => {
    const wrapper = await mountSuspended(BoardFilterBar)
    expect(wrapper.text()).toContain('Archived')
  })

  it('emits add-card on button click', async () => {
    const wrapper = await mountSuspended(BoardFilterBar)
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('add-card')).toBeTruthy()
  })

  it('updates board store when search changes', async () => {
    const board = useBoardStore()
    const wrapper = await mountSuspended(BoardFilterBar)
    const input = wrapper.find('input[placeholder*="Search"]')
    await input.setValue('test query')
    expect(board.boardFilters.search).toBe('test query')
  })

  it('updates board store when type changes', async () => {
    const board = useBoardStore()
    const wrapper = await mountSuspended(BoardFilterBar)
    const select = wrapper.find('select')
    await select.setValue('Bug')
    expect(board.boardFilters.type).toBe('Bug')
  })

  it('updates board store when archived toggles', async () => {
    const board = useBoardStore()
    const wrapper = await mountSuspended(BoardFilterBar)
    const checkbox = wrapper.find('input[type="checkbox"]')
    await checkbox.setValue(true)
    expect(board.boardFilters.includeArchived).toBe(true)
  })

  it('updates board store when hide empty toggles', async () => {
    const board = useBoardStore()
    const wrapper = await mountSuspended(BoardFilterBar)
    const checkboxes = wrapper.findAll('input[type="checkbox"]')
    await checkboxes.at(1)!.setValue(true)
    expect(board.boardFilters.hideEmptyColumns).toBe(true)
  })
})