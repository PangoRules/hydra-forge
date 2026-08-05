<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import { formatDateTime } from '~/lib/date'
import { formatCost } from '~/lib/money'
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

type Tab = 'tokens' | 'images'
const activeTab = ref<Tab>('tokens')

// Filters
const filterUserId = ref('')
const filterFeature = ref<string[]>([])
const filterModel = ref('')
const filterFrom = ref('')
const filterTo = ref('')
const page = ref(1)
const pageSize = ref(50)

const activeFilterCount = computed(() =>
  [
    !!filterUserId.value,
    filterFeature.value.length > 0,
    !!filterModel.value,
    !!filterFrom.value,
    !!filterTo.value
  ].filter(Boolean).length
)

const featureOptions = [
  { label: 'PersonalChat', value: 'PersonalChat' },
  { label: 'ProjectChat', value: 'ProjectChat' },
  { label: 'DeepResearch', value: 'DeepResearch' },
  { label: 'AgentPipeline', value: 'AgentPipeline' },
  { label: 'MemoryExtraction', value: 'MemoryExtraction' },
  { label: 'NotesClassification', value: 'NotesClassification' },
  { label: 'DocumentEditing', value: 'DocumentEditing' },
  { label: 'CardReview', value: 'CardReview' },
  { label: 'ImageChat', value: 'ImageChat' },
  { label: 'ImageDocument', value: 'ImageDocument' },
  { label: 'ImageGalleryEditor', value: 'ImageGalleryEditor' }
]

interface UserSearchResult {
  id: string
  username: string
}

const userQuery = ref('')
const userResults = ref<UserSearchResult[]>([])
const userSearchLoading = ref(false)
const selectedUser = ref<UserSearchResult | null>(null)
let userSearchTimer: ReturnType<typeof setTimeout> | null = null

interface TokenUsageRecord {
  id: string
  userId: string
  userName?: string
  projectId: string | null
  feature: string
  modelName: string
  inputTokens: number
  outputTokens: number
  cachedTokens: number
  cost: number
  createdAt: string
}

interface ImageUsageRecord {
  id: string
  userId: string
  userName?: string
  projectId: string | null
  feature: string
  modelName: string
  imageCount: number
  resolution: string
  cost: number
  createdAt: string
}

const tokenRecords = ref<TokenUsageRecord[]>([])
const imageRecords = ref<ImageUsageRecord[]>([])
const totalCount = ref(0)
const totalCost = ref(0)
const loading = ref(false)

// Input/Output/Cached tokens move into the expand-row detail panel (same
// pattern as audit-log.vue's diff view) instead of their own columns —
// three numeric columns were the main reason this table forced horizontal
// scroll before reaching a usable vertical scrollbar.
const tokenColumns: TableColumn<TokenUsageRecord>[] = [
  { accessorKey: 'createdAt', header: 'Timestamp' },
  { accessorKey: 'userName', header: 'User' },
  { accessorKey: 'feature', header: 'Feature' },
  { accessorKey: 'modelName', header: 'Model' },
  { accessorKey: 'cost', header: 'Cost' },
  { accessorKey: 'id', header: '' }
]
const expandedTokenRow = ref<Record<string, boolean>>({})

const imageColumns: TableColumn<ImageUsageRecord>[] = [
  { accessorKey: 'createdAt', header: 'Timestamp' },
  { accessorKey: 'userName', header: 'User' },
  { accessorKey: 'feature', header: 'Feature' },
  { accessorKey: 'modelName', header: 'Model' },
  { accessorKey: 'imageCount', header: 'Image Count' },
  { accessorKey: 'resolution', header: 'Resolution' },
  { accessorKey: 'cost', header: 'Cost' }
]

function isValidGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
}

function startOfDayIso(dateStr: string): string {
  return new Date(`${dateStr}T00:00:00`).toISOString()
}

function endOfDayIso(dateStr: string): string {
  return new Date(`${dateStr}T23:59:59.999`).toISOString()
}

async function searchUsers(term: string) {
  if (!term.trim()) {
    userResults.value = []
    return
  }
  userSearchLoading.value = true
  try {
    const { data } = await api.GET<UserSearchResult[]>(ApiRoutes.Users.search(term, 10))
    userResults.value = data ?? []
  } catch {
    userResults.value = []
  } finally {
    userSearchLoading.value = false
  }
}

