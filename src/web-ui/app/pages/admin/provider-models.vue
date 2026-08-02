<script setup lang="ts">
import { ApiRoutes, UiRoutes } from '~/lib/routes'
import DataTable from '~/components/shared/DataTable.vue'
import AppModal from '~/components/shared/AppModal.vue'

definePageMeta({ middleware: ['auth'] })

const user = useAuthStore().user
if (!user?.isAdmin) {
  navigateTo(UiRoutes.Chats)
}

interface ProviderDto {
  id: string
  name: string
  adapterType: string
  providerType: string
  tier: string
  isEnabled: boolean
}

interface ProviderModelDto {
  modelId: string
  name: string
  description: string | null
  metadata: Record<string, unknown> | null
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
}

interface CreateModelInput {
  modelId: string
  name: string
  tier: string
  pricePerToken: number | null
  maxTokens: number | null
  isEnabled: boolean
}

interface UpdateModelInput {
  name?: string | null
  tier?: string | null
  pricePerToken?: number | null
  maxTokens?: number | null
  isEnabled?: boolean
}

const MODEL_TIERS = [
  { label: 'Economy', value: 'Economy' },
  { label: 'Standard', value: 'Standard' },
  { label: 'Premium', value: 'Premium' }
]

const api = useApi()
const toast = useAppToast()

const allProviders = ref<ProviderDto[]>([])
const selectedProviderId = ref<string | undefined>(undefined)
const models = ref<ProviderModelDto[]>([])
const totalCount = ref(0)
const loading = ref(false)
const page = ref(1)
const pageSize = ref(20)

// Modal state
const showModal = ref(false)
const editingModel = ref<ProviderModelConfigDto | null>(null)
const modalLoading = ref(false)
const modalError = ref<string | null>(null)

// Form fields
const formModelId = ref('')
const formName = ref('')
const formTier = ref('Standard')
const formPricePerToken = ref<number | null>(null)
const formMaxTokens = ref<number | null>(null)
const formEnabled = ref(true)

// Delete confirm
const deleteTargetId = ref<string | null>(null)

// Form ref for native validation
const modelFormRef = ref<HTMLFormElement | null>(null)

const columns = [
  { accessorKey: 'modelId', header: 'Model ID' },
  { accessorKey: 'name', header: 'Display Name' },
  { accessorKey: 'description', header: 'Description' },
  { accessorKey: 'actions', header: 'Actions', enableSorting: false }
]

async function loadProviders() {
  try {
    const { data, error } = await api.GET<{ items: ProviderDto[], totalCount: number }>(
      ApiRoutes.Admin.providers.list() + '?take=200'
    )
    if (error) throw error
    allProviders.value = data?.items ?? []
  } catch (e) {
    toast.error((e as Error).message || 'Failed to load providers')
  }
}

async function loadModels() {
  if (!selectedProviderId.value) {
    models.value = []
    totalCount.value = 0
    return
  }
  loading.value = true
  try {
    const { data, error } = await api.GET<ProviderModelDto[]>(
      ApiRoutes.Admin.providers.probeModels(selectedProviderId.value)
    )
    if (error) throw error
    models.value = data ?? []
    totalCount.value = models.value.length
  } catch (e) {
    toast.error((e as Error).message || 'Failed to load models')
  } finally {
    loading.value = false
  }
}

function onProviderChange() {
  page.value = 1
  loadModels()
}

function openAddModal() {
  editingModel.value = null
  resetForm()
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
        isEnabled: formEnabled.value
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
        isEnabled: formEnabled.value
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

onMounted(() => loadProviders())
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <div class="shrink-0 px-6 pt-6 space-y-4">
      <div class="flex items-center justify-between">
        <h1 class="text-2xl font-bold">
          Provider Models
        </h1>
        <UButton
          label="Add Model"
          :disabled="!selectedProviderId"
          @click="openAddModal"
        />
      </div>

      <div class="max-w-xs">
        <USelect
          v-model="selectedProviderId"
          :items="allProviders.map(p => ({ label: p.name, value: p.id }))"
          placeholder="Select a provider..."
          @update:model-value="onProviderChange"
        />
      </div>
    </div>

    <div class="flex-1 min-h-0 px-6 pb-6 pt-4">
      <DataTable
        v-if="selectedProviderId"
        :data="models"
        :columns="columns"
        :loading="loading"
        :page="page"
        :page-size="pageSize"
        :total-count="totalCount"
        :row-key="(item: ProviderModelDto) => item.modelId"
        fill-height
        @update:page="page = $event"
        @update:page-size="pageSize = $event"
      >
        <template #actions-cell>
          <span class="text-xs text-muted">Not configured</span>
        </template>

        <template #card="{ item }">
          <UCard>
            <div class="space-y-2">
              <p class="font-medium text-sm">
                {{ item.name || item.modelId }}
              </p>
              <p class="text-xs text-muted font-mono">
                {{ item.modelId }}
              </p>
              <div class="flex gap-1">
                <span class="text-xs text-muted">Not configured</span>
              </div>
            </div>
          </UCard>
        </template>
      </DataTable>

      <div
        v-else
        class="flex-1 flex items-center justify-center text-muted"
      >
        <p>Select a provider to view its models.</p>
      </div>
    </div>

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
            <UFormField label="Price per Token ($)">
              <UInput
                v-model.number="formPricePerToken"
                type="number"
                step="0.000001"
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

    <!-- Delete Confirm Modal -->
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
