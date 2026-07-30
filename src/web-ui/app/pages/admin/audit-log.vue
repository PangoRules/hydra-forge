<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { TableColumn } from '@nuxt/ui'

definePageMeta({ middleware: ['auth'] })

const user = useAuthStore().user
if (!user?.isAdmin) {
  navigateTo('/projects')
}

const api = useApi()
const toast = useAppToast()

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

const entityTypes = [
  { label: 'All', value: '__all__' },
  { label: 'Card', value: 'Card' },
  { label: 'Column', value: 'Column' },
  { label: 'Project', value: 'Project' },
  { label: 'Comment', value: 'Comment' },
  { label: 'Attachment', value: 'Attachment' },
  { label: 'Spec', value: 'Spec' },
  { label: 'Plan', value: 'Plan' },
  { label: 'ChecklistItem', value: 'ChecklistItem' },
  { label: 'CardRelationship', value: 'CardRelationship' }
]
const actions = [
  { label: 'All', value: '__all__' },
  { label: 'Created', value: 'Created' },
  { label: 'Updated', value: 'Updated' },
  { label: 'Deleted', value: 'Deleted' },
  { label: 'Archived', value: 'Archived' },
  { label: 'Restored', value: 'Restored' },
  { label: 'Moved', value: 'Moved' },
  { label: 'Assigned', value: 'Assigned' },
  { label: 'Unassigned', value: 'Unassigned' },
  { label: 'Reordered', value: 'Reordered' },
  { label: 'Completed', value: 'Completed' },
  { label: 'Uncompleted', value: 'Uncompleted' },
  { label: 'ArchivedWithRelationships', value: 'ArchivedWithRelationships' }
]

const columns: TableColumn<AuditEntry>[] = [
  { accessorKey: 'timestamp', header: 'Timestamp' },
  { accessorKey: 'actorName', header: 'Actor' },
  { accessorKey: 'entityType', header: 'Entity Type' },
  { accessorKey: 'entityId', header: 'Entity ID' },
  { accessorKey: 'action', header: 'Action' },
  { accessorKey: 'scope', header: 'Scope' },
  { accessorKey: 'id', header: '' }
]

const expandedRow = ref<Record<string, boolean>>({})

function isValidGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
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

// Stale request guard
let requestSeq = 0

function debounce(fn: () => void, delay: number): () => void {
  let timer: ReturnType<typeof setTimeout> | null = null
  return () => {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => fn(), delay)
  }
}

const debouncedLoad = debounce(() => {
  page.value = 1
  loadEntries()
}, 300)

async function loadEntries() {
  const seq = ++requestSeq
  loading.value = true
  try {
    const params = new URLSearchParams()
    params.set('skip', String((page.value - 1) * pageSize))
    params.set('take', String(pageSize))
    if (filterProjectId.value && isValidGuid(filterProjectId.value)) {
      params.set('projectId', filterProjectId.value)
    }
    if (filterActorId.value && isValidGuid(filterActorId.value)) {
      params.set('actorId', filterActorId.value)
    }
    if (filterEntityType.value && filterEntityType.value !== '__all__') params.set('entityType', filterEntityType.value)
    if (filterAction.value && filterAction.value !== '__all__') params.set('action', filterAction.value)
    if (filterFrom.value) params.set('from', new Date(filterFrom.value).toISOString())
    if (filterTo.value) params.set('to', new Date(filterTo.value).toISOString())

    const { data, error } = await api.GET<{ items: AuditEntry[], totalCount: number }>(
      `${ApiRoutes.Admin.auditLog()}?${params.toString()}`
    )
    if (error) throw error
    if (seq !== requestSeq) return // stale response, ignore
    entries.value = data!.items
    totalCount.value = data!.totalCount
  } catch (e) {
    if (seq !== requestSeq) return
    toast.showApiError(e as Error)
  } finally {
    if (seq === requestSeq) {
      loading.value = false
    }
  }
}

function resetFilters() {
  filterProjectId.value = ''
  filterActorId.value = ''
  filterEntityType.value = ''
  filterAction.value = ''
  filterFrom.value = ''
  filterTo.value = ''
}

watch([filterProjectId, filterActorId, filterEntityType, filterAction, filterFrom, filterTo], () => {
  debouncedLoad()
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
      <UFormField label="From">
        <UInput
          v-model="filterFrom"
          type="datetime-local"
        />
      </UFormField>
      <UFormField label="To">
        <UInput
          v-model="filterTo"
          type="datetime-local"
        />
      </UFormField>
      <UButton
        size="sm"
        color="neutral"
        variant="ghost"
        label="Reset filters"
        @click="resetFilters"
      />
    </div>

    <!-- Results Table -->
    <UTable
      v-model:expanded="expandedRow"
      :data="entries"
      :columns="columns"
      :loading="loading"
      :get-row-id="(row: AuditEntry) => row.id"
    >
      <template #timestamp-cell="{ row }">
        <span class="text-sm">{{ formatDate(row.original.timestamp) }}</span>
      </template>
      <template #entityId-cell="{ row }">
        <code class="text-xs">{{ row.original.entityId.substring(0, 8) }}...</code>
      </template>
      <template #id-cell="{ row }">
        <UButton
          size="xs"
          color="neutral"
          variant="ghost"
          :label="expandedRow[row.original.id] ? 'Collapse' : 'Details'"
          @click="row.toggleExpanded()"
        />
      </template>
      <template #expanded="{ row }">
        <div class="grid grid-cols-2 gap-4 p-4">
          <div>
            <h3 class="font-semibold mb-1">
              Old Value
            </h3>
            <pre class="text-xs overflow-x-auto p-2 bg-gray-100 dark:bg-gray-900 rounded">{{ formatJson(row.original.oldValue) }}</pre>
          </div>
          <div>
            <h3 class="font-semibold mb-1">
              New Value
            </h3>
            <pre class="text-xs overflow-x-auto p-2 bg-gray-100 dark:bg-gray-900 rounded">{{ formatJson(row.original.newValue) }}</pre>
          </div>
        </div>
      </template>
    </UTable>

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
