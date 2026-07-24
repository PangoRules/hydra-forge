import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ColumnHeader from '~/components/board/ColumnHeader.vue'

const makeColumn = (overrides = {}) => ({
  id: 'col1',
  name: 'Backlog',
  position: 0,
  wipLimit: null,
  color: null,
  ...overrides
})

describe('ColumnHeader edit/delete', () => {
  it('emits update-column with the edited name on save', async () => {
    const wrapper = await mountSuspended(ColumnHeader, {
      props: { column: makeColumn(), cardCount: 2, includeArchived: false },
    })

    await wrapper.find('[data-testid="column-edit-trigger"]').trigger('click')
    await wrapper.find('[data-testid="column-name-input"]').setValue('Renamed')
    await wrapper.find('[data-testid="column-save-trigger"]').trigger('click')

    expect(wrapper.emitted('update-column')?.[0]).toEqual(['Renamed', '#94a3b8', null])
  })

  it('emits delete-column after the delete confirm dialog is confirmed', async () => {
    const wrapper = await mountSuspended(ColumnHeader, {
      props: { column: makeColumn(), cardCount: 0, includeArchived: false },
    })

    await wrapper.find('[data-testid="column-edit-trigger"]').trigger('click')
    await wrapper.find('[data-testid="column-delete-trigger"]').trigger('click')
    await (wrapper.vm as any).confirmDelete()

    expect(wrapper.emitted('delete-column')).toBeTruthy()
  })

  it('hides the edit trigger when readonly', async () => {
    const wrapper = await mountSuspended(ColumnHeader, {
      props: { column: makeColumn(), cardCount: 0, includeArchived: false, readonly: true }
    })
    expect(wrapper.find('[data-testid="column-edit-trigger"]').exists()).toBe(false)
  })
})