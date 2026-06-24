<script setup lang="ts">
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  card: CardResponse
}>()

const emit = defineEmits<{
  click: [card: CardResponse]
}>()

const cardTypeIcons: Record<number, string> = {
  0: 'i-lucide-square', // Task
  1: 'i-lucide-bug', // Bug
  2: 'i-lucide-layers', // Epic
  3: 'i-lucide-file-text', // Spec
  4: 'i-lucide-lightbulb' // Idea
}

const typeIcon = computed(() => cardTypeIcons[props.card.type] ?? 'i-lucide-square')

const formattedDue = computed(() => {
  if (!props.card.dueAt) return null
  return new Date(props.card.dueAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
})

const isOverdue = computed(() => {
  if (!props.card.dueAt) return false
  return new Date(props.card.dueAt) < new Date()
})

const plainDescription = computed(() =>
  (props.card.description ?? '').replace(/<[^>]*>/g, '')
)
</script>

<template>
  <div
    class="bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-700 p-3 cursor-pointer hover:shadow-md transition-shadow"
    @click="emit('click', card)"
  >
    <div class="flex items-start gap-2">
      <UIcon
        :name="typeIcon"
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
        <h4 class="text-sm font-medium text-gray-900 dark:text-gray-100 truncate">
          {{ card.title }}
        </h4>
        <p
          v-if="plainDescription"
          class="text-xs text-gray-500 mt-1 line-clamp-2"
        >
          {{ plainDescription }}
        </p>
      </div>
    </div>

    <div class="flex items-center justify-between mt-2">
      <div class="flex items-center gap-2">
        <div
          v-if="card.assignees.length > 0"
          class="flex -space-x-1"
        >
          <div
            v-for="assignee in card.assignees.slice(0, 3)"
            :key="assignee.userId"
            class="size-5 rounded-full bg-primary text-white flex items-center justify-center text-xs"
            :title="assignee.username"
          >
            {{ (assignee.username[0] ?? '?').toUpperCase() }}
          </div>
        </div>
      </div>

      <div class="flex items-center gap-2">
        <span
          v-if="formattedDue"
          class="text-xs"
          :class="isOverdue ? 'text-red-500 font-medium' : 'text-gray-400'"
        >
          <UIcon
            name="i-lucide-calendar"
            class="size-3 inline"
          />
          {{ formattedDue }}
        </span>
      </div>
    </div>
  </div>
</template>
