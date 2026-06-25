<script setup lang="ts">
import type { components } from '~/types/api'
import type { BoardFilters } from '~/stores/board'
import { CARD_TYPE_OPTIONS } from '~/lib/card-type'

type MemberResponse = components['schemas']['MemberResponse']

defineProps<{
  members?: MemberResponse[]
}>()

const filters = defineModel<BoardFilters>({ required: true })

const emit = defineEmits<{
  'add-card': []
}>()

function updateSearch(val: string) {
  filters.value = { ...filters.value, search: val }
}

function updateType(val: string | null) {
  filters.value = { ...filters.value, type: val !== '' ? Number(val) : null }
}

function updateAssignee(val: string | null) {
  filters.value = { ...filters.value, assigneeUserId: val ? val : null }
}

function toggleArchived() {
  filters.value = { ...filters.value, includeArchived: !filters.value.includeArchived }
}

function toggleHideEmpty() {
  filters.value = { ...filters.value, hideEmptyColumns: !filters.value.hideEmptyColumns }
}
</script>

<template>
  <div
    class="flex items-center gap-3 px-4 py-2 border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 flex-wrap"
  >
    <!-- Search -->
    <input
      :value="filters.search"
      placeholder="Search cards..."
      class="w-48 px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800 focus:outline-none focus:ring-2 focus:ring-primary"
      @input="updateSearch(($event.target as HTMLInputElement).value)"
    >

    <!-- Type filter -->
    <div class="flex items-center gap-1.5">
      <span class="text-xs text-gray-500 whitespace-nowrap">Type:</span>
      <select
        :value="filters.type ?? ''"
        class="px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
        @change="updateType(($event.target as HTMLSelectElement).value)"
      >
        <option
          v-for="t in CARD_TYPE_OPTIONS"
          :key="t.label"
          :value="t.value ?? ''"
        >
          {{ t.label }}
        </option>
      </select>
    </div>

    <!-- Assignee filter -->
    <div class="flex items-center gap-1.5">
      <span class="text-xs text-gray-500 whitespace-nowrap">Assignee:</span>
      <select
        :value="filters.assigneeUserId ?? ''"
        class="px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
        @change="updateAssignee(($event.target as HTMLSelectElement).value)"
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
    <label class="flex items-center gap-1.5 text-sm whitespace-nowrap cursor-pointer">
      <input
        type="checkbox"
        :checked="filters.includeArchived"
        class="rounded"
        @change="toggleArchived"
      >
      Archived
    </label>

    <label class="flex items-center gap-1.5 text-sm whitespace-nowrap cursor-pointer">
      <input
        type="checkbox"
        :checked="filters.hideEmptyColumns"
        class="rounded"
        @change="toggleHideEmpty"
      >
      Hide empty
    </label>

    <UButton
      size="sm"
      icon="i-lucide-plus"
      color="primary"
      @click="emit('add-card')"
    >
      Add card
    </UButton>
  </div>
</template>
