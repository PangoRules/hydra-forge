<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes, UiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'

definePageMeta({ middleware: ['auth'] })

type ProjectListResponse = components['schemas']['ProjectListResponse']

const projects = ref<ProjectListResponse[]>([])
const loading = ref(true)
const showCreateModal = ref(false)
const showArchived = ref(false)

const api = useApi()
const toast = useToast()

const archiveTarget = ref<{ id: string, name: string } | null>(null)
const showArchiveConfirm = computed({
  get: () => archiveTarget.value !== null,
  set: (v: boolean) => { if (!v) archiveTarget.value = null }
})

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

function handleArchive(projectId: string) {
  const project = projects.value.find(p => p.id === projectId)
  archiveTarget.value = { id: projectId, name: project?.name ?? projectId.slice(0, 8) }
}

async function confirmArchive() {
  if (!archiveTarget.value) return
  const id = archiveTarget.value.id
  archiveTarget.value = null
  try {
    await api.POST(ApiRoutes.Projects.archive(id))
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
  <div class="min-h-screen flex flex-col">
    <div class="p-4 sm:p-6 lg:p-8 pb-0 max-w-6xl mx-auto w-full flex-1 flex flex-col">
      <div class="flex items-center justify-between pb-4 mb-6 border-b border-gray-200 dark:border-gray-700">
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

      <div class="flex-1">
        <ProjectList
          :projects="projects"
          :loading="loading"
          @select="onProjectSelect"
          @archive="handleArchive"
          @restore="handleRestore"
        />
      </div>
    </div>

    <ProjectCreateModal
      v-model:open="showCreateModal"
      @created="onProjectCreated"
    />

    <ConfirmDialog
      v-model:open="showArchiveConfirm"
      title="Archive project"
      :message="archiveTarget ? `Archive ${archiveTarget.name}? This will hide it from the default project list.` : ''"
      confirm-text="Archive"
      @confirm="confirmArchive"
    />
  </div>
</template>
