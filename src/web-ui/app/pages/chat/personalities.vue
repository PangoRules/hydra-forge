<script setup lang="ts">
import { UiRoutes } from '~/lib/routes'
import PersonalityManager from '~/components/chat/PersonalityManager.vue'

definePageMeta({ middleware: ['auth'] })
</script>

<template>
  <div class="flex flex-col h-full">
    <div class="shrink-0 flex items-center gap-3 p-4 border-b border-gray-200 dark:border-gray-700">
      <UButton
        icon="i-lucide-arrow-left"
        variant="ghost"
        color="neutral"
        size="sm"
        aria-label="Back to chats"
        :to="UiRoutes.Chats"
      />
      <h1 class="text-lg font-bold">
        Personalities
      </h1>
    </div>
    <div class="flex-1 min-h-0 overflow-y-auto">
      <PersonalityManager />
    </div>
  </div>
</template>
