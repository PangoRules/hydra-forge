<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import { AI_FEATURE_LABELS, TIER_OPTIONS, MAX_TIER_OPTIONS, type LlmTier } from '~/lib/ai-feature'
import DataTable from '~/components/shared/DataTable.vue'
import type { TableColumn } from '@nuxt/ui'

definePageMeta({ middleware: ['auth'] })

const user = useAuthStore().user
if (!user?.isAdmin) {
  navigateTo('/projects')
}

interface FeatureRoutingDto {
  id: string
  feature: string
  defaultTier: string
  maxUserTier: string | null
  createdAt: string
  updatedAt: string
}

const api = useApi()
const toast = useAppToast()

const routings = ref<FeatureRoutingDto[]>([])
const loading = ref(false)

const columns: TableColumn<FeatureRoutingDto>[] = [
  { accessorKey: 'feature', header: 'Feature' },
  { accessorKey: 'defaultTier', header: 'Default Tier' },
  { accessorKey: 'maxUserTier', header: 'Max User Tier' }
]

// Per-row saving state keyed by feature name
const savingFeatures = ref<Set<string>>(new Set())

// Stale request guard
let requestSeq = 0

async function loadRoutings() {
  const seq = ++requestSeq
  loading.value = true
  try {
    const { data, error } = await api.GET<FeatureRoutingDto[]>(ApiRoutes.Admin.routing.list())
    if (error) throw error
    if (seq !== requestSeq) return
    routings.value = data ?? []
  } catch (e) {
    if (seq !== requestSeq) return
    toast.showApiError(e as Error)
  } finally {
    if (seq === requestSeq) {
      loading.value = false
    }
  }
}

async function updateRouting(
  feature: string,
  defaultTier: string,
  maxUserTier: string | null
) {
  savingFeatures.value = new Set([...savingFeatures.value, feature])
  try {
    const body: { defaultTier: string, maxUserTier: string | null } = {
      defaultTier,
      maxUserTier: maxUserTier === 'Locked' ? null : (maxUserTier as LlmTier | null)
    }
    const { error } = await api.PUT(ApiRoutes.Admin.routing.update(feature), { body })
    if (error) throw error
    // Update local state with server response
    const idx = routings.value.findIndex(r => r.feature === feature)
    if (idx !== -1) {
      routings.value[idx]!.defaultTier = defaultTier
      routings.value[idx]!.maxUserTier = body.maxUserTier as string | null
    }
    toast.success('Routing updated')
  } catch (e) {
    toast.showApiError(e as Error)
    // Reload to reset optimistic state on failure
    await loadRoutings()
  } finally {
    const next = new Set(savingFeatures.value)
    next.delete(feature)
    savingFeatures.value = next
  }
}

function onDefaultTierChange(feature: string, newTier: LlmTier) {
  const routing = routings.value.find(r => r.feature === feature)
  if (!routing) return
  updateRouting(feature, newTier, routing.maxUserTier)
}

function onMaxUserTierChange(feature: string, newValue: LlmTier | 'Locked') {
  const routing = routings.value.find(r => r.feature === feature)
  if (!routing) return
  updateRouting(feature, routing.defaultTier, newValue)
}

function featureLabel(feature: string): string {
  return AI_FEATURE_LABELS[feature] ?? feature
}

onMounted(() => loadRoutings())
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <div class="shrink-0 px-6 pt-6 pb-4 border-b border-default">
      <h1 class="text-2xl font-bold">
        AI Feature Routing
      </h1>
      <p class="text-sm text-muted mt-1">
        Configure which LLM tier each AI feature uses by default and the maximum tier accessible to non-admin users.
      </p>
    </div>

    <div class="flex-1 min-h-0 px-6 pb-6 pt-4">
      <DataTable
        :data="routings"
        :columns="columns"
        :loading="loading"
        :page="1"
        :page-size="routings.length || 11"
        :total-count="routings.length"
        :row-key="(item: FeatureRoutingDto) => item.feature"
        fill-height
        hide-footer
      >
        <template #feature-cell="{ row }">
          <span class="font-medium">{{ featureLabel(row.original.feature) }}</span>
        </template>
        <template #defaultTier-cell="{ row }">
          <USelect
            :model-value="(row.original.defaultTier as LlmTier)"
            :items="TIER_OPTIONS"
            :disabled="savingFeatures.has(row.original.feature)"
            class="w-36"
            @update:model-value="onDefaultTierChange(row.original.feature, $event as LlmTier)"
          />
        </template>
        <template #maxUserTier-cell="{ row }">
          <USelect
            :model-value="((row.original.maxUserTier ?? 'Locked') as LlmTier | 'Locked')"
            :items="MAX_TIER_OPTIONS"
            :disabled="savingFeatures.has(row.original.feature)"
            class="w-48"
            @update:model-value="onMaxUserTierChange(row.original.feature, $event as LlmTier | 'Locked')"
          />
        </template>

        <template #card="{ item }">
          <UCard>
            <div class="space-y-3">
              <div>
                <p class="font-medium">
                  {{ featureLabel(item.feature) }}
                </p>
              </div>
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <p class="text-xs text-muted mb-1">
                    Default Tier
                  </p>
                  <USelect
                    :model-value="(item.defaultTier as LlmTier)"
                    :items="TIER_OPTIONS"
                    :disabled="savingFeatures.has(item.feature)"
                    class="w-full"
                    @update:model-value="onDefaultTierChange(item.feature, $event as LlmTier)"
                  />
                </div>
                <div>
                  <p class="text-xs text-muted mb-1">
                    Max User Tier
                  </p>
                  <USelect
                    :model-value="((item.maxUserTier ?? 'Locked') as LlmTier | 'Locked')"
                    :items="MAX_TIER_OPTIONS"
                    :disabled="savingFeatures.has(item.feature)"
                    class="w-full"
                    @update:model-value="onMaxUserTierChange(item.feature, $event as LlmTier | 'Locked')"
                  />
                </div>
              </div>
            </div>
          </UCard>
        </template>
      </DataTable>
    </div>
  </div>
</template>
