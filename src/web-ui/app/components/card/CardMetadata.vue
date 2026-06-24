<script setup lang="ts">
import type { components } from '~/types/api'

type CardResponse = components['schemas']['CardResponse']

defineProps<{
  card: CardResponse
}>()
</script>

<template>
  <div class="space-y-4">
    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1">
        Type
      </p>
      <UBadge variant="subtle">
        {{ card.type }}
      </UBadge>
    </div>

    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1">
        Column
      </p>
      <p class="text-sm">
        {{ card.columnId.slice(0, 8) }}
      </p>
    </div>

    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1">
        Assignees
      </p>
      <div
        v-if="card.assignees?.length"
        class="flex flex-wrap gap-1"
      >
        <UAvatar
          v-for="a in card.assignees"
          :key="a.userId"
          :alt="a.username"
          size="sm"
        />
      </div>
      <p
        v-else
        class="text-sm text-muted"
      >
        None
      </p>
    </div>

    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1">
        Due Date
      </p>
      <p class="text-sm">
        {{ card.dueAt ?? 'None' }}
      </p>
    </div>
  </div>
</template>
