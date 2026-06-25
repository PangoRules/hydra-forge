<script setup lang="ts">
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']

defineProps<{
  column: ColumnResponse
  cardCount: number
  includeArchived: boolean
}>()

const emit = defineEmits<{
  'add-card': []
  'filter-type': [value: number | null]
  'filter-archived': [value: boolean]
}>()

const cardTypes = [
  { label: 'All', value: null },
  { label: 'Task', value: 0 },
  { label: 'Bug', value: 1 },
  { label: 'Epic', value: 2 },
  { label: 'Spec', value: 3 },
  { label: 'Idea', value: 4 }
]
</script>

<template>
  <div class="px-2 pt-2 pb-1">
    <!-- Row 1: title + metadata only -->
    <div class="flex items-center gap-2 mb-2">
      <span class="column-drag-handle cursor-grab text-gray-300 hover:text-gray-500 shrink-0">
        <UIcon
          name="i-lucide-grip-vertical"
          class="size-4"
        />
      </span>
      <div
        v-if="column.color"
        class="size-3 rounded-full shrink-0"
        :style="{ backgroundColor: column.color }"
      />
      <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-200 truncate">
        {{ column.name }}
      </h3>
      <span class="text-xs text-gray-400 bg-gray-100 dark:bg-gray-700 rounded px-1.5 py-0.5 shrink-0">
        {{ cardCount }}
      </span>
      <span
        v-if="column.wipLimit && cardCount > Number(column.wipLimit)"
        class="text-xs text-red-500 font-medium shrink-0"
      >
        WIP {{ column.wipLimit }}
      </span>
    </div>

    <!-- Row 2: filter controls -->
    <div class="flex items-center gap-2 mb-1">
      <select
        class="text-xs px-2 py-1 border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
        @change="emit('filter-type', ($event.target as HTMLSelectElement).value ? Number(($event.target as HTMLSelectElement).value) : null)"
      >
        <option
          v-for="t in cardTypes"
          :key="t.label"
          :value="t.value ?? ''"
        >
          {{ t.label }}
        </option>
      </select>
      <label
        v-if="includeArchived"
        class="flex items-center gap-1 text-xs cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-700 px-2 py-1 rounded"
      >
        <input
          type="checkbox"
          class="size-3"
          @change="emit('filter-archived', ($event.target as HTMLInputElement).checked)"
        >
        <span class="text-gray-500">Archived only</span>
      </label>
      <button
        class="ml-auto text-xs px-2 py-1 rounded border border-primary text-primary bg-primary/5 hover:bg-primary/10"
        title="Add card to this column"
        @click="emit('add-card')"
      >
        + Add card
      </button>
    </div>

    <!-- Row 3: inline search slot -->
    <slot name="filter-row" />
  </div>
</template>
