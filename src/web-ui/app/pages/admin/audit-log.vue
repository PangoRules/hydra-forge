<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import { formatDateTime } from '~/lib/date'
import type { TableColumn } from '@nuxt/ui'
import DataTable from '~/components/shared/DataTable.vue'
import CollapsibleFilterPanel from '~/components/shared/CollapsibleFilterPanel.vue'

definePageMeta({ middleware: ['auth'] })

const user = useAuthStore().user
if (!user?.isAdmin) {
  navigateTo('/projects')
}

const api = useApi()
const toast = useAppToast()
const route = useRoute()
const router = useRouter()

interface AuditEntry {
  id: string
  projectId: string | null
  projectName: string | null
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

interface ProjectSearchResult {
  id: string
  name: string
}

interface UserSearchResult {
  id: string
  username: string
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
const pageSize = ref(50)

const activeFilterCount = computed(() =>
  [
    !!filterProjectId.value,
    !!filterActorId.value,
    !!(filterEntityType.value && filterEntityType.value !== '__all__'),
    !!(filterAction.value && filterAction.value !== '__all__'),
    !!filterFrom.value,
    !!filterTo.value
  ].filter(Boolean).length
)

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
  { label: 'CardRelationship', value: 'CardRelationship' },
  { label: 'User', value: 'User' },
  { label: 'ProjectMember', value: 'ProjectMember' }
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
  { label: 'ArchivedWithRelationships', value: 'ArchivedWithRelationships' },
  { label: 'StatusChanged', value: 'StatusChanged' },
  { label: 'Disabled', value: 'Disabled' },
  { label: 'Enabled', value: 'Enabled' },
  { label: 'RoleChanged', value: 'RoleChanged' },
  { label: 'PasswordReset', value: 'PasswordReset' }
]

const columns: TableColumn<AuditEntry>[] = [
  { accessorKey: 'timestamp', header: 'Timestamp' },
  { accessorKey: 'actorName', header: 'Actor' },
  { accessorKey: 'projectName', header: 'Project' },
  { accessorKey: 'entityType', header: 'Entity Type' },
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

// filterFrom/filterTo are plain `yyyy-mm-dd` (native <input type="date">, no
// time-of-day picker). Appending an explicit local time (no timezone suffix)
// makes the Date parse as local time, not UTC midnight — so "From: today" /
// "To: today" covers the whole day in the viewer's own timezone.
function startOfDayIso(dateStr: string): string {
  return new Date(`${dateStr}T00:00:00`).toISOString()
}

function endOfDayIso(dateStr: string): string {
  return new Date(`${dateStr}T23:59:59.999`).toISOString()
}

type BadgeColor = 'neutral' | 'primary' | 'info' | 'success' | 'warning' | 'error'

const actionColors: Record<string, BadgeColor> = {
  Created: 'success',
  Restored: 'success',
  Completed: 'success',
  Enabled: 'success',
  Updated: 'info',
  Assigned: 'info',
  Moved: 'info',
  StatusChanged: 'info',
  RoleChanged: 'info',
  PasswordReset: 'info',
  Reordered: 'neutral',
  Unassigned: 'neutral',
  Uncompleted: 'neutral',
  Deleted: 'error',
  Disabled: 'error',
  Archived: 'warning',
  ArchivedWithRelationships: 'warning'
}

function actionColor(action: string): BadgeColor {
  return actionColors[action] ?? 'neutral'
}

const scopeColors: Record<string, BadgeColor> = {
  Project: 'primary',
  System: 'warning',
  Personal: 'neutral'
}

function scopeColor(scope: string): BadgeColor {
  return scopeColors[scope] ?? 'neutral'
}

async function copyId(value: string) {
  try {
    await navigator.clipboard.writeText(value)
    toast.success('Copied to clipboard', 1500)
  } catch {
    toast.error('Failed to copy')
  }
}

// Line-level diff between old/new JSON for the expanded row detail view.
interface DiffLine {
  type: 'same' | 'add' | 'remove'
  text: string
}

function diffLines(a: string[], b: string[]): DiffLine[] {
  const n = a.length
  const m = b.length
  // DP table is O(n*m) — guard against pathologically large payloads freezing the tab.
  if (n * m > 200_000) {
    return [
      ...a.map(text => ({ type: 'remove' as const, text })),
      ...b.map(text => ({ type: 'add' as const, text }))
    ]
  }
  const dp: number[][] = Array.from({ length: n + 1 }, () => new Array<number>(m + 1).fill(0))
  for (let i = n - 1; i >= 0; i--) {
    for (let j = m - 1; j >= 0; j--) {
      dp[i]![j] = a[i] === b[j] ? dp[i + 1]![j + 1]! + 1 : Math.max(dp[i + 1]![j]!, dp[i]![j + 1]!)
    }
  }
  const result: DiffLine[] = []
  let i = 0
  let j = 0
  while (i < n && j < m) {
    if (a[i] === b[j]) {
      result.push({ type: 'same', text: a[i]! })
      i++
      j++
    } else if (dp[i + 1]![j]! >= dp[i]![j + 1]!) {
      result.push({ type: 'remove', text: a[i]! })
      i++
    } else {
      result.push({ type: 'add', text: b[j]! })
      j++
    }
  }
  while (i < n) {
    result.push({ type: 'remove', text: a[i]! })
    i++
  }
  while (j < m) {
    result.push({ type: 'add', text: b[j]! })
    j++
  }
  return result
}

function diffFor(entry: AuditEntry): DiffLine[] {
  if (!entry.oldValue && !entry.newValue) return []
  if (!entry.oldValue) return formatJson(entry.newValue).split('\n').map(text => ({ type: 'add' as const, text }))
  if (!entry.newValue) return formatJson(entry.oldValue).split('\n').map(text => ({ type: 'remove' as const, text }))
  return diffLines(formatJson(entry.oldValue).split('\n'), formatJson(entry.newValue).split('\n'))
}

function diffLineClass(type: DiffLine['type']): string {
  if (type === 'add') return 'bg-success/10 text-success-700 dark:text-success-400'
  if (type === 'remove') return 'bg-error/10 text-error-700 dark:text-error-400'
  return ''
}

function diffLinePrefix(type: DiffLine['type']): string {
  if (type === 'add') return '+ '
  if (type === 'remove') return '- '
  return '  '
}

// Project / actor filters: type a raw GUID (kept for parity with the old
// behavior) or search by name/username and pick from the dropdown.
const projectQuery = ref('')
const projectResults = ref<ProjectSearchResult[]>([])
const projectSearchLoading = ref(false)
const selectedProject = ref<ProjectSearchResult | null>(null)
let projectSearchTimer: ReturnType<typeof setTimeout> | null = null

const actorQuery = ref('')
const actorResults = ref<UserSearchResult[]>([])
const actorSearchLoading = ref(false)
const selectedActor = ref<UserSearchResult | null>(null)
let actorSearchTimer: ReturnType<typeof setTimeout> | null = null

async function searchProjects(term: string) {
  if (!term.trim()) {
    projectResults.value = []
    return
  }
  projectSearchLoading.value = true
  try {
    const { data } = await api.GET<{ items: ProjectSearchResult[], totalCount: number }>(
      ApiRoutes.Admin.projectsList(0, 10, term)
    )
    projectResults.value = data?.items ?? []
  } catch {
    projectResults.value = []
  } finally {
    projectSearchLoading.value = false
  }
}

function onProjectInput(value: string) {
  projectQuery.value = value
  selectedProject.value = null
  if (isValidGuid(value)) {
    filterProjectId.value = value
    projectResults.value = []
    return
  }
  filterProjectId.value = ''
  if (projectSearchTimer) clearTimeout(projectSearchTimer)
  projectSearchTimer = setTimeout(() => searchProjects(value), 300)
}

function selectProject(p: ProjectSearchResult) {
  selectedProject.value = p
  filterProjectId.value = p.id
  projectQuery.value = ''
  projectResults.value = []
}

function clearProjectFilter() {
  selectedProject.value = null
  filterProjectId.value = ''
  projectQuery.value = ''
  projectResults.value = []
}

async function searchActors(term: string) {
  if (!term.trim()) {
    actorResults.value = []
    return
  }
  actorSearchLoading.value = true
  try {
    const { data } = await api.GET<UserSearchResult[]>(ApiRoutes.Users.search(term, 10))
    actorResults.value = data ?? []
  } catch {
    actorResults.value = []
  } finally {
    actorSearchLoading.value = false
  }
}

function onActorInput(value: string) {
  actorQuery.value = value
  selectedActor.value = null
  if (isValidGuid(value)) {
    filterActorId.value = value
    actorResults.value = []
    return
  }
  filterActorId.value = ''
  if (actorSearchTimer) clearTimeout(actorSearchTimer)
  actorSearchTimer = setTimeout(() => searchActors(value), 300)
}

function selectActor(u: UserSearchResult) {
  selectedActor.value = u
  filterActorId.value = u.id
  actorQuery.value = ''
  actorResults.value = []
}

function clearActorFilter() {
  selectedActor.value = null
  filterActorId.value = ''
  actorQuery.value = ''
  actorResults.value = []
}

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

function syncUrlQuery() {
  const query: Record<string, string> = {}
  if (filterProjectId.value) query.projectId = filterProjectId.value
  if (filterActorId.value) query.actorId = filterActorId.value
  if (filterEntityType.value && filterEntityType.value !== '__all__') query.entityType = filterEntityType.value
  if (filterAction.value && filterAction.value !== '__all__') query.action = filterAction.value
  if (filterFrom.value) query.from = filterFrom.value
  if (filterTo.value) query.to = filterTo.value
  if (page.value > 1) query.page = String(page.value)
  if (pageSize.value !== 50) query.pageSize = String(pageSize.value)
  router.replace({ query })
}

async function loadEntries() {
  const seq = ++requestSeq
  loading.value = true
  try {
    const params = new URLSearchParams()
    params.set('skip', String((page.value - 1) * pageSize.value))
    params.set('take', String(pageSize.value))
    if (filterProjectId.value && isValidGuid(filterProjectId.value)) {
      params.set('projectId', filterProjectId.value)
    }
    if (filterActorId.value && isValidGuid(filterActorId.value)) {
      params.set('actorId', filterActorId.value)
    }
    if (filterEntityType.value && filterEntityType.value !== '__all__') params.set('entityType', filterEntityType.value)
    if (filterAction.value && filterAction.value !== '__all__') params.set('action', filterAction.value)
    if (filterFrom.value) params.set('from', startOfDayIso(filterFrom.value))
    if (filterTo.value) params.set('to', endOfDayIso(filterTo.value))

    const { data, error } = await api.GET<{ items: AuditEntry[], totalCount: number }>(
      `${ApiRoutes.Admin.auditLog()}?${params.toString()}`
    )
    if (error) throw error
    if (seq !== requestSeq) return // stale response, ignore
    entries.value = data!.items
    totalCount.value = data!.totalCount
    syncUrlQuery()
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
  clearProjectFilter()
  clearActorFilter()
}

watch([filterProjectId, filterActorId, filterEntityType, filterAction, filterFrom, filterTo], () => {
  debouncedLoad()
})

function onPageChange(newPage: number) {
  page.value = newPage
  loadEntries()
}

function onPageSizeChange(newPageSize: number) {
  pageSize.value = newPageSize
  page.value = 1
  loadEntries()
}

async function resolveProjectLabel(projectId: string) {
  try {
    const { data } = await api.GET<{ name: string }>(ApiRoutes.Admin.projectGet(projectId))
    if (data) selectedProject.value = { id: projectId, name: data.name }
  } catch {
    // Filter still applies via the raw GUID even if the name lookup fails.
  }
}

async function resolveActorLabel(actorId: string) {
  try {
    const { data } = await api.GET<UserSearchResult>(ApiRoutes.Admin.userGet(actorId))
    if (data) selectedActor.value = { id: actorId, username: data.username }
  } catch {
    // Filter still applies via the raw GUID even if the name lookup fails.
  }
}

function initFromQuery() {
  const q = route.query
  if (typeof q.projectId === 'string' && isValidGuid(q.projectId)) {
    filterProjectId.value = q.projectId
    resolveProjectLabel(q.projectId)
  }
  if (typeof q.actorId === 'string' && isValidGuid(q.actorId)) {
    filterActorId.value = q.actorId
    resolveActorLabel(q.actorId)
  }
  if (typeof q.entityType === 'string') filterEntityType.value = q.entityType
  if (typeof q.action === 'string') filterAction.value = q.action
  if (typeof q.from === 'string') filterFrom.value = q.from
  if (typeof q.to === 'string') filterTo.value = q.to
  if (typeof q.page === 'string') page.value = Number(q.page) || 1
  if (typeof q.pageSize === 'string') pageSize.value = Number(q.pageSize) || 50
}

initFromQuery()

onMounted(() => loadEntries())
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <!-- Header + filter bar (fixed, does not scroll) -->
    <div class="shrink-0 border-b border-default px-6 py-4 space-y-3">
      <h1 class="text-2xl font-bold">
        Audit Log
      </h1>

      <CollapsibleFilterPanel
        :active-count="activeFilterCount"
        @reset="resetFilters"
      >
        <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
          <div class="relative">
            <div
              v-if="selectedProject"
              class="flex items-center gap-1 h-8 rounded border border-muted bg-primary/10 px-2 text-xs"
            >
              <span class="truncate font-medium">{{ selectedProject.name }}</span>
              <UButton
                icon="i-lucide-x"
                variant="ghost"
                size="xs"
                color="neutral"
                class="ml-auto size-4"
                aria-label="Clear project filter"
                @click="clearProjectFilter"
              />
            </div>
            <UInput
              v-else
              :model-value="projectQuery"
              placeholder="Project ID or name"
              :loading="projectSearchLoading"
              @update:model-value="onProjectInput"
            />
            <div
              v-if="!selectedProject && projectResults.length > 0"
              class="absolute z-10 mt-1 w-full max-h-40 overflow-y-auto rounded border border-muted bg-default shadow-lg"
            >
              <button
                v-for="p in projectResults"
                :key="p.id"
                type="button"
                class="flex w-full items-center px-2 py-1 text-left text-xs hover:bg-muted/50"
                @click="selectProject(p)"
              >
                {{ p.name }}
              </button>
            </div>
          </div>

          <div class="relative">
            <div
              v-if="selectedActor"
              class="flex items-center gap-1 h-8 rounded border border-muted bg-primary/10 px-2 text-xs"
            >
              <span class="truncate font-medium">{{ selectedActor.username }}</span>
              <UButton
                icon="i-lucide-x"
                variant="ghost"
                size="xs"
                color="neutral"
                class="ml-auto size-4"
                aria-label="Clear actor filter"
                @click="clearActorFilter"
              />
            </div>
            <UInput
              v-else
              :model-value="actorQuery"
              placeholder="Actor ID or username"
              :loading="actorSearchLoading"
              @update:model-value="onActorInput"
            />
            <div
              v-if="!selectedActor && actorResults.length > 0"
              class="absolute z-10 mt-1 w-full max-h-40 overflow-y-auto rounded border border-muted bg-default shadow-lg"
            >
              <button
                v-for="u in actorResults"
                :key="u.id"
                type="button"
                class="flex w-full items-center px-2 py-1 text-left text-xs hover:bg-muted/50"
                @click="selectActor(u)"
              >
                {{ u.username }}
              </button>
            </div>
          </div>

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
        </div>

        <div class="flex flex-wrap gap-3">
          <UFormField label="From">
            <UInput
              v-model="filterFrom"
              type="date"
            />
          </UFormField>
          <UFormField label="To">
            <UInput
              v-model="filterTo"
              type="date"
            />
          </UFormField>
        </div>
      </CollapsibleFilterPanel>
    </div>

    <!-- Results Table (fills remaining height, scrolls internally) -->
    <div class="flex-1 min-h-0 px-6 pb-6 pt-4">
      <DataTable
        v-model:expanded="expandedRow"
        :data="entries"
        :columns="columns"
        :loading="loading"
        :page="page"
        :page-size="pageSize"
        :total-count="totalCount"
        :page-size-options="[25, 50, 100]"
        :row-key="(item: AuditEntry) => item.id"
        fill-height
        @update:page="onPageChange"
        @update:page-size="onPageSizeChange"
      >
        <template #timestamp-cell="{ row }">
          <span class="text-sm">{{ formatDateTime(row.original.timestamp) }}</span>
        </template>
        <template #projectName-cell="{ row }">
          <span class="text-sm">{{ row.original.projectName ?? '—' }}</span>
        </template>
        <template #action-cell="{ row }">
          <UBadge
            variant="subtle"
            size="sm"
            :color="actionColor(row.original.action)"
          >
            {{ row.original.action }}
          </UBadge>
        </template>
        <template #scope-cell="{ row }">
          <UBadge
            variant="subtle"
            size="sm"
            :color="scopeColor(row.original.scope)"
          >
            {{ row.original.scope }}
          </UBadge>
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
          <div class="p-4 space-y-3">
            <div class="flex items-center gap-1 text-xs text-muted">
              <span>Entity ID:</span>
              <code>{{ row.original.entityId }}</code>
              <UButton
                icon="i-lucide-copy"
                size="xs"
                variant="ghost"
                color="neutral"
                class="size-4"
                aria-label="Copy entity ID"
                @click="copyId(row.original.entityId)"
              />
            </div>
            <div
              v-if="diffFor(row.original).length === 0"
              class="text-xs text-muted"
            >
              No values recorded.
            </div>
            <div
              v-else
              class="font-mono text-xs overflow-x-auto rounded border border-muted"
            >
              <div
                v-for="(line, idx) in diffFor(row.original)"
                :key="idx"
                class="whitespace-pre px-2"
                :class="diffLineClass(line.type)"
              >
                {{ diffLinePrefix(line.type) }}{{ line.text }}
              </div>
            </div>
          </div>
        </template>
      </DataTable>
    </div>
  </div>
</template>
