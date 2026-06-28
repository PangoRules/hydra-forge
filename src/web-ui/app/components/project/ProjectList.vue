<script setup lang="ts">
import type { components } from '~/types/api'
import ProjectCard from '~/components/project/ProjectCard.vue'

type ProjectListResponse = components['schemas']['ProjectListResponse']

defineProps<{
  projects: ProjectListResponse[]
  loading: boolean
}>()

const emit = defineEmits<{
  'select': [projectId: string]
  'toggle-archive': [project: { id: string, name: string, archivedAt: string | null }]
  'edit': [projectId: string]
}>()
</script>

<template>
  <div
    v-if="loading"
    class="flex justify-center items-center p-8 min-h-[200px]"
  >
    <UIcon
      name="i-lucide-loader"
      class="animate-spin size-8"
    />
  </div>

  <div
    v-else-if="projects.length === 0"
    class="text-center p-8 text-muted min-h-[200px] flex items-center justify-center"
  >
    <p>No projects yet. Create your first project!</p>
  </div>

  <div
    v-else
    class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3"
  >
    <ProjectCard
      v-for="project in projects"
      :key="project.id"
      :project="project"
      @select="emit('select', $event)"
      @toggle-archive="emit('toggle-archive', $event)"
      @edit="emit('edit', $event)"
    />
  </div>
</template>
