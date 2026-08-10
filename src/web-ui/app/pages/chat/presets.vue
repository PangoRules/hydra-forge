<script setup lang="ts">
import { UiRoutes } from '~/lib/routes'
import PromptPresetManager from '~/components/chat/PromptPresetManager.vue'
import PageHeader from '~/components/shared/PageHeader.vue'

definePageMeta({ middleware: ['auth'] })
</script>

<template>
  <div class="flex flex-col h-full">
    <PageHeader
      title="Prompt Presets"
      :back-to="UiRoutes.Chats"
      aria-label="Back to chats"
    />
    <div class="flex-1 min-h-0 overflow-y-auto">
      <PromptPresetManager />
    </div>
  </div>
</template>
