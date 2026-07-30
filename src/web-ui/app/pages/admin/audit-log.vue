<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { TableColumn } from '@nuxt/ui'

definePageMeta({ middleware: ['auth'] })

const user = useAuthStore().user
if (!user?.isAdmin) {
  navigateTo('/projects')
}

const api = useApi()
const toast = useToast()

interface AuditEntry {
  id: string
  projectId: string | null
  actorId: string
  actorName: string
  entityType: string
  entityId: string
  action: string
  oldValue: string | null
  newValue: string | null
  timestamp: string
  scope: string
}

const entries = ref<AuditEntry[]>([])
const totalCount = ref(0)
const loading = ref(false)

// Filters
const filterProjectId = ref('')
const filterActorId = ref('')
const filterEntityType = ref('')
const filterAction = ref('')
const filterFrom = ref('')
const filterTo = ref('')
const page = ref(1)
const pageSize = 50

const entityTypes = ['Card', 'Column', 'Project', 'Comment', 'Attachment', 'Spec', 'Plan', 'Checklist', 'CardRelationship']
const actions = ['Created', 'Updated', 'Deleted', 'Archived', 'Restored', 'Moved', 'Assigned', 'Unassigned']

const columns: TableColumn<AuditEntry>[] = [
  { accessorKey: 'timestamp', header: 'Timestamp' },
  { accessorKey: 'actorName', header: 'Actor' },
  { accessorKey: 'entityType', header: 'Entity Type' },
  { accessorKey: 'entityId', header: 'Entity ID' },
  { accessorKey: 'action', header: 'Action' },
  { accessorKey: 'scope', header: 'Scope' },
  { accessorKey: 'id', header: '' }
]

async function loadEntries() {
  loading.value = true
  try {
    const params = new URLSearchParams()
    params.set('skip', String((page.value - 1) * pageSize))
    params.set('take', String(pageSize))
    if (filterProjectId.value) params.set('projectId', filterProjectId.value)
    if (filterActorId.value) params.set('actorId', filterActorId.value)
    if (filterEntityType.value) params.set('entityType', filterEntityType.value)
    if (filterAction.value) params.set('action', filterAction.value)
    if (filterFrom.value) params.set('from', filterFrom.value)
    if (filterTo.value) params.set('to', filterTo.value)

    const { data, error } = await api.GET<{ items: AuditEntry[], totalCount: number }>(
      `${ApiRoutes.Admin.auditLog()}?${params.toString()}`
    )
    if (error) throw error
    entries.value = data!.items
    totalCount.value = data!.totalCount
  } catch (e) {
    toast.add({ title: (e as Error).message || 'Failed to load audit log', color: 'error' })
  } finally {
    loading.value = false
  }
}

const expandedRow = ref<string | null>(null)

function toggleExpand(id: string) {
  expandedRow.value = expandedRow.value === id ? null : id
}

function formatJson(json: string | null): string {
  if (!json) return '—'
  try {
    return JSON.stringify(JSON.parse(json), null, 2)
  } catch {
    return json
  }
}

function formatDate(ts: string): string {
  return new Date(ts).toLocaleString()
}

const totalPages = computed(() => Math.ceil(totalCount.value / pageSize))

watch([filterProjectId, filterActorId, filterEntityType, filterAction, filterFrom, filterTo], () => {
  page.value = 1
  loadEntries()
})

onMounted(() => loadEntries())
</script>

<template>
  <div class="p-6">
    <h1 class="text-2xl font-bold mb-4">
      Audit Log
    </h1>

    <!-- Filter Bar -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-3 mb-4">
      <UInput
        v-model="filterProjectId"
        placeholder="Project ID"
      />
      <UInput
        v-model="filterActorId"
        placeholder="Actor ID"
      />
      <USelect
        v-model="filterEntityType"
        :items="entityTypes"
        placeholder="Entity Type"
      />
      <USelect
        v-model="filterAction"
        :items="actions"
        placeholder="Action"
      />
      <UInput
        v-model="filterFrom"
        type="datetime-local"
        label="From"
      />
      <UInput
        v-model="filterTo"
        type="datetime-local"
        label="To"
      />
    </div>

    <!-- Results Table -->
    <UTable
      :rows="entries"
      :columns="columns"
      :loading="loading"
      :row-key="(row: AuditEntry) => row.id"
    >
      <template #timestamp-data="{ row }">
        <span class="text-sm">{{ formatDate(row.original.timestamp) }}</span>
      </template>
      <template #entityId-data="{ row }">
        <code class="text-xs">{{ row.original.entityId.substring(0, 8) }}...</code>
      </template>
      <template #id-data="{ row }">
        <UButton
          size="xs"
          color="neutral"
          variant="ghost"
          :label="expandedRow === row.original.id ? 'Collapse' : 'Details'"
          @click="toggleExpand(row.original.id)"
        />
      </template>
    </UTable>

    <!-- Expanded Row Detail -->
    <div
      v-if="expandedRow"
      class="mt-4 p-4 bg-gray-50 dark:bg-gray-800 rounded"
    >
      <template
        v-for="entry in entries.filter(e => e.id === expandedRow)"
        :key="entry.id"
      >
        <div class="mb-4">
          <h3 class="font-semibold mb-1">
            Old Value
          </h3>
          <pre class="text-xs overflow-x-auto p-2 bg-gray-100 dark:bg-gray-900 rounded">{{ formatJson(entry.oldValue) }}</pre>
        </div>
        <div>
          <h3 class="font-semibold mb-1">
            New Value
          </h3>
          <pre class="text-xs overflow-x-auto p-2 bg-gray-100 dark:bg-gray-900 rounded">{{ formatJson(entry.newValue) }}</pre>
        </div>
      </template>
    </div>

    <!-- Pagination -->
    <div class="flex items-center justify-between mt-4">
      <span class="text-sm text-gray-500">{{ totalCount }} total entries</span>
      <div class="flex gap-2">
        <UButton
          size="sm"
          color="neutral"
          :disabled="page === 1"
          @click="page--; loadEntries()"
        >
          Previous
        </UButton>
        <span class="text-sm self-center">Page {{ page }} of {{ totalPages }}</span>
        <UButton
          size="sm"
          color="neutral"
          :disabled="page >= totalPages"
          @click="page++; loadEntries()"
        >
          Next
        </UButton>
      </div>
    </div>
  </div>
</template>
