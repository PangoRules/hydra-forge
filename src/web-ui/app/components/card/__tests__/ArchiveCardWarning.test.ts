import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ArchiveCardWarning from '~/components/card/ArchiveCardWarning.vue'

// UModal teleports its content (to <body> in real usage); that target isn't
// present in this component-only test's happy-dom document, so real UModal
// would render nothing for wrapper.text()/findAll() to see. Stub it to a
// plain element that renders the named slots inline instead — same idea as
// the AppModal stub documented in CLAUDE.md for the same Teleport problem.
const stubs = {
  UModal: {
    template: '<div><slot name="header" /><slot name="body" /><slot name="footer" /></div>'
  }
}

describe('ArchiveCardWarning', () => {
  it('renders archive warning with dependents', async () => {
    const wrapper = await mountSuspended(ArchiveCardWarning, {
      props: {
        dependents: [
          { id: 'c1', title: 'Child Card', type: 'BlockedBy' },
          { id: 'c2', title: 'Related Card', type: 'Precedes' }
        ]
      },
      global: { stubs }
    })
    expect(wrapper.text()).toContain('Archive Card')
    expect(wrapper.text()).toContain('Child Card')
    expect(wrapper.text()).toContain('Related Card')
  })

  it('emits confirm on Archive click', async () => {
    const wrapper = await mountSuspended(ArchiveCardWarning, {
      props: { dependents: [{ id: 'c1', title: 'Child', type: 'BlockedBy' }] },
      global: { stubs }
    })
    const buttons = wrapper.findAll('button')
    const archiveButton = buttons.find(b => b.text().includes('Archive'))
    await archiveButton?.trigger('click')
    expect(wrapper.emitted('confirm')).toBeTruthy()
  })

  it('emits cancel on Cancel click', async () => {
    const wrapper = await mountSuspended(ArchiveCardWarning, {
      props: { dependents: [] },
      global: { stubs }
    })
    const buttons = wrapper.findAll('button')
    const cancelButton = buttons.find(b => b.text().includes('Cancel'))
    await cancelButton?.trigger('click')
    expect(wrapper.emitted('cancel')).toBeTruthy()
  })
})