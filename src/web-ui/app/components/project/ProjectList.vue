<script setup lang="ts">
import type { components } from '~/types/api'

type ProjectListResponse = components['schemas']['ProjectListResponse']

defineProps<{
  projects: ProjectListResponse[]
  loading: boolean
}>()

const emit = defineEmits<{
  select: [projectId: string]
}>()
</script>

<template>
  <div
    v-if="loading"
    class="flex justify-center p-8"
  >
    <UIcon
      name="i-lucide-loader"
      class="animate-spin size-8"
    />
  </div>

  <div
    v-else-if="projects.length === 0"
    class="text-center p-8 text-muted"
  >
    <p>No projects yet. Create your first project!</p>
  </div>

  <div
    v-else
    class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3"
  >
    <UCard
      v-for="project in projects"
      :key="project.id"
      class="cursor-pointer hover:ring-2 hover:ring-primary transition-shadow"
      @click="emit('select', project.id)"
    >
      <template #header>
        <h3 class="font-semibold truncate">
          {{ project.name }}
        </h3>
      </template>
      <p class="text-sm text-muted line-clamp-2">
        {{ project.description ?? 'No description' }}
      </p>
    </UCard>
  </div>
</template>
