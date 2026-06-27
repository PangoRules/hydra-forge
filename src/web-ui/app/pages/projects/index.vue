<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes, UiRoutes } from '~/lib/routes'

definePageMeta({ middleware: ['auth'] })

type ProjectListResponse = components['schemas']['ProjectListResponse']

const projects = ref<ProjectListResponse[]>([])
const loading = ref(true)
const showCreateModal = ref(false)

const api = useApi()
const toast = useToast()

async function fetchProjects() {
  loading.value = true
  try {
    const { data, error } = await api.GET(ApiRoutes.Projects.list())
    if (error) throw error
    projects.value = (data as ProjectListResponse[]) ?? []
  } catch (e: unknown) {
    const message = e instanceof Error ? e.message : 'Failed to load projects'
    toast.add({ title: message, color: 'error' })
  } finally {
    loading.value = false
  }
}

function onProjectSelect(projectId: string) {
  navigateTo(UiRoutes.Projects.Board(projectId))
}

function onProjectCreated() {
  showCreateModal.value = false
  fetchProjects()
  toast.add({ title: 'Project created', color: 'success', duration: 4000 })
}

onMounted(() => fetchProjects())
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 max-w-6xl mx-auto">
    <div class="flex items-center justify-between mb-6">
      <h1 class="text-2xl font-bold">
        Projects
      </h1>
      <UButton @click="showCreateModal = true">
        New Project
      </UButton>
    </div>

    <ProjectList
      :projects="projects"
      :loading="loading"
      @select="onProjectSelect"
    />

    <ProjectCreateModal
      v-model:open="showCreateModal"
      @created="onProjectCreated"
    />
  </div>
</template>
