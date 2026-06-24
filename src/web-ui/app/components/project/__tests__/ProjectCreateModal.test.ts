import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ProjectCreateModal from '~/components/project/ProjectCreateModal.vue'

describe('ProjectCreateModal', () => {
  it('renders form fields', async () => {
    const wrapper = await mountSuspended(ProjectCreateModal, {
      props: {}
    })
    expect(wrapper.find('input[type="text"]').exists()).toBe(true)
    expect(wrapper.find('textarea').exists()).toBe(true)
  })

  it('renders Create Project heading', async () => {
    const wrapper = await mountSuspended(ProjectCreateModal, {
      props: {}
    })
    expect(wrapper.text()).toContain('Create Project')
  })

  it('renders Cancel and Create buttons', async () => {
    const wrapper = await mountSuspended(ProjectCreateModal, {
      props: {}
    })
    expect(wrapper.text()).toContain('Cancel')
    expect(wrapper.text()).toContain('Create')
  })

  it('emits close on Cancel click', async () => {
    const wrapper = await mountSuspended(ProjectCreateModal, {
      props: {}
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('close')).toBeTruthy()
  })
})
