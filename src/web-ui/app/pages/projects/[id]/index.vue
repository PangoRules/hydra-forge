<script setup lang="ts">
definePageMeta({ middleware: ['auth'] })

const route = useRoute()
const projectId = route.params.id as string
const activeTab = ref<'board' | 'docs'>('board')

watch(activeTab, (tab) => {
  useHead({
    bodyAttrs: {
      class: tab === 'board' ? 'md:overflow-hidden' : ''
    }
  })
}, { immediate: true })
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <!-- Tab bar -->
    <div class="flex items-center gap-1 px-4 pt-2 border-b border-gray-200 dark:border-gray-700">
      <UButton
        :variant="activeTab === 'board' ? 'solid' : 'ghost'"
        size="sm"
        @click="activeTab = 'board'"
      >
        <UIcon
          name="i-lucide-layout-columns"
          class="size-4 mr-1"
        />
        Board
      </UButton>
      <UButton
        :variant="activeTab === 'docs' ? 'solid' : 'ghost'"
        size="sm"
        @click="activeTab = 'docs'"
      >
        <UIcon
          name="i-lucide-file-text"
          class="size-4 mr-1"
        />
        Docs
      </UButton>
    </div>

    <!-- Tab content -->
    <ProjectBoard
      v-if="activeTab === 'board'"
      :project-id="projectId"
    />
    <ProjectDocuments
      v-else-if="activeTab === 'docs'"
      :project-id="projectId"
    />
  </div>
</template>
