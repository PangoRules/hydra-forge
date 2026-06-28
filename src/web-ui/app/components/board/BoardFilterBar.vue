<script setup lang="ts">
import type { components } from '~/types/api'

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

    <!-- Column visibility chips -->
    <div class="flex items-center gap-1 flex-wrap">
      <span class="text-xs text-gray-500 whitespace-nowrap">Columns:</span>
      <button
        v-for="col in columns"
        :key="col.id"
        type="button"
        data-testid="column-chip"
        class="px-2 py-0.5 rounded-full text-xs border transition-colors"
        :class="visibleColumnIds.includes(col.id)
          ? 'bg-primary-500 text-white border-primary-500'
          : 'bg-white text-gray-600 border-gray-300 hover:border-primary-400'"
        @click="toggleColumnVisibility(col.id)"
      >
        {{ col.name }}
      </button>
    </div>

    <!-- Assignee filter -->
    <div class="flex items-center gap-1.5">
      <span class="text-xs text-gray-500 whitespace-nowrap">Assignee:</span>
      <select
        :value="assigneeUserId ?? ''"
        class="px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
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
      @click="emit('add-card')"
    >
      Add card
    </UButton>
  </div>
</template>
