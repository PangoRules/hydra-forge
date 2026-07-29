import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import DataTable from '~/components/shared/DataTable.vue'

interface Item { id: string, name: string }

const columns = [{ accessorKey: 'name', header: 'Name' }]
const items: Item[] = [
  { id: '1', name: 'Alpha' },
  { id: '2', name: 'Beta' }
]
// DataTable is a generic SFC; Vue's generated type doesn't propagate T through
// mountSuspended from a plain .ts test file, so the prop value is cast here.
const rowKey = ((item: Item) => item.id) as (item: unknown) => string

describe('DataTable', () => {
  it('renders the table with provided columns and data', async () => {
    const wrapper = await mountSuspended(DataTable, {
      props: {
        data: items,
        columns,
        loading: false,
        page: 1,
        pageSize: 10,
        totalCount: 2,
        rowKey,
      }
    })
    expect(wrapper.text()).toContain('Alpha')
    expect(wrapper.text()).toContain('Beta')
  })

  it('shows the loading spinner when loading and no data yet', async () => {
    const wrapper = await mountSuspended(DataTable, {
      props: {
        data: [],
        columns,
        loading: true,
        page: 1,
        pageSize: 10,
        totalCount: 0,
        rowKey,
      }
    })
    expect(wrapper.find('.animate-spin').exists()).toBe(true)
  })

  it('shows an empty state when not loading and data is empty', async () => {
    const wrapper = await mountSuspended(DataTable, {
      props: {
        data: [],
        columns,
        loading: false,
        page: 1,
        pageSize: 10,
        totalCount: 0,
        rowKey,
      }
    })
    expect(wrapper.text()).toContain('No results found.')
  })

  it('computes the "X-Y of Z" range text', async () => {
    const wrapper = await mountSuspended(DataTable, {
      props: {
        data: items,
        columns,
        loading: false,
        page: 2,
        pageSize: 10,
        totalCount: 25,
        rowKey,
      }
    })
    expect(wrapper.text()).toContain('11-20 of 25')
  })

  it('renders a card slot per item when provided, alongside the table', async () => {
    const wrapper = await mountSuspended(DataTable, {
      props: {
        data: items,
        columns,
        loading: false,
        page: 1,
        pageSize: 10,
        totalCount: 2,
        rowKey,
      },
      slots: {
        card: (props: { item: Item }) => `Card:${props.item.name}`
      } as any
    })
    expect(wrapper.text()).toContain('Card:Alpha')
    expect(wrapper.text()).toContain('Card:Beta')
    expect(wrapper.find('table').exists()).toBe(true)
  })

  it('emits update:pageSize when the rows-per-page select changes', async () => {
    const wrapper = await mountSuspended(DataTable, {
      props: {
        data: items,
        columns,
        loading: false,
        page: 1,
        pageSize: 10,
        totalCount: 25,
        rowKey,
      }
    })
    const select = wrapper.findComponent({ name: 'USelect' })
    await select.vm.$emit('update:model-value', 20)
    expect(wrapper.emitted('update:pageSize')?.[0]).toEqual([20])
  })

  it('emits update:page when pagination changes', async () => {
    const wrapper = await mountSuspended(DataTable, {
      props: {
        data: items,
        columns,
        loading: false,
        page: 1,
        pageSize: 10,
        totalCount: 25,
        rowKey,
      }
    })
    const pagination = wrapper.findComponent({ name: 'UPagination' })
    await pagination.vm.$emit('update:page', 2)
    expect(wrapper.emitted('update:page')?.[0]).toEqual([2])
  })
})
