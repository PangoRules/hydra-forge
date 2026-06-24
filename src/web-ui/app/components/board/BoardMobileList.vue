<script setup lang="ts">
import type { components } from '~/types/api'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']

defineProps<{
  columns: ColumnResponse[]
  cardsByColumn: Map<string, CardResponse[]>
}>()

const emit = defineEmits<{
  'card-click': [card: CardResponse]
}>()

const typeIcons: Record<number, string> = {
  0: 'i-lucide-square',
  1: 'i-lucide-bug',
  2: 'i-lucide-layers',
  3: 'i-lucide-file-text',
  4: 'i-lucide-lightbulb'
}

function stripHtml(text: string): string {
  return text.replace(/<[^>]*>/g, '')
}
</script>

<template>
  <div class="p-4 space-y-6">
    <div
      v-for="column in columns"
      :key="column.id"
    >
      <div class="flex items-center gap-2 mb-3 pb-2 border-b border-gray-200 dark:border-gray-700">
        <h3 class="font-semibold text-sm">
          {{ column.name }}
        </h3>
        <span class="text-xs text-gray-400 bg-gray-100 dark:bg-gray-700 rounded px-1.5 py-0.5">
          {{ cardsByColumn.get(column.id)?.length ?? 0 }}
        </span>
        <span
          v-if="column.wipLimit"
          class="text-xs"
          :class="Number(column.wipLimit) < (cardsByColumn.get(column.id)?.length ?? 0) ? 'text-red-500 font-medium' : 'text-gray-400'"
        >
          WIP {{ column.wipLimit }}
        </span>
      </div>

      <div class="space-y-2">
        <div
          v-for="card in cardsByColumn.get(column.id) ?? []"
          :key="card.id"
          class="bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-700 p-3 cursor-pointer hover:shadow-sm transition-shadow"
          @click="emit('card-click', card)"
        >
          <div class="flex items-start gap-2">
            <UIcon
              :name="typeIcons[card.type] ?? 'i-lucide-square'"
              class="size-4 mt-0.5 shrink-0 text-gray-400"
            />
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 mb-1">
                <span class="text-xs font-medium text-gray-500">#{{ card.cardNumber }}</span>
                <span
                  v-if="card.archivedAt"
                  class="text-xs text-gray-400"
                >
                  archived
                </span>
              </div>
              <p class="text-sm font-medium text-gray-900 dark:text-gray-100 truncate">
                {{ card.title }}
              </p>
              <p
                v-if="card.description"
                class="text-xs text-gray-500 mt-1 line-clamp-2"
              >
                {{ stripHtml(card.description) }}
              </p>
            </div>
          </div>

          <div
            v-if="card.assignees.length > 0"
            class="flex items-center justify-between mt-2"
          >
            <div class="flex -space-x-1">
              <div
                v-for="assignee in card.assignees.slice(0, 3)"
                :key="assignee.userId"
                class="size-5 rounded-full bg-primary text-white flex items-center justify-center text-xs"
                :title="assignee.username"
              >
                {{ (assignee.username[0] ?? '?').toUpperCase() }}
              </div>
            </div>
            <div
              v-if="card.dueAt"
              class="text-xs"
              :class="new Date(card.dueAt) < new Date() ? 'text-red-500 font-medium' : 'text-gray-400'"
            >
              {{ new Date(card.dueAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }) }}
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
