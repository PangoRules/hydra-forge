<script setup lang="ts">
import { ApiRoutes, UiRoutes } from '~/lib/routes'
import DataTable from '~/components/shared/DataTable.vue'
import ClientDataTable from '~/components/shared/ClientDataTable.vue'
import AppModal from '~/components/shared/AppModal.vue'
import ProviderModal, { type ProviderFormData, type ProviderDto } from '~/components/admin/ProviderModal.vue'

definePageMeta({ middleware: ['auth'] })

const user = useAuthStore().user
if (!user?.isAdmin) {
  navigateTo(UiRoutes.Chats)
}

interface ProbedModelDto {
  modelId: string
  name: string
  description: string | null
  metadata: Record<string, string> | null
}

interface ProviderModelConfigDto {
  id: string
  providerId: string
  modelId: string
  name: string
  tier: string
  pricePerToken: number | null
  maxTokens: number | null
  isEnabled: boolean
  supportsReasoning: boolean
  ollamaThinkMode: string
}

interface CreateModelInput {
  modelId: string
  name: string
  tier: string
  pricePerToken: number | null
  maxTokens: number | null
  isEnabled: boolean
  supportsReasoning: boolean
  ollamaThinkMode: string
}

interface UpdateModelInput {
  name?: string | null
  tier?: string | null
  pricePerToken?: number | null
  maxTokens?: number | null
  isEnabled?: boolean
  supportsReasoning?: boolean
  ollamaThinkMode?: string
}

const MODEL_TIERS = [
  { label: 'Economy', value: 'Economy' },
  { label: 'Standard', value: 'Standard' },
  { label: 'Premium', value: 'Premium' }
]

// Ollama-only: some thinking-capable local models (e.g. a "gemma4:26b") burn their
// whole token budget on hidden reasoning and return empty content for short tasks
// like chat titles unless thinking is explicitly turned off. "Auto" only enables it
// when a reasoning effort was explicitly requested elsewhere in the app.
const OLLAMA_THINK_MODES = [
  { label: 'Auto (default)', value: 'Auto' },
  { label: 'Always on', value: 'On' },
  { label: 'Always off', value: 'Off' }
]

const api = useApi()
const toast = useAppToast()

// ---------------------------------------------------------------------------
// Providers — compact top section. Not many providers ever exist relative to
// the number of models each one has, so this is the full list, client-paged,
// no server round-trip per page like the models table below needs.
// ---------------------------------------------------------------------------

const providers = ref<ProviderDto[]>([])
const providersLoading = ref(false)
const selectedProviderId = ref<string | undefined>(undefined)

const providerColumns = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'adapterType', header: 'Adapter' },
  { accessorKey: 'providerType', header: 'Type' },
  { accessorKey: 'tier', header: 'Tier' },
  { accessorKey: 'isEnabled', header: 'Status' },
  { accessorKey: 'actions', header: 'Actions', enableSorting: false }
]

async function loadProviders() {
  providersLoading.value = true
  try {
    const { data, error } = await api.GET<{ items: ProviderDto[], totalCount: number }>(
      ApiRoutes.Admin.providers.list() + '?take=200'
    )
    if (error) throw error
    providers.value = data?.items ?? []
    // Not many providers — default straight to the first one's models instead
    // of making the admin pick from an empty-looking models section below.
    if (!selectedProviderId.value && providers.value.length > 0) {
      selectedProviderId.value = providers.value[0]!.id
    }
    if (selectedProviderId.value) await loadModels()
  } catch (e) {
    toast.error((e as Error).message || 'Failed to load providers')
  } finally {
    providersLoading.value = false
  }
}

function selectProvider(id: string) {
  if (selectedProviderId.value === id) return
  selectedProviderId.value = id
  page.value = 1
  loadModels()
}

// Provider add/edit modal state
const showProviderModal = ref(false)
const editingProvider = ref<ProviderDto | null>(null)
const providerModalLoading = ref(false)
const providerModalError = ref<string | null>(null)

function openAddProviderModal() {
  editingProvider.value = null
  providerModalError.value = null
  showProviderModal.value = true
}

function openEditProviderModal(provider: ProviderDto) {
  editingProvider.value = provider
  providerModalError.value = null
  showProviderModal.value = true
}

