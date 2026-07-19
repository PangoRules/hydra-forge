import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import BoardView from '~/components/board/BoardView.vue'

const baseProps = {
  columns: [{ id: 'col-1', name: 'Backlog', position: 0, wipLimit: null, color: null }],
  cardsByColumn: new Map([['col-1', []]]),
  projectId: 'p1',
  includeArchived: false
}

describe('BoardView', () => {
  it('emits container-focus when the root element receives focus', async () => {
    const wrapper = await mountSuspended(BoardView, { props: baseProps })
    await wrapper.find('[tabindex="0"]').trigger('focus')
    expect(wrapper.emitted('container-focus')).toBeTruthy()
  })

  it('does not apply a focus ring class to the root element', async () => {
    const wrapper = await mountSuspended(BoardView, { props: baseProps })
    const root = wrapper.find('[tabindex="0"]')
    expect(root.classes().join(' ')).not.toContain('focus:ring')
  })

  it('labels the root element for screen readers', async () => {
    const wrapper = await mountSuspended(BoardView, { props: baseProps })
    const root = wrapper.find('[tabindex="0"]')
    expect(root.attributes('aria-label')).toBeTruthy()
  })
})
