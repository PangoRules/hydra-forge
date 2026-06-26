<script setup lang="ts">
import type { components } from '~/types/api'

const {
  selectedCount,
  bulkTargetColumnId,
  columns
} = defineProps<{
  selectedCount: number
  bulkTargetColumnId: string | null
  columns: components['schemas']['ColumnResponse'][]
}>()

const emit = defineEmits([
  'update:bulkTargetColumnId',
  'move',
  'archive',
  'clear'
])

function onChange(e: Event) {
  const v = (e.target as HTMLSelectElement).value
  emit('update:bulkTargetColumnId', v === 'null' ? null : v)
}
</script>

<template>
  <div
    v-if="selectedCount > 0"
    class="px-4 py-2 bg-gray-50 dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700"
  >
    <!-- DEBUG: always visible so we can see if BulkActionBar renders at all -->
    <div class="md:hidden bg-red-500 text-white text-xs">
      DEBUG MOBILE: selectedCount={{ selectedCount }}
    </div>
    <div class="hidden md:block bg-blue-500 text-white text-xs">
      DEBUG DESKTOP: selectedCount={{ selectedCount }}
    </div>
    <!-- Mobile: keep existing compact layout -->
    <div class="flex gap-2 flex-wrap items-center md:hidden">
      <span class="text-sm">{{ selectedCount }} selected</span>

      <select
        :value="bulkTargetColumnId"
        class="text-xs px-2 py-1 border rounded bg-white dark:bg-gray-800 dark:border-gray-600"
        @change="onChange"
      >
        <option :value="null">
          Move to...
        </option>
        <option
          v-for="col in columns"
          :key="col.id"
          :value="col.id"
        >
          {{ col.name }}
        </option>
      </select>

      <UButton
        size="sm"
        variant="ghost"
        @click="$emit('move')"
      >
        Move
      </UButton>

      <UButton
        size="sm"
        variant="ghost"
        @click="$emit('archive')"
      >
        Archive
      </UButton>

      <UButton
        size="sm"
        variant="ghost"
        @click="$emit('clear')"
      >
        Clear
      </UButton>
    </div>

    <!-- Desktop: toolbar style -->
    <div class="hidden md:flex items-center gap-3 justify-end">
      <span class="text-sm">{{ selectedCount }} selected</span>

      <div class="flex items-center gap-2">
        <label class="text-xs text-gray-500 mr-2">Move to</label>

        <select
          :value="bulkTargetColumnId"
          class="text-xs px-2 py-1 border rounded bg-white dark:bg-gray-800 dark:border-gray-600"
          @change="onChange"
        >
          <option :value="null">
            Choose column
          </option>
          <option
            v-for="col in columns"
            :key="col.id"
            :value="col.id"
          >
            {{ col.name }}
          </option>
        </select>

        <UButton
          size="sm"
          variant="ghost"
          @click="$emit('move')"
        >
          Move
        </UButton>
      </div>

      <UButton
        size="sm"
        variant="ghost"
        @click="$emit('archive')"
      >
        Archive
      </UButton>

      <UButton
        size="sm"
        variant="ghost"
        @click="$emit('clear')"
      >
        Clear
      </UButton>
    </div>
  </div>
</template>
