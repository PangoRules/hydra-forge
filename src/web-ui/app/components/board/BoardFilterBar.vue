<script setup lang="ts">
import type { components } from '~/types/api'
import { onClickOutside } from '@vueuse/core'

type MemberResponse = components['schemas']['MemberResponse']
type ColumnResponse = components['schemas']['ColumnResponse']

defineProps<{
  members?: MemberResponse[]
  columns: ColumnResponse[]
  readonly?: boolean
}>()

const emit = defineEmits<{
  'add-card': []
}>()

const { search, assigneeUserId, includeArchived, hideEmptyColumns, visibleColumnIds, columnSelectionActive, toggleColumnVisibility } = useBoardFilters()

const showColumnPicker = ref(false)
const columnPickerRef = ref<HTMLElement | null>(null)
onClickOutside(columnPickerRef, () => {
  showColumnPicker.value = false
})
</script>

<template>
  <div
    class="flex items-center gap-3 px-4 py-2 border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 flex-wrap"
  >
    <!-- Search -->
    <input
      v-model="search"
      placeholder="Search cards..."
      class="flex-1 min-w-[160px] px-3 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800 focus:outline-none focus:ring-2 focus:ring-primary"
    >

    <!-- Column visibility dropdown -->
    <div
      ref="columnPickerRef"
      class="relative"
    >
      <UButton
        size="sm"
        variant="outline"
        data-testid="column-visibility-trigger"
        @click="showColumnPicker = !showColumnPicker"
      >
        {{ columnSelectionActive ? `${visibleColumnIds.length} column${visibleColumnIds.length > 1 ? 's' : ''}` : 'All columns' }}
      </UButton>
      <div
        v-if="showColumnPicker"
        class="absolute top-full left-0 mt-1 z-50 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-md shadow-lg p-2 min-w-[160px]"
      >
        <label
          v-for="col in columns"
          :key="col.id"
          class="flex items-center gap-2 px-2 py-1 text-xs hover:bg-gray-100 dark:hover:bg-gray-700 rounded cursor-pointer whitespace-nowrap"
        >
          <input
            type="checkbox"
            class="rounded"
            :checked="visibleColumnIds.includes(col.id)"
            @change="toggleColumnVisibility(col.id)"
          >
          {{ col.name }}
        </label>
      </div>
    </div>

    <!-- Assignee filter -->
    <div class="flex items-center gap-1.5 shrink-0">
      <span class="text-xs text-gray-500 whitespace-nowrap">Assignee:</span>
      <select
        :value="assigneeUserId ?? ''"
        class="px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800 max-w-[140px]"
        @change="assigneeUserId = ($event.target as HTMLSelectElement).value || null"
      >
        <option value="">
          All
        </option>
        <option
          v-for="m in members"
          :key="m.userId"
          :value="m.userId"
        >
          {{ m.username }}
        </option>
      </select>
    </div>

    <!-- Toggles -->
    <label class="flex items-center gap-1.5 text-sm whitespace-nowrap cursor-pointer shrink-0">
      <input
        v-model="includeArchived"
        type="checkbox"
        class="rounded"
      >
      Archived
    </label>

    <label
      class="flex items-center gap-1.5 text-sm whitespace-nowrap shrink-0"
      :class="columnSelectionActive ? 'opacity-40 cursor-not-allowed' : 'cursor-pointer'"
    >
      <input
        v-model="hideEmptyColumns"
        type="checkbox"
        data-testid="hide-empty-checkbox"
        class="rounded"
        :disabled="columnSelectionActive"
      >
      Hide empty
    </label>
    <span
      v-if="columnSelectionActive"
      class="text-xs text-gray-400"
    >(column selected)</span>

    <UButton
      v-if="!readonly"
      size="sm"
      icon="i-lucide-plus"
      color="primary"
      data-testid="add-card-btn"
      @click="emit('add-card')"
    >
      Add card
    </UButton>
  </div>
</template>