async function handleProviderModalSubmit(data: ProviderFormData) {
  providerModalLoading.value = true
  providerModalError.value = null
  try {
    if (editingProvider.value) {
      const body: Record<string, unknown> = {
        name: data.name,
        baseUrl: data.baseUrl,
        adapterType: data.adapterType,
        providerType: data.providerType,
        tier: data.tier,
        fallbackProviderId: data.fallbackProviderId
      }
      if (data.apiKey) body.apiKey = data.apiKey
      await api.PUT(ApiRoutes.Admin.providers.update(editingProvider.value.id), { body })
      toast.success('Provider updated')
    } else {
      await api.POST(ApiRoutes.Admin.providers.create(), {
        body: {
          name: data.name,
          baseUrl: data.baseUrl,
          adapterType: data.adapterType,
          providerType: data.providerType,
          tier: data.tier,
          fallbackProviderId: data.fallbackProviderId,
          apiKey: data.apiKey
        }
      })
      toast.success('Provider created')
    }
    showProviderModal.value = false
    await loadProviders()
  } catch (e) {
    providerModalError.value = (e as Error).message || 'Operation failed'
  } finally {
    providerModalLoading.value = false
  }
}

// Disable/enable confirm state
const confirmTargetId = ref<string | null>(null)
const confirmCurrentEnabled = ref(false)

function showDisableConfirm(provider: ProviderDto) {
  confirmTargetId.value = provider.id
  confirmCurrentEnabled.value = provider.isEnabled
}

async function confirmToggleDisable() {
  if (!confirmTargetId.value) return
  const targetId = confirmTargetId.value
  const wasEnabled = confirmCurrentEnabled.value
  confirmTargetId.value = null
  try {
    if (wasEnabled) {
      await api.DELETE(ApiRoutes.Admin.providers.disable(targetId))
    } else {
      await api.PUT(ApiRoutes.Admin.providers.update(targetId), { body: { isEnabled: true } })
    }
    await loadProviders()
    toast.success(`Provider ${wasEnabled ? 'disabled' : 'enabled'}`)
  } catch (e) {
    toast.error((e as Error).message || 'Action failed')
  }
}

// Hard-delete confirm state
const deleteProviderTarget = ref<ProviderDto | null>(null)
const deletingProvider = ref(false)

async function confirmDeleteProvider() {
  if (!deleteProviderTarget.value) return
  deletingProvider.value = true
  try {
    await api.DELETE(ApiRoutes.Admin.providers.delete(deleteProviderTarget.value.id))
    toast.success('Provider permanently deleted')
    if (selectedProviderId.value === deleteProviderTarget.value.id) selectedProviderId.value = undefined
    deleteProviderTarget.value = null
    await loadProviders()
  } catch (e) {
    toast.error((e as Error).message || 'Delete failed')
  } finally {
    deletingProvider.value = false
  }
}

// ---------------------------------------------------------------------------
// Provider Models — the rest of the page, scoped to selectedProviderId above.
// ---------------------------------------------------------------------------

const models = ref<ProviderModelConfigDto[]>([])
const loading = ref(false)
const page = ref(1)
const pageSize = ref(20)

const modelSearch = ref('')
const modelTierFilter = ref('all')
const tierFilterOptions = [{ label: 'All Tiers', value: 'all' }, ...MODEL_TIERS]

const selectedProvider = computed(() =>
  providers.value.find(p => p.id === selectedProviderId.value)
)

const filteredModels = computed(() => {
  const q = modelSearch.value.trim().toLowerCase()
  return models.value.filter((m) => {
    if (modelTierFilter.value !== 'all' && m.tier !== modelTierFilter.value) return false
    if (!q) return true
    return m.modelId.toLowerCase().includes(q) || m.name.toLowerCase().includes(q)
  })
})

const totalCount = computed(() => filteredModels.value.length)
const pagedModels = computed(() => {
  const start = (page.value - 1) * pageSize.value
  return filteredModels.value.slice(start, start + pageSize.value)
})

watch([modelSearch, modelTierFilter], () => {
  page.value = 1
})

// Add/edit modal state
const showModal = ref(false)
const editingModel = ref<ProviderModelConfigDto | null>(null)
const modalLoading = ref(false)
const modalError = ref<string | null>(null)

// Discover (probe) modal state
const showProbeModal = ref(false)
const probeResults = ref<ProbedModelDto[]>([])
const probeLoading = ref(false)
const probeError = ref<string | null>(null)
const probeFilter = ref('')

