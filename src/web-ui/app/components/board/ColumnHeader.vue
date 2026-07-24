<script setup lang="ts">
import type { components } from '~/types/api'
import { CARD_TYPE_FILTER_OPTIONS } from '~/lib/card-type'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import { onClickOutside } from '@vueuse/core'
import { onUnmounted } from 'vue'

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
  'filter-type': [value: string | null]
  'filter-archived': [value: boolean]
  'reorder': [draggedColumnId: string, targetColumnId: string]
  'move-left': []
  'move-right': []
  'update-column': [name: string, color: string | null, wipLimit: number | null]
  'delete-column': []
}>()

const showEdit = ref(false)
const editPanelRef = ref<HTMLElement | null>(null)
const editName = ref(props.column.name)
const editColor = ref(props.column.color ?? '#94a3b8')
const editWipLimitStr = ref(props.column.wipLimit != null ? String(props.column.wipLimit) : '')
const showDeleteConfirm = ref(false)

onClickOutside(editPanelRef, () => {
  showEdit.value = false
})

onUnmounted(() => {
  showEdit.value = false
})

function openEdit() {
  editName.value = props.column.name
  editColor.value = props.column.color ?? '#94a3b8'
  editWipLimitStr.value = props.column.wipLimit != null ? String(props.column.wipLimit) : ''
  showEdit.value = true
}

function saveEdit() {
  if (!editName.value.trim()) return
  const parsed = editWipLimitStr.value.trim() === '' ? null : Number(editWipLimitStr.value)
  const wipLimit = parsed !== null && !Number.isNaN(parsed) && parsed > 0 ? parsed : null
  emit('update-column', editName.value.trim(), editColor.value || null, wipLimit)
  showEdit.value = false
}

function confirmDelete() {
  emit('delete-column')
  showDeleteConfirm.value = false
}

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
      <span
        v-if="column.wipLimit && cardCount > Number(column.wipLimit)"
        class="text-xs text-red-500 font-medium shrink-0"
      >
        WIP {{ column.wipLimit }}
      </span>

      <div class="ml-auto flex items-center gap-1 shrink-0">
        <UButton
          v-if="canMoveLeft && !readonly"
          tabindex="-1"
          icon="i-lucide-chevron-left"
          size="xs"
          variant="ghost"
          color="neutral"
          class="opacity-0 group-hover:opacity-100 transition-opacity"
          @click.stop="emit('move-left')"
        />
        <UButton
          v-if="canMoveRight && !readonly"
          tabindex="-1"
          icon="i-lucide-chevron-right"
          size="xs"
          variant="ghost"
          color="neutral"
          class="opacity-0 group-hover:opacity-100 transition-opacity"
          @click.stop="emit('move-right')"
        />
        <div
          v-if="!readonly"
          ref="editPanelRef"
          class="relative shrink-0 flex items-center"
        >
          <button
            tabindex="-1"
            class="text-gray-300 hover:text-gray-500 opacity-0 group-hover:opacity-100 transition-opacity"
            title="Edit column"
            data-testid="column-edit-trigger"
            @click.stop="showEdit ? (showEdit = false) : openEdit()"
          >
            <UIcon
              name="i-lucide-settings"
              class="size-4"
            />
          </button>
          <div
            v-if="showEdit"
            class="absolute z-20 top-full right-0 mt-1 w-56 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-md shadow-lg p-3 space-y-2"
            @mousedown.stop
          >
            <label class="block text-xs text-gray-500">
              Name
              <input
                v-model="editName"
                data-testid="column-name-input"
                class="mt-0.5 w-full px-2 py-1 text-sm border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
              >
            </label>
            <label class="flex items-center justify-between text-xs text-gray-500">
              Color
              <div class="flex items-center gap-1">
                <input
                  v-model="editColor"
                  type="color"
                  class="h-6 w-10 border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
                >
                <button
                  v-if="editColor"
                  class="text-gray-400 hover:text-red-500"
                  title="Reset color"
                  type="button"
                  @click="editColor = ''"
                >
                  <UIcon
                    name="i-lucide-x"
                    class="size-3"
                  />
                </button>
              </div>
            </label>
            <label class="block text-xs text-gray-500">
              WIP limit
              <input
                v-model="editWipLimitStr"
                type="number"
                min="0"
                class="mt-0.5 w-full px-2 py-1 text-sm border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
              >
            </label>
            <div class="flex items-center justify-between pt-1">
              <button
                class="text-xs text-red-500 hover:text-red-600"
                data-testid="column-delete-trigger"
                @click="showDeleteConfirm = true"
              >
                Delete
              </button>
              <UButton
                size="xs"
                data-testid="column-save-trigger"
                @click="saveEdit"
              >
                Save
              </UButton>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Row 2: filter controls -->
    <div class="flex items-center gap-2 mb-1">
      <span class="text-xs text-gray-500 shrink-0">Type:</span>
      <select
        tabindex="-1"
        class="text-xs px-2 py-1 border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
        @change="emit('filter-type', ($event.target as HTMLSelectElement).value || null)"
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
          tabindex="-1"
          class="size-3"
          @change="emit('filter-archived', ($event.target as HTMLInputElement).checked)"
        >
        <span class="text-gray-500">Archived only</span>
      </label>
      <button
        v-if="!readonly"
        tabindex="-1"
        class="ml-auto text-xs px-2 py-1 rounded border border-primary text-primary bg-primary/5 hover:bg-primary/10"
        title="Add card to this column"
        @click="emit('add-card')"
      >
        + Add card
      </button>
    </div>

    <!-- Row 3: inline search slot -->
    <slot name="filter-row" />

    <ConfirmDialog
      :open="showDeleteConfirm"
      title="Delete column"
      :message="`Delete '${column.name}'? This only works if the column has no cards.`"
      confirm-text="Delete"
      confirm-color="error"
      @update:open="showDeleteConfirm = $event"
      @confirm="confirmDelete"
    />
  </div>
</template>
