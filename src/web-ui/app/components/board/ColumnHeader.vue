<script setup lang="ts">
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']

defineProps<{
  column: ColumnResponse
  cardCount: number
}>()
</script>

<template>
  <div class="flex items-center justify-between px-2 py-2">
    <div class="flex items-center gap-2">
      <span class="column-drag-handle cursor-grab text-gray-300 hover:text-gray-500">
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
      <span class="text-xs text-gray-400 bg-gray-100 dark:bg-gray-700 rounded px-1.5 py-0.5">
        {{ cardCount }}
      </span>
      <span
        v-if="column.wipLimit && cardCount > Number(column.wipLimit)"
        class="text-xs text-red-500 font-medium"
      >
        WIP: {{ column.wipLimit }}
      </span>
    </div>
  </div>
</template>