const filteredProbeResults = computed(() => {
  const q = probeFilter.value.trim().toLowerCase()
  if (!q) return probeResults.value
  return probeResults.value.filter(
    m => m.modelId.toLowerCase().includes(q) || (m.name ?? '').toLowerCase().includes(q)
  )
})

const probeColumns = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'modelId', header: 'Model ID' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const configuredModelIds = computed(() => new Set(models.value.map(m => m.modelId)))

// Form fields
const formModelId = ref('')
const formName = ref('')
const formTier = ref('Standard')
const formPricePerToken = ref<number | null>(null)
const formMaxTokens = ref<number | null>(null)
const formEnabled = ref(true)
const formSupportsReasoning = ref(false)
const formThinkMode = ref('Auto')

const isOllamaProvider = computed(() => selectedProvider.value?.adapterType === 'Ollama')

// $/token values for real-world models are tiny (often < 1e-6) and render in
// ugly scientific notation ("7.6e-7") in a plain number input. Editing in
// $/1M-tokens — the unit every provider actually quotes pricing in — keeps
// the field a normal-looking decimal while `formPricePerToken` (submitted to
// the API) stays the source of truth.
const formPricePerMillionTokens = computed({
  get: () => formPricePerToken.value === null ? null : formPricePerToken.value * 1_000_000,
  set: (val: number | null) => {
    formPricePerToken.value = val === null ? null : val / 1_000_000
  }
})

// Delete confirm
const deleteTargetId = ref<string | null>(null)

// Form ref for native validation
const modelFormRef = ref<HTMLFormElement | null>(null)

// "Thinking" column only makes sense for Ollama providers — lets an admin see at a
// glance which models have been overridden away from Auto without opening each one.
const modelColumns = computed(() => [
  { accessorKey: 'modelId', header: 'Model ID' },
  { accessorKey: 'name', header: 'Display Name' },
  { accessorKey: 'tier', header: 'Tier' },
  { accessorKey: 'isEnabled', header: 'Status' },
  ...(isOllamaProvider.value ? [{ accessorKey: 'thinkMode', header: 'Thinking' }] : []),
  { accessorKey: 'actions', header: 'Actions', enableSorting: false }
])

async function loadModels() {
  if (!selectedProviderId.value) {
    models.value = []
    return
  }
  loading.value = true
  try {
    const { data, error } = await api.GET<ProviderModelConfigDto[]>(
      ApiRoutes.Admin.providers.listModels(selectedProviderId.value)
    )
    if (error) throw error
    models.value = data ?? []
  } catch (e) {
    toast.error((e as Error).message || 'Failed to load models')
  } finally {
    loading.value = false
  }
}

function openAddModal() {
  editingModel.value = null
  resetForm()
  modalError.value = null
  showModal.value = true
}

function openEditModal(model: ProviderModelConfigDto) {
  editingModel.value = model
  formModelId.value = model.modelId
  formName.value = model.name
  formTier.value = model.tier
  formPricePerToken.value = model.pricePerToken
  formMaxTokens.value = model.maxTokens
  formEnabled.value = model.isEnabled
  formSupportsReasoning.value = model.supportsReasoning
  formThinkMode.value = model.ollamaThinkMode
  modalError.value = null
  showModal.value = true
}

function resetForm() {
  formModelId.value = ''
  formName.value = ''
  formTier.value = 'Standard'
  formPricePerToken.value = null
  formMaxTokens.value = null
  formEnabled.value = true
  formSupportsReasoning.value = false
  formThinkMode.value = 'Auto'
}

async function handleModalSubmit() {
  if (!selectedProviderId.value) return
  modalLoading.value = true
  modalError.value = null
  try {
    if (editingModel.value) {
      const body: UpdateModelInput = {
        name: formName.value,
        tier: formTier.value,
        pricePerToken: formPricePerToken.value,
        maxTokens: formMaxTokens.value,
        isEnabled: formEnabled.value,
        supportsReasoning: formSupportsReasoning.value,
        ollamaThinkMode: formThinkMode.value
      }
      await api.PUT(
        ApiRoutes.Admin.providers.updateModel(selectedProviderId.value, editingModel.value.id),
        { body }
      )
      toast.success('Model updated')
    } else {
      const body: CreateModelInput = {
        modelId: formModelId.value,
        name: formName.value,
        tier: formTier.value,
        pricePerToken: formPricePerToken.value,
        maxTokens: formMaxTokens.value,
        isEnabled: formEnabled.value,
        supportsReasoning: formSupportsReasoning.value,
        ollamaThinkMode: formThinkMode.value
      }
      await api.POST(
        ApiRoutes.Admin.providers.createModel(selectedProviderId.value),
        { body }
      )
      toast.success('Model created')
    }
    showModal.value = false
    await loadModels()
  } catch (e) {
    modalError.value = (e as Error).message || 'Operation failed'
  } finally {
    modalLoading.value = false
  }
}

