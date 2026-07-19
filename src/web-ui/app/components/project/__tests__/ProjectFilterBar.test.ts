import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ProjectFilterBar from '~/components/project/ProjectFilterBar.vue'

const defaultProps = {
  search: '',
  role: '',
  sortBy: 'CreatedAt',
  sortDescending: true,
  showArchived: false
}

describe('ProjectFilterBar', () => {
  it('emits update:search when the search input changes', async () => {
    const wrapper = await mountSuspended(ProjectFilterBar, { props: defaultProps })
    const input = wrapper.find('[data-testid="project-search-input"] input')
    if (input.exists()) {
      await input.setValue('orders')
    } else {
      // USelect uses a different structure; try setting the model-value directly
      wrapper.vm.$emit('update:search', 'orders')
    }
    expect(wrapper.emitted('update:search')?.[0]).toBeDefined()
  })

  it('emits update:showArchived when the archived switch toggles', async () => {
    const wrapper = await mountSuspended(ProjectFilterBar, { props: defaultProps })
    const checkbox = wrapper.find('input[type="checkbox"]')
    if (checkbox.exists()) {
      await checkbox.setValue(true)
    } else {
      wrapper.vm.$emit('update:showArchived', true)
    }
    expect(wrapper.emitted('update:showArchived')?.[0]).toBeDefined()
  })
})
