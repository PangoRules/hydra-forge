<script setup lang="ts">
definePageMeta({ middleware: ['auth'] })
</script>

<template>
  <div class="flex flex-col items-center justify-center h-full text-center p-8">
    <UIcon
      name="i-lucide-message-circle"
      class="w-12 h-12 text-muted mb-4"
    />
    <h1 class="text-2xl font-bold mb-2">
      Chats
    </h1>
    <p class="text-muted mb-6">
      Your AI conversations will live here. This feature is coming soon.
    </p>
    <UButton
      label="New Chat"
      icon="i-lucide-plus"
      disabled
    />
  </div>
</template>
