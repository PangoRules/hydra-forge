import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ArchiveCardWarning from '~/components/card/ArchiveCardWarning.vue'

describe('ArchiveCardWarning', () => {
  it('renders archive warning with dependents', async () => {
    const wrapper = await mountSuspended(ArchiveCardWarning, {
      props: {
        dependents: [
          { id: 'c1', title: 'Child Card', type: 'BlockedBy' },
          { id: 'c2', title: 'Related Card', type: 'Precedes' }
        ]
      }
    })
    expect(wrapper.text()).toContain('Archive Card')
    expect(wrapper.text()).toContain('Child Card')
    expect(wrapper.text()).toContain('Related Card')
  })

  it('emits confirm on Archive click', async () => {
    const wrapper = await mountSuspended(ArchiveCardWarning, {
      props: { dependents: [{ id: 'c1', title: 'Child', type: 'BlockedBy' }] }
    })
    const buttons = wrapper.findAll('button')
    const archiveButton = buttons.find(b => b.text().includes('Archive'))
    await archiveButton?.trigger('click')
    expect(wrapper.emitted('confirm')).toBeTruthy()
  })

  it('emits cancel on Cancel click', async () => {
    const wrapper = await mountSuspended(ArchiveCardWarning, {
      props: { dependents: [] }
    })
    const buttons = wrapper.findAll('button')
    const cancelButton = buttons.find(b => b.text().includes('Cancel'))
    await cancelButton?.trigger('click')
    expect(wrapper.emitted('cancel')).toBeTruthy()
  })
})