<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import { formatDateTime } from '~/lib/date'
import type { TableColumn } from '@nuxt/ui'

definePageMeta({ middleware: ['auth'] })

const api = useApi()
const toast = useAppToast()

interface RecentCallDto {
  feature: string
  model: string
  tokens: number
  images: number
  cost: number
  timestamp: string
}

interface AccountUsageResponse {
  tokensUsed: number
  tokensBudget: number
  imagesUsed: number
  imagesBudget: number
  periodStart: string
  periodEnd: string
  recentCalls: RecentCallDto[]
}

const usage = ref<AccountUsageResponse | null>(null)
const loading = ref(false)

const recentColumns: TableColumn<RecentCallDto>[] = [
  { accessorKey: 'timestamp', header: 'Timestamp' },
  { accessorKey: 'feature', header: 'Feature' },
  { accessorKey: 'model', header: 'Model' },
  { accessorKey: 'tokens', header: 'Tokens' },
  { accessorKey: 'images', header: 'Images' },
  { accessorKey: 'cost', header: 'Cost' }
]

function usagePercent(used: number, budget: number): number {
  if (budget <= 0) return 0
  return Math.min(100, Math.round((used / budget) * 100))
}

async function loadUsage() {
  loading.value = true
  try {
    const { data, error } = await api.GET<AccountUsageResponse>(ApiRoutes.Account.usage())
    if (error) throw error
    usage.value = data!
  } catch (e) {
    toast.showApiError(e as Error)
  } finally {
    loading.value = false
  }
}

onMounted(() => loadUsage())
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <div class="shrink-0 border-b border-default px-6 py-4">
      <h1 class="text-2xl font-bold">
        My Usage
      </h1>
    </div>

    <div
      v-if="loading && !usage"
      class="flex-1 flex items-center justify-center"
    >
      <span class="text-muted">Loading...</span>
    </div>

    <div
      v-else-if="usage"
      class="flex-1 min-h-0 px-6 pb-6 pt-4 space-y-6 overflow-y-auto"
    >
      <!-- Period -->
      <div class="text-sm text-muted">
        Period: {{ formatDateTime(usage.periodStart) }} — {{ formatDateTime(usage.periodEnd) }}
      </div>

      <!-- Token Usage Bar -->
      <div class="space-y-2">
        <div class="flex items-center justify-between text-sm">
          <span class="font-medium">Token Usage</span>
          <span
            v-if="usage.tokensBudget > 0"
            class="tabular-nums"
          >
            {{ usage.tokensUsed.toLocaleString() }} / {{ usage.tokensBudget.toLocaleString() }}
            ({{ usagePercent(usage.tokensUsed, usage.tokensBudget) }}%)
          </span>
          <span
            v-else
            class="text-muted"
          >Unlimited</span>
        </div>
        <div
          v-if="usage.tokensBudget > 0"
          class="w-full h-3 rounded-full bg-muted overflow-hidden"
        >
          <div
            class="h-full rounded-full bg-primary transition-all"
            :style="{ width: `${usagePercent(usage.tokensUsed, usage.tokensBudget)}%` }"
          />
        </div>
      </div>

      <!-- Image Usage Bar -->
      <div class="space-y-2">
        <div class="flex items-center justify-between text-sm">
          <span class="font-medium">Image Usage</span>
          <span
            v-if="usage.imagesBudget > 0"
            class="tabular-nums"
          >
            {{ usage.imagesUsed.toLocaleString() }} / {{ usage.imagesBudget.toLocaleString() }}
            ({{ usagePercent(usage.imagesUsed, usage.imagesBudget) }}%)
          </span>
          <span
            v-else
            class="text-muted"
          >Unlimited</span>
        </div>
        <div
          v-if="usage.imagesBudget > 0"
          class="w-full h-3 rounded-full bg-muted overflow-hidden"
        >
          <div
            class="h-full rounded-full bg-primary transition-all"
            :style="{ width: `${usagePercent(usage.imagesUsed, usage.imagesBudget)}%` }"
          />
        </div>
      </div>

      <!-- Recent Calls -->
      <div>
        <h2 class="text-lg font-semibold mb-3">
          Recent Calls
        </h2>
        <UTable
          :data="usage.recentCalls"
          :columns="recentColumns"
          :row-key="(_: RecentCallDto, idx: number) => String(idx)"
          class="text-sm"
        >
          <template #timestamp-cell="{ row }">
            {{ formatDateTime(row.original.timestamp) }}
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
          <template #tokens-cell="{ row }">
            <span class="tabular-nums">{{ row.original.tokens.toLocaleString() }}</span>
          </template>
          <template #images-cell="{ row }">
            <span class="tabular-nums">{{ row.original.images.toLocaleString() }}</span>
          </template>
          <template #cost-cell="{ row }">
            <span class="tabular-nums">${{ row.original.cost.toFixed(4) }}</span>
          </template>
        </UTable>
      </div>
    </div>
  </div>
</template>
