import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import BoardFilterBar from '~/components/board/BoardFilterBar.vue'
import type { BoardFilters } from '~/stores/board'

const defaultFilters: BoardFilters = {
  search: '',
  type: null,
  includeArchived: false,
  hideEmptyColumns: false
}

describe('BoardFilterBar', () => {
  it('renders search input', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    expect(wrapper.find('input[placeholder*="Search"]').exists()).toBe(true)
  })

  it('renders type dropdown with All option', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    const select = wrapper.find('select')
    expect(select.exists()).toBe(true)
    expect(select.text()).toContain('All')
  })

  it('renders archived checkbox', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    expect(wrapper.text()).toContain('Archived')
  })

  it('emits add-card on button click', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('add-card')).toBeTruthy()
  })

  it('emits update:modelValue when search changes', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    const input = wrapper.find('input[placeholder*="Search"]')
    await input.setValue('test query')
    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    const emitted = wrapper.emitted('update:modelValue')?.at(0)?.at(0) as BoardFilters
    expect(emitted?.search).toBe('test query')
  })

  it('emits update:modelValue when type changes', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    const select = wrapper.find('select')
    await select.setValue('1')
    const emitted = wrapper.emitted('update:modelValue')?.at(0)?.at(0) as BoardFilters
    expect(emitted?.type).toBe(1)
  })

  it('emits update:modelValue when archived toggles', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    const checkbox = wrapper.find('input[type="checkbox"]')
    await checkbox.setValue(true)
    const emitted = wrapper.emitted('update:modelValue')?.at(0)?.at(0) as BoardFilters
    expect(emitted?.includeArchived).toBe(true)
  })

  it('emits update:modelValue when hide empty toggles', async () => {
    const wrapper = await mountSuspended(BoardFilterBar, {
      props: { modelValue: defaultFilters }
    })
    const checkboxes = wrapper.findAll('input[type="checkbox"]')
    await checkboxes.at(1)!.setValue(true)
    const emitted = wrapper.emitted('update:modelValue')?.at(0)?.at(0) as BoardFilters
    expect(emitted?.hideEmptyColumns).toBe(true)
  })
})