function onUserInput(value: string) {
  userQuery.value = value
  selectedUser.value = null
  if (isValidGuid(value)) {
    filterUserId.value = value
    userResults.value = []
    return
  }
  filterUserId.value = ''
  if (userSearchTimer) clearTimeout(userSearchTimer)
  userSearchTimer = setTimeout(() => searchUsers(value), 300)
}

function selectUser(u: UserSearchResult) {
  selectedUser.value = u
  filterUserId.value = u.id
  userQuery.value = ''
  userResults.value = []
}

function clearUserFilter() {
  selectedUser.value = null
  filterUserId.value = ''
  userQuery.value = ''
  userResults.value = []
}

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
  loadRecords()
}, 300)

function buildParams(): URLSearchParams {
  const params = new URLSearchParams()
  params.set('skip', String((page.value - 1) * pageSize.value))
  params.set('take', String(pageSize.value))
  if (filterUserId.value && isValidGuid(filterUserId.value)) {
    params.set('userId', filterUserId.value)
  }
  if (filterFeature.value.length > 0) {
    filterFeature.value.forEach(f => params.append('feature', f))
  }
  if (filterModel.value) {
    params.set('modelId', filterModel.value)
  }
  if (filterFrom.value) {
    params.set('from', startOfDayIso(filterFrom.value))
  }
  if (filterTo.value) {
    params.set('to', endOfDayIso(filterTo.value))
  }
  return params
}

