<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes, UiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import ProjectFilterBar from '~/components/project/ProjectFilterBar.vue'
import ProjectListTable from '~/components/project/ProjectListTable.vue'

definePageMeta({ middleware: ['auth'] })

type ProjectListResponse = components['schemas']['ProjectListResponse']
type ProjectListPageResponse = components['schemas']['ProjectListPageResponse']

const projects = ref<ProjectListResponse[]>([])
const totalCount = ref(0)
const loading = ref(true)
const showCreateModal = ref(false)
const showArchived = ref(false)

const search = ref('')
const role = ref('all')
const sortBy = ref('CreatedAt')
const sortDescending = ref(true)
const page = ref(1)
const pageSize = ref(10)
const pageSizeOptions = [5, 10, 15]

const api = useApi()
const toast = useAppToast()

const archiveTarget = ref<{ id: string, name: string } | null>(null)
const showArchiveConfirm = computed({
  get: () => archiveTarget.value !== null,
  set: (v: boolean) => { if (!v) archiveTarget.value = null }
})

const editingProject = ref<{ id: string, name: string, description: string | null } | null>(null)

function handleEditProject(projectId: string) {
  const project = projects.value.find(p => p.id === projectId)
  if (project) {
    editingProject.value = { id: project.id, name: project.name, description: project.description ?? null }
  }
}

async function fetchProjects() {
  loading.value = true
  try {
    const params = new URLSearchParams()
    if (showArchived.value) params.set('includeArchived', 'true')
    if (search.value) params.set('search', search.value)
    if (role.value && role.value !== 'all') params.set('role', role.value)
    params.set('sortBy', sortBy.value)
    params.set('sortDescending', String(sortDescending.value))
    params.set('skip', String((page.value - 1) * pageSize.value))
    params.set('take', String(pageSize.value))

    const { data } = await api.GET<ProjectListPageResponse>(`${ApiRoutes.Projects.list()}?${params}`)
    projects.value = data?.items ?? []
    totalCount.value = data?.totalCount !== undefined ? Number(data.totalCount) : 0
  } catch (e: unknown) {
    const message = e instanceof Error ? e.message : 'Failed to load projects'
    toast.error(message)
  } finally {
    loading.value = false
  }
}

async function handleToggleArchive(project: { id: string, name: string, archivedAt: string | null }) {
  const isArchiving = !project.archivedAt
  if (isArchiving) {
    archiveTarget.value = { id: project.id, name: project.name }
    return
  }
  try {
    await api.POST(ApiRoutes.Projects.toggleArchive(project.id))
    toast.success('Project restored')
    fetchProjects()
  } catch {
    toast.error('Failed to restore project')
  }
}

async function confirmArchive() {
  if (!archiveTarget.value) return
  const id = archiveTarget.value.id
  archiveTarget.value = null
  try {
    await api.POST(ApiRoutes.Projects.toggleArchive(id))
    toast.success('Project archived')
    fetchProjects()
  } catch {
    toast.error('Failed to archive project')
  }
}

watch(showArchived, () => {
  page.value = 1
  fetchProjects()
})
watch(role, () => {
  page.value = 1
  fetchProjects()
})
watch(sortBy, () => {
  page.value = 1
  fetchProjects()
})
watch(sortDescending, () => {
  page.value = 1
  fetchProjects()
})
watch(page, () => fetchProjects())
watch(pageSize, () => { page.value = 1; fetchProjects() })

let searchTimer: ReturnType<typeof setTimeout> | null = null
watch(search, () => {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    page.value = 1
    fetchProjects()
    searchTimer = null
  }, 300)
})

function onProjectSelect(projectId: string) {
  navigateTo(UiRoutes.Projects.Board(projectId))
}

function onProjectCreated() {
  showCreateModal.value = false
  fetchProjects()
  toast.success('Project created')
}

onMounted(() => fetchProjects())

const rangeStart = computed(() => totalCount.value === 0 ? 0 : (page.value - 1) * pageSize.value + 1)
const rangeEnd = computed(() => Math.min(page.value * pageSize.value, totalCount.value))
</script>

<template>
  <div class="min-h-screen flex flex-col">
    <div class="p-4 sm:p-6 lg:p-8 pb-0 w-full flex-1 flex flex-col">
      <div class="flex items-center justify-between pb-4 mb-4 border-b border-gray-200 dark:border-gray-700">
        <h1 class="text-2xl font-bold">
          Projects
        </h1>
        <UButton @click="showCreateModal = true">
          New Project
        </UButton>
      </div>

      <ClientOnly>
        <ProjectFilterBar
          v-model:search="search"
          v-model:role="role"
          v-model:sort-by="sortBy"
          v-model:sort-descending="sortDescending"
          v-model:show-archived="showArchived"
          class="mb-6"
        />
        <template #fallback>
          <div class="mb-6 h-10" />
        </template>
      </ClientOnly>

      <div class="flex-1">
        <ProjectListTable
          class="hidden md:block"
          :projects="projects"
          :loading="loading"
          @select="onProjectSelect"
          @toggle-archive="handleToggleArchive"
          @edit="handleEditProject"
        />
        <ProjectList
          class="md:hidden"
          :projects="projects"
          :loading="loading"
          @select="onProjectSelect"
          @toggle-archive="handleToggleArchive"
          @edit="handleEditProject"
        />

        <ClientOnly>
          <div
            v-if="totalCount > 0"
            class="flex items-center justify-between py-6 border-t border-gray-200 dark:border-gray-700"
          >
            <div class="flex items-center gap-4">
              <div class="flex items-center gap-2">
                <span class="text-sm text-gray-500 dark:text-gray-400">Rows per page:</span>
                <USelect
                  :model-value="pageSize"
                  :items="pageSizeOptions.map(v => ({ label: String(v), value: v }))"
                  class="w-20"
                  @update:model-value="pageSize = Number($event)"
                />
              </div>
              <span class="text-sm text-gray-500 dark:text-gray-400">
                {{ rangeStart }}-{{ rangeEnd }} of {{ totalCount }}
              </span>
            </div>
            <UPagination
              v-model:page="page"
              :total="totalCount"
              :items-per-page="pageSize"
            />
          </div>
        </ClientOnly>
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

    <ProjectEditModal
      v-if="editingProject"
      :project-id="editingProject.id"
      :initial-name="editingProject.name"
      :initial-description="editingProject.description"
      @close="editingProject = null"
      @updated="editingProject = null; fetchProjects()"
    />
  </div>
</template>
