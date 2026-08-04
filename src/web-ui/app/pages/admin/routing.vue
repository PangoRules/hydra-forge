<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import {
  AI_FEATURE_LABELS,
  AI_FEATURE_CATEGORY_ORDER,
  aiFeatureCategory,
  TIER_OPTIONS,
  MAX_TIER_OPTIONS,
  type LlmTier
} from '~/lib/ai-feature'
import CollapsibleSection from '~/components/shared/CollapsibleSection.vue'

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
const search = ref('')

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

function toggleAllowlistEditor(routing: FeatureRoutingDto) {
  const feature = routing.feature
  if (!expandedRow.value[feature]) {
    draftAllowedModels.value = {
      ...draftAllowedModels.value,
      [feature]: [...routing.allowedModelConfigIds]
    }
  }
  expandedRow.value = { ...expandedRow.value, [feature]: !expandedRow.value[feature] }
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

const filteredRoutings = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return routings.value
  return routings.value.filter((r) => {
    return featureLabel(r.feature).toLowerCase().includes(q)
      || aiFeatureCategory(r.feature).toLowerCase().includes(q)
  })
})

const groupedRoutings = computed(() => {
  const groups = new Map<string, FeatureRoutingDto[]>()
  for (const routing of filteredRoutings.value) {
    const category = aiFeatureCategory(routing.feature)
    if (!groups.has(category)) groups.set(category, [])
    groups.get(category)!.push(routing)
  }
  const order = [...AI_FEATURE_CATEGORY_ORDER, 'Other']
  return order
    .filter(category => groups.has(category))
    .map(category => ({ category, items: groups.get(category)! }))
})

onMounted(() => {
  loadRoutings()
  loadProviderModels()
})
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <div class="shrink-0 px-6 pt-6 pb-4 border-b border-default space-y-3">
      <div>
        <h1 class="text-2xl font-bold">
          AI Feature Routing
        </h1>
        <p class="text-sm text-muted mt-1">
          Configure which LLM tier each AI feature uses by default, the maximum tier accessible to non-admin users, and (optionally) a fixed, ordered list of models that overrides tier-based selection entirely.
        </p>
      </div>
      <UInput
        v-model="search"
        icon="i-lucide-search"
        placeholder="Filter features or categories..."
        class="max-w-sm"
      />
    </div>

    <div
      v-if="loading && routings.length === 0"
      class="flex-1 flex justify-center items-center p-8"
    >
      <UIcon
        name="i-lucide-loader-circle"
        class="animate-spin size-8"
      />
    </div>

    <div
      v-else
      class="flex-1 min-h-0 overflow-auto px-6 pb-6 pt-4 space-y-4"
    >
      <p
        v-if="groupedRoutings.length === 0"
        class="text-center text-muted p-8"
      >
        No features match "{{ search }}".
      </p>

      <CollapsibleSection
        v-for="group in groupedRoutings"
        :key="group.category"
        :title="group.category"
        :badge="group.items.length"
        :default-open="true"
      >
        <div
          v-for="routing in group.items"
          :key="routing.feature"
          class="rounded-md border border-muted/60 p-3"
        >
          <div class="flex flex-col sm:flex-row sm:items-center gap-3">
            <div class="sm:w-44 shrink-0 font-medium text-sm">
              {{ featureLabel(routing.feature) }}
            </div>

            <div class="flex-1 grid grid-cols-1 sm:grid-cols-3 gap-3 items-center">
              <div>
                <p class="text-xs text-muted mb-1 sm:hidden">
                  Default Tier
                </p>
                <USelect
                  :model-value="(routing.defaultTier as LlmTier)"
                  :items="TIER_OPTIONS"
                  :disabled="savingFeatures.has(routing.feature)"
                  class="w-full sm:w-36"
                  @update:model-value="onDefaultTierChange(routing.feature, $event as LlmTier)"
                />
              </div>

              <div>
                <p class="text-xs text-muted mb-1 sm:hidden">
                  Max User Tier
                </p>
                <USelect
                  :model-value="((routing.maxUserTier ?? 'Locked') as LlmTier | 'Locked')"
                  :items="MAX_TIER_OPTIONS"
                  :disabled="savingFeatures.has(routing.feature)"
                  class="w-full sm:w-48"
                  @update:model-value="onMaxUserTierChange(routing.feature, $event as LlmTier | 'Locked')"
                />
              </div>

              <div class="flex items-center gap-2">
                <UBadge
                  v-if="routing.allowedModelConfigIds.length === 0"
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
                  {{ routing.allowedModelConfigIds.length }} model{{ routing.allowedModelConfigIds.length === 1 ? '' : 's' }}
                </UBadge>
                <UButton
                  size="xs"
                  color="neutral"
                  variant="ghost"
                  :label="expandedRow[routing.feature] ? 'Close' : 'Configure'"
                  @click="toggleAllowlistEditor(routing)"
                />
              </div>
            </div>
          </div>

          <div
            v-if="expandedRow[routing.feature]"
            class="mt-3 pt-3 border-t border-muted/60 space-y-3"
          >
            <p class="text-xs text-muted">
              Restrict {{ featureLabel(routing.feature) }} to a fixed set of models, tried in order. Leave empty to fall back to tier-based routing.
            </p>
            <div
              v-if="(draftAllowedModels[routing.feature] ?? []).length > 0"
              class="space-y-1"
            >
              <div
                v-for="(modelConfigId, index) in draftAllowedModels[routing.feature]"
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
                  @click="moveAllowedModel(routing.feature, index, -1)"
                />
                <UButton
                  icon="i-lucide-arrow-down"
                  size="xs"
                  color="neutral"
                  variant="ghost"
                  :disabled="index === (draftAllowedModels[routing.feature]?.length ?? 0) - 1"
                  aria-label="Move down"
                  @click="moveAllowedModel(routing.feature, index, 1)"
                />
                <UButton
                  icon="i-lucide-x"
                  size="xs"
                  color="neutral"
                  variant="ghost"
                  aria-label="Remove"
                  @click="removeAllowedModel(routing.feature, index)"
                />
              </div>
            </div>
            <USelectMenu
              :model-value="''"
              :items="availableModelsFor(routing.feature)"
              value-key="value"
              size="sm"
              class="w-full max-w-sm"
              placeholder="+ Add model..."
              @update:model-value="(v: string) => v && addAllowedModel(routing.feature, v)"
            />
            <div class="flex gap-2 pt-1">
              <UButton
                size="sm"
                :loading="savingAllowlist.has(routing.feature)"
                @click="saveAllowlist(routing.feature)"
              >
                Save
              </UButton>
              <UButton
                size="sm"
                color="neutral"
                variant="ghost"
                @click="toggleAllowlistEditor(routing)"
              >
                Cancel
              </UButton>
            </div>
          </div>
        </div>
      </CollapsibleSection>
    </div>
  </div>
</template>
