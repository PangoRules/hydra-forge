import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ProjectListTable from '~/components/project/ProjectListTable.vue'

const makeProject = (overrides = {}) => ({
  id: 'p1',
  name: 'Orders API',
  description: 'desc',
  createdAt: new Date().toISOString(),
  archivedAt: null,
  memberCount: 3,
  myRole: 'Owner' as any,
  ...overrides
})

describe('ProjectListTable', () => {
  it('renders project rows', async () => {
    const wrapper = await mountSuspended(ProjectListTable, {
      props: { projects: [makeProject()], loading: false }
    })
    expect(wrapper.text()).toContain('Orders API')
    expect(wrapper.text()).toContain('Owner')
  })

  it('shows an Archived badge for archived projects', async () => {
    const wrapper = await mountSuspended(ProjectListTable, {
      props: { projects: [makeProject({ archivedAt: new Date().toISOString() })], loading: false }
    })
    expect(wrapper.text()).toContain('Archived')
  })

  it('emits edit when the edit button is clicked', async () => {
    const wrapper = await mountSuspended(ProjectListTable, {
      props: { projects: [makeProject()], loading: false }
    })
    await wrapper.find('[data-testid="edit-p1"]').trigger('click')
    expect(wrapper.emitted('edit')?.[0]).toEqual(['p1'])
  })
})