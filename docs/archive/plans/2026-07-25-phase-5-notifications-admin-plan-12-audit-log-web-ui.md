# Plan 12: Audit Log Web UI Page

**Branch:** `task/audit-log-web-ui`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 12

## Task

Create `/admin/audit-log` page with filter bar (project, actor, entity type, action, date range), results table with pagination, and row expansion for OldValue/NewValue JSON.

## Files to create

- `src/web-ui/app/pages/admin/audit-log.vue`

## Files to modify

None — routes already defined in Task 9.

## Implementation steps

### Step 1: Create audit log page

Create `src/web-ui/app/pages/admin/audit-log.vue`:

```vue
<script setup lang="ts">
definePageMeta({ middleware: ['auth'] })

const { user } = useAuth()
if (!user.value?.isAdmin) {
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
const page = ref(0)
const pageSize = 50

const entityTypes = ['Card', 'Column', 'Project', 'Comment', 'Attachment', 'Spec', 'Plan', 'Checklist', 'CardRelationship']
const actions = ['Created', 'Updated', 'Deleted', 'Archived', 'Restored', 'Moved', 'Assigned', 'Unassigned']

async function loadEntries() {
  loading.value = true
  try {
    const params = new URLSearchParams()
    params.set('skip', String(page.value * pageSize))
    params.set('take', String(pageSize))
    if (filterProjectId.value) params.set('projectId', filterProjectId.value)
    if (filterActorId.value) params.set('actorId', filterActorId.value)
    if (filterEntityType.value) params.set('entityType', filterEntityType.value)
    if (filterAction.value) params.set('action', filterAction.value)
    if (filterFrom.value) params.set('from', filterFrom.value)
    if (filterTo.value) params.set('to', filterTo.value)

    const data = await api.GET<{ items: AuditEntry[], totalCount: number }>(
      `${ApiRoutes.Admin.auditLog()}?${params.toString()}`
    )
    entries.value = data.items
    totalCount.value = data.totalCount
  } catch (e: any) {
    toast.add({ title: e.message || 'Failed to load audit log', color: 'error' })
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
  page.value = 0
  loadEntries()
})

onMounted(() => loadEntries())
</script>

<template>
  <div class="p-6">
    <h1 class="text-2xl font-bold mb-4">Audit Log</h1>

    <!-- Filter Bar -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-3 mb-4">
      <UInput v-model="filterProjectId" placeholder="Project ID" />
      <UInput v-model="filterActorId" placeholder="Actor ID" />
      <USelect v-model="filterEntityType" :items="entityTypes" placeholder="Entity Type" />
      <USelect v-model="filterAction" :items="actions" placeholder="Action" />
      <UInput v-model="filterFrom" type="datetime-local" label="From" />
      <UInput v-model="filterTo" type="datetime-local" label="To" />
    </div>

    <!-- Results Table -->
    <UTable :rows="entries" :loading="loading">
      <template #timestamp-data="{ row }">
        <span class="text-sm">{{ formatDate(row.timestamp) }}</span>
      </template>
      <template #entityId-data="{ row }">
        <code class="text-xs">{{ row.entityId.substring(0, 8) }}...</code>
      </template>
      <template #expand-data="{ row }">
        <UButton
          size="xs"
          color="neutral"
          variant="ghost"
          :label="expandedRow === row.id ? 'Collapse' : 'Details'"
          @click="toggleExpand(row.id)"
        />
      </template>
    </UTable>

    <!-- Expanded Row Detail -->
    <div v-if="expandedRow" class="mt-4 p-4 bg-gray-50 dark:bg-gray-800 rounded">
      <template v-for="entry in entries.filter(e => e.id === expandedRow)" :key="entry.id">
        <div class="mb-4">
          <h3 class="font-semibold mb-1">Old Value</h3>
          <pre class="text-xs overflow-x-auto p-2 bg-gray-100 dark:bg-gray-900 rounded">{{ formatJson(entry.oldValue) }}</pre>
        </div>
        <div>
          <h3 class="font-semibold mb-1">New Value</h3>
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
          :disabled="page === 0"
          @click="page--; loadEntries()"
        >
          Previous
        </UButton>
        <span class="text-sm self-center">Page {{ page + 1 }} of {{ totalPages }}</span>
        <UButton
          size="sm"
          color="neutral"
          :disabled="(page + 1) >= totalPages"
          @click="page++; loadEntries()"
        >
          Next
        </UButton>
      </div>
    </div>
  </div>
</template>
```

### Step 2: Verify

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
```

## Verification

- `pnpm typecheck` — no errors
- `pnpm lint` — no errors
- `pnpm build` — no errors
- Manual: login as admin, navigate to `/admin/audit-log`, see entries table, filter by entity type, expand row to see JSON details

## Dependencies

- Task 11 (Audit log reader + API endpoint must exist)
- Task 9 (Admin routes defined, admin nav visible)