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
  allowedModelConfigIds: string[]
}

interface ProviderDto {
  id: string
  name: string
}

interface ProviderModelConfigDto {
  id: string
  name: string
}

interface ProviderModelOption {
  id: string
  label: string
}

const api = useApi()
const toast = useAppToast()

const routings = ref<FeatureRoutingDto[]>([])
const loading = ref(false)
const providerModels = ref<ProviderModelOption[]>([])

const columns: TableColumn<FeatureRoutingDto>[] = [
  { accessorKey: 'feature', header: 'Feature' },
  { accessorKey: 'defaultTier', header: 'Default Tier' },
  { accessorKey: 'maxUserTier', header: 'Max User Tier' },
  { accessorKey: 'id', header: 'Allowed Models' }
]

// Per-row saving state keyed by feature name
const savingFeatures = ref<Set<string>>(new Set())
const savingAllowlist = ref<Set<string>>(new Set())
const expandedRow = ref<Record<string, boolean>>({})
// Working copy of the allowed-model order while a row's allowlist editor is open
const draftAllowedModels = ref<Record<string, string[]>>({})

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

async function loadProviderModels() {
  try {
    const { data: providerPage, error: providersError } = await api.GET<{ items: ProviderDto[] }>(
      `${ApiRoutes.Admin.providers.list()}?skip=0&take=200`
    )
    if (providersError) throw providersError

    const options: ProviderModelOption[] = []
    for (const provider of providerPage?.items ?? []) {
      const { data: models, error: modelsError } = await api.GET<ProviderModelConfigDto[]>(
        ApiRoutes.Admin.providers.listModels(provider.id)
      )
      if (modelsError) throw modelsError
      for (const model of models ?? []) {
        options.push({ id: model.id, label: `${provider.name} — ${model.name}` })
      }
    }
    providerModels.value = options
  } catch (e) {
    toast.showApiError(e as Error)
  }
}

