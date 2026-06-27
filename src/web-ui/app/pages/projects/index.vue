<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes, UiRoutes } from '~/lib/routes'

definePageMeta({ middleware: ['auth'] })

type ProjectListResponse = components['schemas']['ProjectListResponse']

const projects = ref<ProjectListResponse[]>([])
const loading = ref(true)
const showCreateModal = ref(false)
const showArchived = ref(false)

const api = useApi()
const toast = useToast()

async function fetchProjects() {
  loading.value = true
  try {
    const url = showArchived.value
      ? `${ApiRoutes.Projects.list()}?includeArchived=true`
      : ApiRoutes.Projects.list()
    const { data, error } = await api.GET(url)
    if (error) throw error
    projects.value = (data as ProjectListResponse[]) ?? []
  } catch (e: unknown) {
    const message = e instanceof Error ? e.message : 'Failed to load projects'
    toast.add({ title: message, color: 'error' })
  } finally {
    loading.value = false
  }
}

async function handleArchive(projectId: string) {
  try {
    await api.POST(ApiRoutes.Projects.archive(projectId))
    toast.add({ title: 'Project archived', color: 'success', duration: 4000 })
    fetchProjects()
  } catch {
    toast.add({ title: 'Failed to archive project', color: 'error' })
  }
}

async function handleRestore(projectId: string) {
  try {
    await api.POST(ApiRoutes.Projects.restore(projectId))
    toast.add({ title: 'Project restored', color: 'success', duration: 4000 })
    fetchProjects()
  } catch {
    toast.add({ title: 'Failed to restore project', color: 'error' })
  }
}

watch(showArchived, () => fetchProjects())

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
      <div class="flex items-center gap-4">
        <div class="flex items-center gap-2">
          <USwitch v-model="showArchived" />
          <span class="text-sm text-muted">Show archived</span>
        </div>
        <UButton @click="showCreateModal = true">
          New Project
        </UButton>
      </div>
    </div>

    <ProjectList
      :projects="projects"
      :loading="loading"
      @select="onProjectSelect"
      @archive="handleArchive"
      @restore="handleRestore"
    />

    <ProjectCreateModal
      v-model:open="showCreateModal"
      @created="onProjectCreated"
    />
  </div>
</template>
