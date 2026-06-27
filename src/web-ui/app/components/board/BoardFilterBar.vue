<script setup lang="ts">
import type { components } from '~/types/api'
import { CARD_TYPE_FILTER_OPTIONS } from '~/lib/card-type'

type MemberResponse = components['schemas']['MemberResponse']

defineProps<{
  members?: MemberResponse[]
}>()

const emit = defineEmits<{
  'add-card': []
}>()

const { search, type, assigneeUserId, includeArchived, hideEmptyColumns } = useBoardFilters()
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

    <!-- Type filter -->
    <div class="flex items-center gap-1.5">
      <span class="text-xs text-gray-500 whitespace-nowrap">Type:</span>
      <select
        :value="type ?? ''"
        class="px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
        @change="type = ($event.target as HTMLSelectElement).value ? Number(($event.target as HTMLSelectElement).value) : null"
      >
        <option
          v-for="t in CARD_TYPE_FILTER_OPTIONS"
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

    <label class="flex items-center gap-1.5 text-sm whitespace-nowrap cursor-pointer shrink-0">
      <input
        v-model="hideEmptyColumns"
        type="checkbox"
        class="rounded"
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