function modelLabel(modelConfigId: string): string {
  return providerModels.value.find(m => m.id === modelConfigId)?.label ?? modelConfigId
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

function availableModelsFor(feature: string): { label: string, value: string }[] {
  const selected = new Set(draftAllowedModels.value[feature] ?? [])
  return providerModels.value
    .filter(m => !selected.has(m.id))
    .map(m => ({ label: m.label, value: m.id }))
}

function addAllowedModel(feature: string, modelConfigId: string) {
  const draft = draftAllowedModels.value[feature] ?? []
  draftAllowedModels.value = { ...draftAllowedModels.value, [feature]: [...draft, modelConfigId] }
}

function removeAllowedModel(feature: string, index: number) {
  const draft = [...(draftAllowedModels.value[feature] ?? [])]
  draft.splice(index, 1)
  draftAllowedModels.value = { ...draftAllowedModels.value, [feature]: draft }
}

function moveAllowedModel(feature: string, index: number, delta: number) {
  const draft = [...(draftAllowedModels.value[feature] ?? [])]
  const target = index + delta
  if (target < 0 || target >= draft.length) return
  const item = draft[index]!
  draft.splice(index, 1)
  draft.splice(target, 0, item)
  draftAllowedModels.value = { ...draftAllowedModels.value, [feature]: draft }
}

function toggleAllowlistEditor(row: { original: FeatureRoutingDto, toggleExpanded: () => void }) {
  const feature = row.original.feature
  if (!expandedRow.value[feature]) {
    draftAllowedModels.value = {
      ...draftAllowedModels.value,
      [feature]: [...row.original.allowedModelConfigIds]
    }
  }
  row.toggleExpanded()
}

async function saveAllowlist(feature: string) {
  savingAllowlist.value = new Set([...savingAllowlist.value, feature])
  try {
    const modelConfigIds = draftAllowedModels.value[feature] ?? []
    const { data, error } = await api.PUT<FeatureRoutingDto>(
      ApiRoutes.Admin.routing.setAllowedModels(feature),
      { body: { modelConfigIds } }
    )
    if (error) throw error
    const idx = routings.value.findIndex(r => r.feature === feature)
    if (idx !== -1 && data) {
      routings.value[idx]!.allowedModelConfigIds = data.allowedModelConfigIds
    }
    toast.success('Allowed models updated')
    expandedRow.value = { ...expandedRow.value, [feature]: false }
  } catch (e) {
    toast.showApiError(e as Error)
  } finally {
    const next = new Set(savingAllowlist.value)
    next.delete(feature)
    savingAllowlist.value = next
  }
}

onMounted(() => {
  loadRoutings()
  loadProviderModels()
})
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <div class="shrink-0 px-6 pt-6 pb-4 border-b border-default">
      <h1 class="text-2xl font-bold">
        AI Feature Routing
      </h1>
      <p class="text-sm text-muted mt-1">
        Configure which LLM tier each AI feature uses by default, the maximum tier accessible to non-admin users, and (optionally) a fixed, ordered list of models that overrides tier-based selection entirely.
      </p>
    </div>

    <div class="flex-1 min-h-0 px-6 pb-6 pt-4">
      <DataTable
        v-model:expanded="expandedRow"
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
        <template #id-cell="{ row }">
          <div class="flex items-center gap-2">
            <UBadge
              v-if="row.original.allowedModelConfigIds.length === 0"
              variant="subtle"
              color="neutral"
              size="sm"
            >
              Auto (tier-based)
            </UBadge>
            <UBadge
              v-else
              variant="subtle"
              color="primary"
              size="sm"
            >
              {{ row.original.allowedModelConfigIds.length }} model{{ row.original.allowedModelConfigIds.length === 1 ? '' : 's' }}
            </UBadge>
            <UButton
              size="xs"
              color="neutral"
              variant="ghost"
              :label="expandedRow[row.original.feature] ? 'Close' : 'Configure'"
              @click="toggleAllowlistEditor(row)"
            />
          </div>
        </template>
        <template #expanded="{ row }">
          <div class="p-4 space-y-3">
            <p class="text-xs text-muted">
              Restrict {{ featureLabel(row.original.feature) }} to a fixed set of models, tried in order. Leave empty to fall back to tier-based routing.
            </p>
            <div
              v-if="(draftAllowedModels[row.original.feature] ?? []).length > 0"
              class="space-y-1"
            >
              <div
                v-for="(modelConfigId, index) in draftAllowedModels[row.original.feature]"
                :key="modelConfigId"
                class="flex items-center gap-2 text-sm"
              >
                <span class="w-5 text-xs text-muted">{{ index + 1 }}.</span>
                <span class="flex-1">{{ modelLabel(modelConfigId) }}</span>
                <UButton
                  icon="i-lucide-arrow-up"
                  size="xs"
                  color="neutral"
                  variant="ghost"
                  :disabled="index === 0"
                  aria-label="Move up"
                  @click="moveAllowedModel(row.original.feature, index, -1)"
                />
                <UButton
                  icon="i-lucide-arrow-down"
                  size="xs"
                  color="neutral"
                  variant="ghost"
                  :disabled="index === (draftAllowedModels[row.original.feature]?.length ?? 0) - 1"
                  aria-label="Move down"
                  @click="moveAllowedModel(row.original.feature, index, 1)"
                />
                <UButton
                  icon="i-lucide-x"
                  size="xs"
                  color="neutral"
                  variant="ghost"
                  aria-label="Remove"
                  @click="removeAllowedModel(row.original.feature, index)"
                />
              </div>
            </div>
            <USelectMenu
              :model-value="''"
              :items="availableModelsFor(row.original.feature)"
              value-key="value"
              size="sm"
              class="w-full max-w-sm"
              placeholder="+ Add model..."
              @update:model-value="(v: string) => v && addAllowedModel(row.original.feature, v)"
            />
            <div class="flex gap-2 pt-1">
              <UButton
                size="sm"
                :loading="savingAllowlist.has(row.original.feature)"
                @click="saveAllowlist(row.original.feature)"
              >
                Save
              </UButton>
              <UButton
                size="sm"
                color="neutral"
                variant="ghost"
                @click="row.toggleExpanded()"
              >
                Cancel
              </UButton>
            </div>
          </div>
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
              <div>
                <p class="text-xs text-muted mb-1">
                  Allowed Models
                </p>
                <UBadge
                  v-if="item.allowedModelConfigIds.length === 0"
                  variant="subtle"
                  color="neutral"
                  size="sm"
                >
                  Auto (tier-based)
                </UBadge>
                <UBadge
                  v-else
                  variant="subtle"
                  color="primary"
                  size="sm"
                >
                  {{ item.allowedModelConfigIds.length }} model{{ item.allowedModelConfigIds.length === 1 ? '' : 's' }}
                </UBadge>
              </div>
            </div>
          </UCard>
        </template>
      </DataTable>
    </div>
  </div>
</template>
