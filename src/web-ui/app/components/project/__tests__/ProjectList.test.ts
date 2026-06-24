import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ProjectList from '~/components/project/ProjectList.vue'

const makeProject = (id: string, name: string, description = 'A test project') => ({
  id,
  name,
  description,
  createdAt: new Date().toISOString(),
  archivedAt: null,
  memberCount: 1,
})

describe('ProjectList', () => {
  it('shows loading spinner when loading', async () => {
    const wrapper = await mountSuspended(ProjectList, {
      props: { projects: [], loading: true }
    })
    expect(wrapper.find('.animate-spin').exists()).toBe(true)
  })

  it('shows empty message when no projects', async () => {
    const wrapper = await mountSuspended(ProjectList, {
      props: { projects: [], loading: false }
    })
    expect(wrapper.text()).toContain('No projects yet')
  })

  it('renders project cards', async () => {
    const projects = [
      makeProject('p1', 'Project Alpha'),
      makeProject('p2', 'Project Beta'),
    ]
    const wrapper = await mountSuspended(ProjectList, {
      props: { projects, loading: false }
    })
    expect(wrapper.text()).toContain('Project Alpha')
    expect(wrapper.text()).toContain('Project Beta')
  })

  it('shows description or fallback text', async () => {
    const wrapper = await mountSuspended(ProjectList, {
      props: { projects: [makeProject('p1', 'Project A', null)], loading: false }
    })
    expect(wrapper.text()).toContain('Project A')
    expect(wrapper.text()).toContain('No description')
  })

  it('emits select with project id on card click', async () => {
    const projects = [makeProject('p1', 'Project Alpha')]
    const wrapper = await mountSuspended(ProjectList, {
      props: { projects, loading: false }
    })
    await wrapper.find('.cursor-pointer').trigger('click')
    expect(wrapper.emitted('select')).toBeTruthy()
    expect(wrapper.emitted('select')?.[0]).toEqual(['p1'])
  })
})
