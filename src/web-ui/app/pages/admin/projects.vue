<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'

definePageMeta({ middleware: ['auth'] })

interface ProjectRow {
  id: string
  name: string
  description: string | null
  archivedAt: string | null
}

const user = useAuthStore().user
if (!user?.isAdmin) {
  navigateTo('/projects')
}

const api = useApi()
const toast = useToast()

const projects = ref<ProjectRow[]>([])
const totalCount = ref(0)
const loading = ref(false)
const search = ref('')
const page = ref(1)
const pageSize = 20

const columns = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'description', header: 'Description' },
  { accessorKey: 'archivedAt', header: 'Status' },
  { accessorKey: 'actions', header: 'Actions', enableSorting: false }
]

watch(search, () => {
  page.value = 1
  loadProjects()
})

async function loadProjects() {
  loading.value = true
  try {
    const { data, error } = await api.GET<{ items: ProjectRow[], totalCount: number }>(
      ApiRoutes.Admin.projectsList((page.value - 1) * pageSize, pageSize, search.value || undefined))
    if (error) throw error
    projects.value = data!.items
    totalCount.value = data!.totalCount
  } catch (e) {
    toast.add({ title: (e as Error).message || 'Failed to load projects', color: 'error' })
  } finally {
    loading.value = false
  }
}

onMounted(() => loadProjects())
</script>

<template>
  <div class="p-6">
    <h1 class="text-2xl font-bold mb-4">
      All Projects
    </h1>

    <UInput
      v-model="search"
      placeholder="Search projects..."
      class="mb-4"
    />

    <UTable
      :data="projects"
      :columns="columns"
      :loading="loading"
    >
      <template #archivedAt-cell="{ row }">
        <UBadge :color="row.original.archivedAt ? 'error' : 'success'">
          {{ row.original.archivedAt ? 'Archived' : 'Active' }}
        </UBadge>
      </template>
      <template #actions-cell="{ row }">
        <UButton
          size="xs"
          color="neutral"
          :to="`/projects/${row.original.id}/board`"
        >
          Open Board
        </UButton>
      </template>
    </UTable>

    <UPagination
      v-model:page="page"
      :total="totalCount"
      :page-size="pageSize"
      class="mt-4"
      @update:page="loadProjects"
    />

    <p class="text-sm text-gray-500 mt-3">
      {{ totalCount }} total projects
    </p>
  </div>
</template>
