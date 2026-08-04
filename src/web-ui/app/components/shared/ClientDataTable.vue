<script setup lang="ts" generic="T">
import type { TableColumn } from '@nuxt/ui'
import DataTable from '~/components/shared/DataTable.vue'

/**
 * Same look/feel and pagination footer as `DataTable`, but for a dataset
 * that's already fully loaded client-side (e.g. a "discover" probe result) —
 * paging is a local array slice instead of an `update:page` refetch.
 */
const props = withDefaults(defineProps<{
  data: T[]
  columns: TableColumn<T>[]
  rowKey: (item: T) => string
  loading?: boolean
  defaultPageSize?: number
  pageSizeOptions?: number[]
  fillHeight?: boolean
  selectable?: boolean
}>(), {
  loading: false,
  defaultPageSize: 10,
  pageSizeOptions: () => [10, 20, 50],
  fillHeight: false,
  selectable: false
})

const emit = defineEmits<{
  select: [T]
}>()

const page = ref(1)
const pageSize = ref(props.defaultPageSize)

// Filtering/searching upstream changes the array identity — jumping back to
// page 1 avoids landing on a now-empty page past the end of the new result set.
watch(() => props.data, () => {
  page.value = 1
})

const totalCount = computed(() => props.data.length)
const pagedData = computed(() => {
  const start = (page.value - 1) * pageSize.value
  return props.data.slice(start, start + pageSize.value)
})
</script>

<template>
  <DataTable
    :data="pagedData"
    :columns="columns"
    :loading="loading"
    :page="page"
    :page-size="pageSize"
    :total-count="totalCount"
    :page-size-options="pageSizeOptions"
    :row-key="rowKey"
    :fill-height="fillHeight"
    :selectable="selectable"
    @update:page="page = $event"
    @update:page-size="(v: number) => { pageSize = v; page = 1 }"
    @select="emit('select', $event)"
  >
    <template
      v-for="(_, name) in $slots"
      #[name]="slotProps"
    >
      <slot
        :name="name"
        v-bind="slotProps"
      />
    </template>
  </DataTable>
</template>
