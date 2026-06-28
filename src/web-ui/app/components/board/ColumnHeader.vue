<script setup lang="ts">
import type { components } from '~/types/api'
import { CARD_TYPE_FILTER_OPTIONS } from '~/lib/card-type'

type ColumnResponse = components['schemas']['ColumnResponse']

const props = defineProps<{
  column: ColumnResponse
  cardCount: number
  includeArchived: boolean
  readonly?: boolean
  canMoveLeft?: boolean
  canMoveRight?: boolean
}>()

const isDragging = ref(false)

const emit = defineEmits<{
  'add-card': []
  'filter-type': [value: number | null]
  'filter-archived': [value: boolean]
  'reorder': [draggedColumnId: string, targetColumnId: string]
  'move-left': []
  'move-right': []
}>()

function handleDragStart(event: DragEvent) {
  if (!event.dataTransfer) return
  event.dataTransfer.setData('text/plain', props.column.id)
  event.dataTransfer.effectAllowed = 'move'
  isDragging.value = true
}

function handleDragEnd() {
  isDragging.value = false
}

function handleDragOver(event: DragEvent) {
  if (!event.dataTransfer) return
  event.dataTransfer.dropEffect = 'move'
}

function handleDrop(event: DragEvent) {
  if (!event.dataTransfer) return
  const draggedColumnId = event.dataTransfer.getData('text/plain')
  if (draggedColumnId === props.column.id) return
  emit('reorder', draggedColumnId, props.column.id)
}
</script>

<template>
  <div
    class="px-2 pt-2 pb-1 group"
    :class="{ 'opacity-50': isDragging }"
    :draggable="!readonly"
    @dragstart="handleDragStart"
    @dragend="handleDragEnd"
    @dragover.prevent="handleDragOver"
    @drop.prevent="handleDrop"
  >
    <!-- Row 1: title + metadata only -->
    <div class="flex items-center gap-2 mb-2">
      <span
        class="column-drag-handle cursor-grab text-gray-300 hover:text-gray-500 shrink-0"
        @mousedown.stop
      >
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
      <span
        class="text-xs text-gray-400 bg-gray-100 dark:bg-gray-700 rounded px-1.5 py-0.5 shrink-0"
      >
        {{ cardCount }}
      </span>
      <UButton
        v-if="canMoveLeft && !readonly"
        icon="i-lucide-chevron-left"
        size="xs"
        variant="ghost"
        color="neutral"
        class="opacity-0 group-hover:opacity-100 transition-opacity"
        @click.stop="emit('move-left')"
      />
      <UButton
        v-if="canMoveRight && !readonly"
        icon="i-lucide-chevron-right"
        size="xs"
        variant="ghost"
        color="neutral"
        class="opacity-0 group-hover:opacity-100 transition-opacity"
        @click.stop="emit('move-right')"
      />
      <span
        v-if="column.wipLimit && cardCount > Number(column.wipLimit)"
        class="text-xs text-red-500 font-medium shrink-0"
      >
        WIP {{ column.wipLimit }}
      </span>
    </div>

    <!-- Row 2: filter controls -->
    <div class="flex items-center gap-2 mb-1">
      <span class="text-xs text-gray-500 shrink-0">Type:</span>
      <select
        class="text-xs px-2 py-1 border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
        @change="
          emit(
            'filter-type',
            ($event.target as HTMLSelectElement).value
              ? Number(($event.target as HTMLSelectElement).value)
              : null
          )
        "
      >
        <option
          v-for="t in CARD_TYPE_FILTER_OPTIONS"
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
        v-if="!readonly"
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
