import { describe, it, expect, beforeEach } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import BoardMobileList from '~/components/board/BoardMobileList.vue'
const makeColumn = (id: string, name: string, wipLimit: number | null = null) => ({
  id,
  name,
  position: 0,
  wipLimit,
  color: null,
})

type CardType = 'Task' | 'Issue' | 'Idea' | 'Goal'
const makeCard = (id: string, columnId: string, title: string, type: CardType = 'Task', assignees: never[] = []) => ({
  id,
  projectId: 'p1',
  columnId,
  cardNumber: 1,
  title,
  description: '',
  type,
  position: 0,
  dueAt: null,
  version: 1,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  movedAt: new Date().toISOString(),
  archivedAt: null,
  parentCardId: null,
  assignees,
  watchers: [],
  relationshipBadges: [],
  relationshipCount: 0,
  parentCard: null,
  childCount: 0,
})

describe('BoardMobileList', () => {
  beforeEach(() => {
    const board = useBoardStore()
    board.boardFilters = { search: '', includeArchived: false, hideEmptyColumns: false, assigneeUserId: null, visibleColumnIds: [] }
  })

  it('renders column headers', async () => {
    const columns = [makeColumn('col1', 'Backlog'), makeColumn('col2', 'Done')]
    const cardsByColumn = new Map([['col1', []], ['col2', []]])
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Backlog')
    expect(wrapper.text()).toContain('Done')
  })

  it('renders cards under correct column', async () => {
    const columns = [makeColumn('col1', 'Todo')]
    const cardsByColumn = new Map([['col1', [makeCard('c1', 'col1', 'My Task')]]])
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1' }
    })
    await wrapper.find('[data-testid="column-toggle"]').trigger('click')
    await wrapper.vm.$nextTick()
    expect(wrapper.text()).toContain('My Task')
  })

  it('renders cards from multiple columns', async () => {
    const columns = [makeColumn('col1', 'Todo'), makeColumn('col2', 'Done')]
    const cardsByColumn = new Map([
      ['col1', [makeCard('c1', 'col1', 'Task 1')]],
      ['col2', [makeCard('c2', 'col2', 'Task 2')]],
    ])
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1' }
    })
    const toggles = wrapper.findAll('[data-testid="column-toggle"]')
    await toggles.at(0)!.trigger('click')
    await wrapper.vm.$nextTick()
    await toggles.at(1)!.trigger('click')
    await wrapper.vm.$nextTick()
    expect(wrapper.text()).toContain('Task 1')
    expect(wrapper.text()).toContain('Task 2')
  })

  it('emits card-click on card tap', async () => {
    const columns = [makeColumn('col1', 'Todo')]
    const cardsByColumn = new Map([['col1', [makeCard('c1', 'col1', 'Tap Me')]]])
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1' }
    })
    await wrapper.find('[data-testid="column-toggle"]').trigger('click')
    await wrapper.vm.$nextTick()
    await wrapper.find('.cursor-pointer').trigger('click')
    expect(wrapper.emitted('card-click')).toBeTruthy()
    expect(wrapper.emitted('card-click')?.[0]).toBeDefined()
  })

  it('shows WIP limit when over the limit (shared ColumnHeader — matches desktop threshold)', async () => {
    const columns = [makeColumn('col1', 'In Progress', 3)]
    const cardsByColumn = new Map([
      ['col1', [
        makeCard('c1', 'col1', 'Task 1'),
        makeCard('c2', 'col1', 'Task 2'),
        makeCard('c3', 'col1', 'Task 3'),
        makeCard('c4', 'col1', 'Task 4'),
      ]]
    ])
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('WIP 3')
  })

  it('renders filter panel toggle', async () => {
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns: [], cardsByColumn: new Map(), projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Filter')
  })

  it('renders accordion column headers', async () => {
    const columns = [makeColumn('col1', 'Backlog')]
    const cardsByColumn = new Map([['col1', [makeCard('c1', 'col1', 'My Task')]]])
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('Backlog')
    expect(wrapper.text()).toContain('▶')
  })

  it('renders column visibility trigger in filter panel', async () => {
    const columns = [makeColumn('col1', 'Backlog'), makeColumn('col2', 'In Progress')]
    const cardsByColumn = new Map([['col1', []], ['col2', []]])
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1' }
    })
    await wrapper.find('[data-testid="mobile-filter-btn"]').trigger('click')
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-testid="column-visibility-trigger"]').exists()).toBe(true)
  })

  it('opens column dropdown with checkboxes on trigger click', async () => {
    const columns = [makeColumn('col1', 'Backlog')]
    const cardsByColumn = new Map([['col1', []]])
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1' }
    })
    await wrapper.find('[data-testid="mobile-filter-btn"]').trigger('click')
    await wrapper.vm.$nextTick()
    await wrapper.find('[data-testid="column-visibility-trigger"]').trigger('click')
    await wrapper.vm.$nextTick()
    const checkbox = wrapper.find('input[type="checkbox"]')
    expect(checkbox.exists()).toBe(true)
    expect(wrapper.text()).toContain('Backlog')
  })

  it('exposes column rename/delete via the shared ColumnHeader (not a mobile-only gap)', async () => {
    const columns = [makeColumn('col1', 'Backlog')]
    const cardsByColumn = new Map([['col1', []]])
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1' }
    })
    expect(wrapper.find('[data-testid="column-edit-trigger"]').exists()).toBe(true)
  })

  it('hides column rename/delete when readonly, same as desktop', async () => {
    const columns = [makeColumn('col1', 'Backlog')]
    const cardsByColumn = new Map([['col1', []]])
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1', readonly: true }
    })
    expect(wrapper.find('[data-testid="column-edit-trigger"]').exists()).toBe(false)
  })

  it('disables hideEmptyColumns when column is selected', async () => {
    const columns = [makeColumn('col1', 'Backlog')]
    const cardsByColumn = new Map([['col1', []]])
    const board = useBoardStore()
    board.boardFilters.visibleColumnIds = ['col1']
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1' }
    })
    await wrapper.find('[data-testid="mobile-filter-btn"]').trigger('click')
    await wrapper.vm.$nextTick()
    const checkbox = wrapper.find('[data-testid="hide-empty-checkbox"]')!
    expect((checkbox.element as HTMLInputElement).disabled).toBe(true)
  })
})
