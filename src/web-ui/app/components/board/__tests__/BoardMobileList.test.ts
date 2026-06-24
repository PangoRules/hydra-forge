import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import BoardMobileList from '~/components/board/BoardMobileList.vue'

const makeColumn = (id: string, name: string, wipLimit: number | null = null) => ({
  id,
  name,
  position: 0,
  wipLimit,
  color: null,
})

const makeCard = (id: string, columnId: string, title: string, type = 0, assignees = []) => ({
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
})

describe('BoardMobileList', () => {
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
    expect(wrapper.text()).toContain('Task 1')
    expect(wrapper.text()).toContain('Task 2')
  })

  it('emits card-click on card tap', async () => {
    const columns = [makeColumn('col1', 'Todo')]
    const cardsByColumn = new Map([['col1', [makeCard('c1', 'col1', 'Tap Me')]]])
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1' }
    })
    await wrapper.find('.cursor-pointer').trigger('click')
    expect(wrapper.emitted('card-click')).toBeTruthy()
    expect(wrapper.emitted('card-click')?.[0]).toBeDefined()
  })

  it('shows WIP limit when set and at limit', async () => {
    const columns = [makeColumn('col1', 'In Progress', 3)]
    const cardsByColumn = new Map([
      ['col1', [
        makeCard('c1', 'col1', 'Task 1'),
        makeCard('c2', 'col1', 'Task 2'),
        makeCard('c3', 'col1', 'Task 3'),
      ]]
    ])
    const wrapper = await mountSuspended(BoardMobileList, {
      props: { columns, cardsByColumn, projectId: 'p1' }
    })
    expect(wrapper.text()).toContain('WIP 3')
  })
})