async function toggleModelEnabled(model: ProviderModelConfigDto) {
  if (!selectedProviderId.value) return
  try {
    await api.PUT(
      ApiRoutes.Admin.providers.updateModel(selectedProviderId.value, model.id),
      { body: { isEnabled: !model.isEnabled } satisfies UpdateModelInput }
    )
    toast.success(`Model ${model.isEnabled ? 'disabled' : 'enabled'}`)
    await loadModels()
  } catch (e) {
    toast.error((e as Error).message || 'Action failed')
  }
}

async function deleteModel(modelId: string) {
  if (!selectedProviderId.value) return
  try {
    await api.DELETE(
      ApiRoutes.Admin.providers.deleteModel(selectedProviderId.value, modelId)
    )
    toast.success('Model deleted')
    deleteTargetId.value = null
    await loadModels()
  } catch (e) {
    toast.error((e as Error).message || 'Delete failed')
  }
}

async function discoverModels() {
  if (!selectedProviderId.value) return
  probeLoading.value = true
  probeError.value = null
  probeResults.value = []
  probeFilter.value = ''
  showProbeModal.value = true
  try {
    const { data, error } = await api.GET<ProbedModelDto[]>(
      ApiRoutes.Admin.providers.probeModels(selectedProviderId.value)
    )
    if (error) throw error
    probeResults.value = data ?? []
  } catch (e) {
    probeError.value = (e as Error).message || 'Probe failed'
  } finally {
    probeLoading.value = false
  }
}

function discoveredPrice(model: ProbedModelDto): number | null {
  const raw = model.metadata?.pricePerToken
  if (!raw) return null
  const parsed = Number(raw)
  return Number.isFinite(parsed) ? parsed : null
}

function addProbedModel(model: ProbedModelDto) {
  if (configuredModelIds.value.has(model.modelId)) return
  showProbeModal.value = false
  editingModel.value = null
  formModelId.value = model.modelId
  formName.value = model.name
  formTier.value = 'Standard'
  formPricePerToken.value = discoveredPrice(model)
  formMaxTokens.value = null
  formEnabled.value = true
  formSupportsReasoning.value = false
  modalError.value = null
  showModal.value = true
}

async function applyDiscoveredPrice(model: ProbedModelDto) {
  const price = discoveredPrice(model)
  const existing = models.value.find(m => m.modelId === model.modelId)
  if (!selectedProviderId.value || price === null || !existing) return
  try {
    await api.PUT(
      ApiRoutes.Admin.providers.updateModel(selectedProviderId.value, existing.id),
      { body: { pricePerToken: price } satisfies UpdateModelInput }
    )
    toast.success('Price updated from discovered pricing')
    await loadModels()
  } catch (e) {
    toast.error((e as Error).message || 'Failed to update price')
  }
}

