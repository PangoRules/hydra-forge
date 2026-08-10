<script setup lang="ts">
interface Props {
  title: string
  backTo?: string
}

const props = defineProps<Props>()
</script>

<template>
  <div class="shrink-0 flex items-center gap-3 p-4 border-b border-gray-200 dark:border-gray-700">
    <UButton
      icon="i-lucide-arrow-left"
      variant="ghost"
      color="neutral"
      size="sm"
      aria-label="Back to chats"
      :to="props.backTo"
    />
    <h1 class="text-lg font-bold">
      {{ props.title }}
    </h1>
  </div>
</template>
