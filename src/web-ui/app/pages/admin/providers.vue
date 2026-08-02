<script setup lang="ts">
import { ApiRoutes, UiRoutes } from '~/lib/routes'
import DataTable from '~/components/shared/DataTable.vue'
import AppModal from '~/components/shared/AppModal.vue'
import ProviderModal, { type ProviderFormData, type ProviderDto } from '~/components/admin/ProviderModal.vue'

definePageMeta({ middleware: ['auth'] })

const user = useAuthStore().user
if (!user?.isAdmin) {
  navigateTo(UiRoutes.Chats)
}

interface ProviderPageDto {
  items: ProviderDto[]
  totalCount: number
}

interface ProviderModelDto {
  modelId: string
  name: string
  description: string | null
  metadata: Record<string, string> | null
}

const api = useApi()
const toast = useAppToast()

const providers = ref<ProviderDto[]>([])
const totalCount = ref(0)
const loading = ref(false)
const page = ref(1)
const pageSize = ref(20)

// Modal state
const showModal = ref(false)
const editingProvider = ref<ProviderDto | null>(null)
const modalLoading = ref(false)
const modalError = ref<string | null>(null)

// Probe modal state
const showProbeModal = ref(false)
const probeResults = ref<ProviderModelDto[]>([])
const probeLoading = ref(false)
const probeError = ref<string | null>(null)

// Disable/enable confirm state
const confirmTargetId = ref<string | null>(null)
const confirmCurrentEnabled = ref(false)

const columns = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'adapterType', header: 'Adapter' },
  { accessorKey: 'providerType', header: 'Type' },
  { accessorKey: 'tier', header: 'Tier' },
  { accessorKey: 'isEnabled', header: 'Status' },
  { accessorKey: 'actions', header: 'Actions', enableSorting: false }
]

async function loadProviders() {
  loading.value = true
  try {
    const skip = (page.value - 1) * pageSize.value
    const { data, error } = await api.GET<ProviderPageDto>(
      `${ApiRoutes.Admin.providers.list()}?skip=${skip}&take=${pageSize.value}`
    )
    if (error) throw error
    providers.value = data!.items
    totalCount.value = data!.totalCount
  } catch (e) {
    toast.error((e as Error).message || 'Failed to load providers')
  } finally {
    loading.value = false
  }
}

function openAddModal() {
  editingProvider.value = null
  modalError.value = null
  showModal.value = true
}

function openEditModal(provider: ProviderDto) {
  editingProvider.value = provider
  modalError.value = null
  showModal.value = true
}

async function handleModalSubmit(data: ProviderFormData) {
  modalLoading.value = true
  modalError.value = null
  try {
    if (editingProvider.value) {
      const body: Record<string, unknown> = {
        name: data.name,
        baseUrl: data.baseUrl,
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
    showModal.value = false
    await loadProviders()
  } catch (e) {
    modalError.value = (e as Error).message || 'Operation failed'
  } finally {
    modalLoading.value = false
  }
}

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

async function probeModels(providerId: string) {
  probeLoading.value = true
  probeError.value = null
  probeResults.value = []
  showProbeModal.value = true
  try {
    const { data, error } = await api.GET<ProviderModelDto[]>(
      ApiRoutes.Admin.providers.probeModels(providerId)
    )
    if (error) throw error
    probeResults.value = data ?? []
  } catch (e) {
    probeError.value = (e as Error).message || 'Probe failed'
  } finally {
    probeLoading.value = false
  }
}

watch([page, pageSize], () => loadProviders())

onMounted(() => loadProviders())
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <div class="shrink-0 px-6 pt-6 space-y-4">
      <div class="flex items-center justify-between">
        <h1 class="text-2xl font-bold">
          LLM Providers
        </h1>
        <UButton
          label="Add Provider"
          @click="openAddModal"
        />
      </div>
    </div>

    <div class="flex-1 min-h-0 px-6 pb-6 pt-4">
      <DataTable
        :data="providers"
        :columns="columns"
        :loading="loading"
        :page="page"
        :page-size="pageSize"
        :total-count="totalCount"
        :row-key="(item: ProviderDto) => item.id"
        fill-height
        @update:page="page = $event"
        @update:page-size="pageSize = $event"
      >
        <template #name-cell="{ row }">
          <span class="font-medium">{{ row.original.name }}</span>
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
              color="neutral"
              @click="openEditModal(row.original)"
            >
              Edit
            </UButton>
            <UButton
              size="xs"
              color="neutral"
              @click="probeModels(row.original.id)"
            >
              Probe
            </UButton>
            <UButton
              v-if="row.original.isEnabled"
              size="xs"
              color="error"
              @click="showDisableConfirm(row.original)"
            >
              Disable
            </UButton>
            <UButton
              v-else
              size="xs"
              color="success"
              @click="showDisableConfirm(row.original)"
            >
              Enable
            </UButton>
          </div>
        </template>

        <template #card="{ item }">
          <UCard>
            <div class="space-y-2">
              <div class="flex items-center justify-between">
                <span class="font-medium">{{ item.name }}</span>
                <UBadge :color="item.isEnabled ? 'success' : 'error'">
                  {{ item.isEnabled ? 'Enabled' : 'Disabled' }}
                </UBadge>
              </div>
              <p class="text-xs text-muted">
                {{ item.adapterType }} · {{ item.providerType }} · {{ item.tier }}
              </p>
              <div class="flex gap-1">
                <UButton
                  size="xs"
                  color="neutral"
                  @click="openEditModal(item)"
                >
                  Edit
                </UButton>
                <UButton
                  size="xs"
                  color="neutral"
                  @click="probeModels(item.id)"
                >
                  Probe
                </UButton>
                <UButton
                  v-if="item.isEnabled"
                  size="xs"
                  color="error"
                  @click="showDisableConfirm(item)"
                >
                  Disable
                </UButton>
                <UButton
                  v-else
                  size="xs"
                  color="success"
                  @click="showDisableConfirm(item)"
                >
                  Enable
                </UButton>
              </div>
            </div>
          </UCard>
        </template>
      </DataTable>
    </div>

    <!-- Add/Edit Modal -->
    <ProviderModal
      v-model:open="showModal"
      :provider="editingProvider"
      :fallback-providers="providers"
      :loading="modalLoading"
      :error="modalError"
      @submit="handleModalSubmit"
      @close="showModal = false"
    />

    <!-- Probe Results Modal -->
    <AppModal
      v-model:open="showProbeModal"
      title="Discovered Models"
      width="sm:max-w-xl"
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
        <ul
          v-else-if="!probeLoading && !probeError"
          class="divide-y divide-muted max-h-96 overflow-y-auto"
        >
          <li
            v-for="model in probeResults"
            :key="model.modelId"
            class="p-3"
          >
            <p class="font-medium text-sm">
              {{ model.name || model.modelId }}
            </p>
            <p
              v-if="model.description"
              class="text-xs text-muted mt-1"
            >
              {{ model.description }}
            </p>
            <code class="text-xs text-muted">{{ model.modelId }}</code>
          </li>
        </ul>
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

    <!-- Disable/Enable Confirm Modal -->
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
            :color="confirmCurrentEnabled ? 'error' : 'success'"
            @click="confirmToggleDisable"
          >
            {{ confirmCurrentEnabled ? 'Disable' : 'Enable' }}
          </UButton>
        </div>
      </template>
    </AppModal>
  </div>
</template>