onMounted(() => loadProviders())
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0 overflow-y-auto">
    <div class="shrink-0 px-6 pt-6 space-y-4">
      <div class="flex items-center justify-between">
        <h1 class="text-2xl font-bold">
          LLM Providers
        </h1>
        <UButton
          label="Add Provider"
          @click="openAddProviderModal"
        />
      </div>
    </div>

    <!-- Providers — compact, ~30% of the page. Click a row to scope the
         models section below to it. -->
    <div class="shrink-0 px-6 pb-4 pt-4 max-h-[30vh] overflow-y-auto">
      <ClientDataTable
        :data="providers"
        :columns="providerColumns"
        :loading="providersLoading"
        :row-key="(item: ProviderDto) => item.id"
        :default-page-size="5"
        selectable
        @select="(item: ProviderDto) => selectProvider(item.id)"
      >
        <template #name-cell="{ row }">
          <span class="font-medium inline-flex items-center gap-1.5">
            <span
              v-if="row.original.id === selectedProviderId"
              class="size-1.5 rounded-full bg-primary shrink-0"
              title="Viewing this provider's models below"
            />
            {{ row.original.name }}
          </span>
        </template>
        <template #isEnabled-cell="{ row }">
          <UBadge :color="row.original.isEnabled ? 'success' : 'error'">
            {{ row.original.isEnabled ? 'Enabled' : 'Disabled' }}
          </UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex gap-1">
            <UButton
              size="xs"
              variant="subtle"
              color="neutral"
              @click.stop="openEditProviderModal(row.original)"
            >
              Edit
            </UButton>
            <UButton
              v-if="row.original.isEnabled"
              size="xs"
              variant="subtle"
              color="warning"
              @click.stop="showDisableConfirm(row.original)"
            >
              Disable
            </UButton>
            <UButton
              v-else
              size="xs"
              variant="subtle"
              color="success"
              @click.stop="showDisableConfirm(row.original)"
            >
              Enable
            </UButton>
            <UButton
              size="xs"
              variant="solid"
              color="error"
              @click.stop="deleteProviderTarget = row.original"
            >
              Delete
            </UButton>
          </div>
        </template>
      </ClientDataTable>
    </div>

    <USeparator />

    <!-- Provider Models — scoped to the provider selected above. -->
    <div class="shrink-0 px-6 pt-4 space-y-4">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold">
          Models<template v-if="selectedProvider">
            — {{ selectedProvider.name }}
          </template>
        </h2>
        <div class="flex gap-2">
          <UButton
            label="Discover Models"
            color="neutral"
            variant="outline"
            :disabled="!selectedProviderId"
            @click="discoverModels"
          />
          <UButton
            label="Add Model"
            :disabled="!selectedProviderId"
            @click="openAddModal"
          />
        </div>
      </div>

      <div
        v-if="selectedProviderId"
        class="flex flex-wrap gap-3"
      >
        <UInput
          v-model="modelSearch"
          icon="i-lucide-search"
          placeholder="Filter by model ID or name..."
          class="w-64"
        />
        <USelect
          v-model="modelTierFilter"
          :items="tierFilterOptions"
          class="w-40"
        />
      </div>
    </div>

    <div class="flex-1 min-h-0 px-6 pb-6 pt-4">
      <DataTable
        v-if="selectedProviderId"
        :data="pagedModels"
        :columns="modelColumns"
        :loading="loading"
        :page="page"
        :page-size="pageSize"
        :total-count="totalCount"
        :row-key="(item: ProviderModelConfigDto) => item.id"
        fill-height
        @update:page="page = $event"
        @update:page-size="pageSize = $event"
      >
        <template #isEnabled-cell="{ row }">
          <UBadge :color="row.original.isEnabled ? 'success' : 'error'">
            {{ row.original.isEnabled ? 'Enabled' : 'Disabled' }}
          </UBadge>
        </template>

        <template #ollamaThinkMode-cell="{ row }">
          <UBadge
            v-if="row.original.ollamaThinkMode !== 'Auto'"
            :color="row.original.ollamaThinkMode === 'On' ? 'primary' : 'neutral'"
          >
            {{ row.original.ollamaThinkMode === 'On' ? 'Always on' : 'Always off' }}
          </UBadge>
          <span
            v-else
            class="text-muted text-xs"
          >Auto</span>
        </template>

        <template #actions-cell="{ row }">
          <div class="flex gap-1">
            <UButton
              size="xs"
              variant="subtle"
              :color="row.original.isEnabled ? 'warning' : 'success'"
              @click="toggleModelEnabled(row.original)"
            >
              {{ row.original.isEnabled ? 'Disable' : 'Enable' }}
            </UButton>
            <UButton
              size="xs"
              variant="subtle"
              color="neutral"
              @click="openEditModal(row.original)"
            >
              Edit
            </UButton>
            <UButton
              size="xs"
              variant="solid"
              color="error"
              @click="deleteTargetId = row.original.id"
            >
              Delete
            </UButton>
          </div>
        </template>

        <template #card="{ item }">
          <UCard>
            <div class="space-y-2">
              <div class="flex items-center justify-between">
                <span class="font-medium">{{ item.name || item.modelId }}</span>
                <UBadge :color="item.isEnabled ? 'success' : 'error'">
                  {{ item.isEnabled ? 'Enabled' : 'Disabled' }}
                </UBadge>
              </div>
              <p class="text-xs text-muted font-mono">
                {{ item.modelId }}
              </p>
              <div class="flex gap-1">
                <UButton
                  size="xs"
                  variant="subtle"
                  :color="item.isEnabled ? 'warning' : 'success'"
                  @click="toggleModelEnabled(item)"
                >
                  {{ item.isEnabled ? 'Disable' : 'Enable' }}
                </UButton>
                <UButton
                  size="xs"
                  variant="subtle"
                  color="neutral"
                  @click="openEditModal(item)"
                >
                  Edit
                </UButton>
                <UButton
                  size="xs"
                  variant="solid"
                  color="error"
                  @click="deleteTargetId = item.id"
                >
                  Delete
                </UButton>
              </div>
            </div>
          </UCard>
        </template>
      </DataTable>

      <div
        v-else
        class="flex-1 flex items-center justify-center text-muted"
      >
        <p>{{ providers.length === 0 ? 'Add a provider above to configure its models.' : 'Select a provider above to view its models.' }}</p>
      </div>
    </div>

    <!-- Add/Edit Provider Modal -->
    <ProviderModal
      v-model:open="showProviderModal"
      :provider="editingProvider"
      :fallback-providers="providers"
      :loading="providerModalLoading"
      :error="providerModalError"
      @submit="handleProviderModalSubmit"
      @close="showProviderModal = false"
    />

    <!-- Disable/Enable Provider Confirm Modal -->
    <AppModal
      :open="!!confirmTargetId"
      title="Confirm Action"
      width="sm:max-w-sm"
      @update:open="confirmTargetId = null"
    >
      <template #body>
        <p class="p-4">
          {{ confirmCurrentEnabled ? 'Disable' : 'Enable' }} this provider?
        </p>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            variant="outline"
            @click="confirmTargetId = null"
          >
            Cancel
          </UButton>
          <UButton
            :color="confirmCurrentEnabled ? 'warning' : 'success'"
            @click="confirmToggleDisable"
          >
            {{ confirmCurrentEnabled ? 'Disable' : 'Enable' }}
          </UButton>
        </div>
      </template>
    </AppModal>

    <!-- Hard Delete Provider Confirm Modal -->
    <AppModal
      :open="!!deleteProviderTarget"
      title="Delete Provider Permanently"
      width="sm:max-w-sm"
      @update:open="deleteProviderTarget = null"
    >
      <template #body>
        <div class="p-4 space-y-2">
          <p>
            Permanently delete <strong>{{ deleteProviderTarget?.name }}</strong>? This removes the provider and all of its
            configured models. This cannot be undone.
          </p>
          <p class="text-xs text-muted">
            Past usage records stay in Usage history — only the provider and model configuration are removed.
          </p>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            variant="outline"
            @click="deleteProviderTarget = null"
          >
            Cancel
          </UButton>
          <UButton
            color="error"
            :loading="deletingProvider"
            @click="confirmDeleteProvider"
          >
            Delete Permanently
          </UButton>
        </div>
      </template>
    </AppModal>

    <!-- Add/Edit Model Modal -->
    <AppModal
      v-model:open="showModal"
      :title="editingModel ? 'Edit Model' : 'Add Model'"
      :loading="modalLoading"
      :error="modalError"
      width="sm:max-w-lg"
      @close="showModal = false"
    >
      <template #body>
        <form
          id="model-form"
          ref="modelFormRef"
          class="space-y-4 p-4"
          @submit.prevent="handleModalSubmit"
        >
          <UFormField
            label="Model ID"
            :required="!editingModel"
          >
            <UInput
              v-model="formModelId"
              :placeholder="editingModel ? undefined : 'gpt-4o'"
              :disabled="!!editingModel"
              class="w-full"
            />
          </UFormField>

          <UFormField label="Display Name">
            <UInput
              v-model="formName"
              placeholder="GPT-4o"
              class="w-full"
            />
          </UFormField>

          <UFormField label="Tier">
            <USelect
              v-model="formTier"
              :items="MODEL_TIERS"
              class="w-full"
            />
          </UFormField>

          <div class="grid grid-cols-2 gap-4">
            <UFormField
              label="Price per 1M Tokens ($)"
              description="What providers quote — stored internally as $/token"
            >
              <UInput
                v-model.number="formPricePerMillionTokens"
                type="number"
                step="0.01"
                min="0"
                placeholder="0.0"
                class="w-full"
              />
            </UFormField>

            <UFormField label="Max Tokens">
              <UInput
                v-model.number="formMaxTokens"
                type="number"
                min="0"
                placeholder="128000"
                class="w-full"
              />
            </UFormField>
          </div>

          <UCheckbox
            v-model="formEnabled"
            label="Enabled"
          />

          <UCheckbox
            v-model="formSupportsReasoning"
            label="Supports reasoning effort"
            help="Shows the Low/Medium/High effort picker in chat when this model is selected"
          />

          <UFormField
            v-if="isOllamaProvider"
            label="Thinking mode"
            help="Some local thinking-capable models burn their whole token budget on hidden reasoning and return empty replies for short tasks like chat titles unless forced off. Set this once you've seen it happen for a specific model."
          >
            <USelect
              v-model="formThinkMode"
              :items="OLLAMA_THINK_MODES"
              value-key="value"
              class="w-full"
            />
          </UFormField>
        </form>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            variant="outline"
            @click="showModal = false"
          >
            Cancel
          </UButton>
          <UButton
            :loading="modalLoading"
            type="submit"
            form="model-form"
          >
            {{ editingModel ? 'Save' : 'Create' }}
          </UButton>
        </div>
      </template>
    </AppModal>

    <!-- Discover (probe) Models Modal -->
    <AppModal
      v-model:open="showProbeModal"
      title="Discovered Models"
      width="sm:max-w-3xl"
      :loading="probeLoading"
      :error="probeError"
      @close="showProbeModal = false"
    >
      <template #body>
        <div
          v-if="!probeLoading && !probeError && probeResults.length === 0"
          class="p-4 text-center text-muted"
        >
          No models discovered.
        </div>
        <div
          v-else-if="!probeLoading && !probeError"
          class="flex flex-col gap-3 p-4"
        >
          <UInput
            v-model="probeFilter"
            icon="i-lucide-search"
            placeholder="Filter by name or model ID..."
            class="w-full"
          />
          <ClientDataTable
            :data="filteredProbeResults"
            :columns="probeColumns"
            :row-key="(m: ProbedModelDto) => m.modelId"
            :default-page-size="10"
          >
            <template #modelId-cell="{ row }">
              <code class="text-xs truncate block max-w-56">{{ row.original.modelId }}</code>
            </template>
            <template #name-cell="{ row }">
              <span class="truncate block max-w-40">{{ row.original.name || row.original.modelId }}</span>
            </template>
            <template #actions-cell="{ row }">
              <div class="flex justify-end gap-1">
                <template v-if="configuredModelIds.has(row.original.modelId)">
                  <UBadge
                    color="neutral"
                    variant="subtle"
                  >
                    Already added
                  </UBadge>
                  <UButton
                    v-if="discoveredPrice(row.original) !== null && !models.find(m => m.modelId === row.original.modelId)?.pricePerToken"
                    size="xs"
                    color="primary"
                    variant="subtle"
                    title="Discovered price not yet applied to this model's config"
                    @click="applyDiscoveredPrice(row.original)"
                  >
                    Apply price
                  </UButton>
                </template>
                <UButton
                  v-else
                  size="xs"
                  color="neutral"
                  @click="addProbedModel(row.original)"
                >
                  Add
                </UButton>
              </div>
            </template>
          </ClientDataTable>
        </div>
      </template>
      <template #footer>
        <UButton
          variant="outline"
          @click="showProbeModal = false"
        >
          Close
        </UButton>
      </template>
    </AppModal>

    <!-- Delete Model Confirm Modal -->
    <AppModal
      :open="!!deleteTargetId"
      title="Delete Model"
      width="sm:max-w-sm"
      @update:open="deleteTargetId = null"
    >
      <template #body>
        <p class="p-4">
          Are you sure you want to delete this model? This cannot be undone.
        </p>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            variant="outline"
            @click="deleteTargetId = null"
          >
            Cancel
          </UButton>
          <UButton
            color="error"
            @click="deleteModel(deleteTargetId!)"
          >
            Delete
          </UButton>
        </div>
      </template>
    </AppModal>
  </div>
</template>