async function loadRecords() {
  const seq = ++requestSeq
  loading.value = true
  try {
    if (activeTab.value === 'tokens') {
      const { data, error } = await api.GET<{ items: TokenUsageRecord[], totalCount: number, totalCost: number }>(
        ApiRoutes.Admin.usage.tokens(buildParams().toString())
      )
      if (error) throw error
      if (seq !== requestSeq) return
      tokenRecords.value = data!.items
      totalCount.value = data!.totalCount
      totalCost.value = data!.totalCost
    } else {
      const { data, error } = await api.GET<{ items: ImageUsageRecord[], totalCount: number, totalCost: number }>(
        ApiRoutes.Admin.usage.images(buildParams().toString())
      )
      if (error) throw error
      if (seq !== requestSeq) return
      imageRecords.value = data!.items
      totalCount.value = data!.totalCount
      totalCost.value = data!.totalCost
    }
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
  filterUserId.value = ''
  filterFeature.value = []
  filterModel.value = ''
  filterFrom.value = ''
  filterTo.value = ''
  clearUserFilter()
}

watch([activeTab], () => {
  page.value = 1
  loadRecords()
})

watch([filterUserId, filterFeature, filterModel, filterFrom, filterTo], () => {
  debouncedLoad()
})

function onPageChange(newPage: number) {
  page.value = newPage
  loadRecords()
}

function onPageSizeChange(newPageSize: number) {
  pageSize.value = newPageSize
  page.value = 1
  loadRecords()
}

function switchTab(tab: Tab) {
  activeTab.value = tab
}

onMounted(() => loadRecords())
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <!-- Header + filter bar -->
    <div class="shrink-0 border-b border-default px-6 py-4 space-y-3">
      <h1 class="text-2xl font-bold">
        Usage
      </h1>

      <!-- Tabs -->
      <div class="flex gap-1">
        <UButton
          :color="activeTab === 'tokens' ? 'primary' : 'neutral'"
          :variant="activeTab === 'tokens' ? 'solid' : 'ghost'"
          label="Token Usage"
          @click="switchTab('tokens')"
        />
        <UButton
          :color="activeTab === 'images' ? 'primary' : 'neutral'"
          :variant="activeTab === 'images' ? 'solid' : 'ghost'"
          label="Image Usage"
          @click="switchTab('images')"
        />
      </div>

      <CollapsibleFilterPanel
        :active-count="activeFilterCount"
        @reset="resetFilters"
      >
        <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
          <div class="relative">
            <div
              v-if="selectedUser"
              class="flex items-center gap-1 h-8 rounded border border-muted bg-primary/10 px-2 text-xs"
            >
              <span class="truncate font-medium">{{ selectedUser.username }}</span>
              <UButton
                icon="i-lucide-x"
                variant="ghost"
                size="xs"
                color="neutral"
                class="ml-auto size-4"
                aria-label="Clear user filter"
                @click="clearUserFilter"
              />
            </div>
            <UInput
              v-else
              :model-value="userQuery"
              placeholder="User ID or name"
              :loading="userSearchLoading"
              @update:model-value="onUserInput"
            />
            <div
              v-if="!selectedUser && userResults.length > 0"
              class="absolute z-10 mt-1 w-full max-h-40 overflow-y-auto rounded border border-muted bg-default shadow-lg"
            >
              <button
                v-for="u in userResults"
                :key="u.id"
                type="button"
                class="flex w-full items-center px-2 py-1 text-left text-xs hover:bg-muted/50"
                @click="selectUser(u)"
              >
                {{ u.username }}
              </button>
            </div>
          </div>

          <USelect
            v-model="filterFeature"
            :items="featureOptions"
            multiple
            placeholder="Feature"
          />
          <UInput
            v-model="filterModel"
            placeholder="Model"
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

    <!-- Results Table -->
    <div class="flex-1 min-h-0 px-6 pb-6 pt-4">
      <!-- Token Table -->
      <DataTable
        v-if="activeTab === 'tokens'"
        v-model:expanded="expandedTokenRow"
        :data="tokenRecords"
        :columns="tokenColumns"
        :loading="loading"
        :page="page"
        :page-size="pageSize"
        :total-count="totalCount"
        :page-size-options="[25, 50, 100]"
        :row-key="(item: TokenUsageRecord) => item.id"
        fill-height
        @update:page="onPageChange"
        @update:page-size="onPageSizeChange"
      >
        <template #createdAt-cell="{ row }">
          <span class="text-sm">{{ formatDateTime(row.original.createdAt) }}</span>
        </template>
        <template #userName-cell="{ row }">
          <span class="text-sm">{{ row.original.userName ?? row.original.userId }}</span>
        </template>
        <template #feature-cell="{ row }">
          <UBadge
            variant="subtle"
            size="sm"
            color="primary"
          >
            {{ row.original.feature }}
          </UBadge>
        </template>
        <template #cost-cell="{ row }">
          <span class="text-sm tabular-nums">{{ formatCost(row.original.cost) }}</span>
        </template>
        <template #id-cell="{ row }">
          <UButton
            size="xs"
            color="neutral"
            variant="ghost"
            :label="expandedTokenRow[row.original.id] ? 'Collapse' : 'Details'"
            @click="row.toggleExpanded()"
          />
        </template>
        <template #expanded="{ row }">
          <div class="flex flex-wrap gap-x-8 gap-y-2 p-4 text-sm">
            <div>
              <span class="text-muted">Input Tokens: </span>
              <span class="tabular-nums">{{ row.original.inputTokens.toLocaleString() }}</span>
            </div>
            <div>
              <span class="text-muted">Output Tokens: </span>
              <span class="tabular-nums">{{ row.original.outputTokens.toLocaleString() }}</span>
            </div>
            <div>
              <span class="text-muted">Cached Tokens: </span>
              <span class="tabular-nums">{{ row.original.cachedTokens.toLocaleString() }}</span>
            </div>
          </div>
        </template>
        <template #footer>
          <div class="flex justify-end px-4 py-2 text-sm font-semibold border-t border-muted">
            <span>Total Cost: {{ formatCost(totalCost) }}</span>
          </div>
        </template>
      </DataTable>

      <!-- Image Table -->
      <DataTable
        v-else
        :data="imageRecords"
        :columns="imageColumns"
        :loading="loading"
        :page="page"
        :page-size="pageSize"
        :total-count="totalCount"
        :page-size-options="[25, 50, 100]"
        :row-key="(item: ImageUsageRecord) => item.id"
        fill-height
        @update:page="onPageChange"
        @update:page-size="onPageSizeChange"
      >
        <template #createdAt-cell="{ row }">
          <span class="text-sm">{{ formatDateTime(row.original.createdAt) }}</span>
        </template>
        <template #userName-cell="{ row }">
          <span class="text-sm">{{ row.original.userName ?? row.original.userId }}</span>
        </template>
        <template #feature-cell="{ row }">
          <UBadge
            variant="subtle"
            size="sm"
            color="primary"
          >
            {{ row.original.feature }}
          </UBadge>
        </template>
        <template #imageCount-cell="{ row }">
          <span class="text-sm tabular-nums">{{ row.original.imageCount }}</span>
        </template>
        <template #resolution-cell="{ row }">
          <span class="text-sm">{{ row.original.resolution }}</span>
        </template>
        <template #cost-cell="{ row }">
          <span class="text-sm tabular-nums">{{ formatCost(row.original.cost) }}</span>
        </template>
        <template #footer>
          <div class="flex justify-end px-4 py-2 text-sm font-semibold border-t border-muted">
            <span>Total Cost: {{ formatCost(totalCost) }}</span>
          </div>
        </template>
      </DataTable>
    </div>
  </div>
</template>
