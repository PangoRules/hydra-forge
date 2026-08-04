<script setup lang="ts" generic="T">
import type { TableColumn } from '@nuxt/ui'

const props = withDefaults(defineProps<{
  data: T[]
  columns: TableColumn<T>[]
  loading: boolean
  page: number
  pageSize: number
  totalCount: number
  pageSizeOptions?: number[]
  rowKey: (item: T) => string
  selectable?: boolean
  expanded?: Record<string, boolean>
  /**
   * Opt-in: fill the parent's height and scroll rows internally instead of
   * letting the page scroll. Only meaningful when an ancestor actually
   * clamps height (e.g. a `flex-1 ... min-h-0` dashboard layout) — on a
   * normal page that just grows with content, leave this off (default).
   */
  fillHeight?: boolean
  /**
   * Opt-in: hide the rows-per-page/pagination footer entirely. For a fixed-size
   * preview list (e.g. "last 10" on a dashboard) with no `@update:page`/
   * `@update:pageSize` handler wired up — showing controls that don't do
   * anything is worse than not showing them.
   */
  hideFooter?: boolean
}>(), {
  pageSizeOptions: () => [10, 20, 50],
  selectable: false,
  expanded: undefined,
  fillHeight: false,
  hideFooter: false
})

const emit = defineEmits<{
  'update:page': [number]
  'update:pageSize': [number]
  'select': [T]
  'update:expanded': [Record<string, boolean> | boolean]
}>()

const rangeStart = computed(() => props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1)
const rangeEnd = computed(() => Math.min(props.page * props.pageSize, props.totalCount))
const isEmpty = computed(() => !props.loading && props.data.length === 0)
</script>

<template>
  <div :class="fillHeight ? 'h-full flex flex-col min-h-0' : ''">
    <div
      v-if="loading && data.length === 0"
      :class="fillHeight ? 'flex-1' : ''"
      class="flex justify-center items-center p-8 min-h-50"
    >
      <UIcon
        name="i-lucide-loader-circle"
        class="animate-spin size-8"
      />
    </div>

    <div
      v-else-if="isEmpty"
      :class="fillHeight ? 'flex-1' : ''"
      class="text-center p-8 text-muted min-h-50 flex items-center justify-center"
    >
      <p>No results found.</p>
    </div>

    <template v-else>
      <div :class="fillHeight ? 'flex-1 min-h-0 overflow-auto' : ''">
        <UTable
          :data="data"
          :columns="columns"
          :loading="loading"
          :get-row-id="rowKey"
          :expanded="expanded ?? {}"
          :sticky="fillHeight ? 'header' : undefined"
          class="w-full"
          :class="[$slots.card ? 'hidden md:block' : '', fillHeight ? 'h-full [&_td:not([colspan])]:max-w-64 [&_td:not([colspan])]:truncate' : '']"
          :meta="{ class: { tr: selectable ? 'cursor-pointer' : '' } }"
          @select="(_e, row) => emit('select', row.original)"
          @update:expanded="emit('update:expanded', $event as Record<string, boolean>)"
        >
          <template
            v-for="(_, name) in $slots"
            #[name]="slotProps"
          >
            <slot
              v-if="name !== 'card' && name !== 'footer'"
              :name="name"
              v-bind="slotProps"
            />
          </template>
        </UTable>

        <div
          v-if="$slots.card"
          class="md:hidden grid gap-4 sm:grid-cols-2 lg:grid-cols-3"
        >
          <slot
            v-for="item in data"
            :key="rowKey(item)"
            name="card"
            :item="item"
          />
        </div>
      </div>
    </template>

    <div
      v-if="$slots.footer"
      :class="fillHeight ? 'shrink-0' : ''"
    >
      <slot name="footer" />
    </div>

    <div
      v-if="totalCount > 0 && !hideFooter"
      :class="fillHeight ? 'shrink-0' : ''"
      class="flex flex-col gap-3 py-4 sm:py-6 border-t border-gray-200 dark:border-gray-700"
    >
      <div class="flex flex-col sm:flex-row items-center sm:justify-between gap-3 sm:gap-4">
        <div class="flex items-center gap-2">
          <span class="text-xs sm:text-sm text-gray-500 dark:text-gray-400 whitespace-nowrap">Rows per page:</span>
          <USelect
            :model-value="pageSize"
            :items="pageSizeOptions.map(v => ({ label: String(v), value: v }))"
            class="w-16 sm:w-20"
            @update:model-value="emit('update:pageSize', Number($event))"
          />
          <span class="text-xs sm:text-sm text-gray-500 dark:text-gray-400 whitespace-nowrap">
            {{ rangeStart }}-{{ rangeEnd }} of {{ totalCount }}
          </span>
        </div>
        <UPagination
          :page="page"
          :total="totalCount"
          :items-per-page="pageSize"
          size="sm"
          @update:page="emit('update:page', $event)"
        />
      </div>
    </div>
  </div>
</template>
