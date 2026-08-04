<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { AvailableModelDto } from '~/types/chat'

const props = withDefaults(
  defineProps<{
    feature?: string
    disabled?: boolean
  }>(),
  {
    feature: 'PersonalChat',
    disabled: false
  }
)

const modelId = defineModel<string | null>({ default: null })

const api = useApi()
const models = ref<AvailableModelDto[]>([])
const loading = ref(true)
const open = ref(false)

const storageKey = computed(() => `hydraforge:chat:preferredModel:${props.feature}`)

const selectedModel = computed(
  () => models.value.find(m => m.providerModelConfigId === modelId.value) ?? null
)

// Grouped by provider so the list stays scannable as the model catalog grows —
// UCommandPalette gives us type-to-filter + keyboard nav across groups for free.
const groups = computed(() => {
  const byProvider = new Map<string, AvailableModelDto[]>()
  for (const m of models.value) {
    if (!byProvider.has(m.providerName)) byProvider.set(m.providerName, [])
    byProvider.get(m.providerName)!.push(m)
  }
  return [...byProvider.entries()].map(([providerName, providerModels]) => ({
    id: providerName,
    label: providerName,
    items: providerModels.map(m => ({
      label: m.modelName,
      active: m.providerModelConfigId === modelId.value,
      onSelect: () => selectModel(m.providerModelConfigId)
    }))
  }))
})

function selectModel(id: string) {
  modelId.value = id
  if (import.meta.client) {
    localStorage.setItem(storageKey.value, id)
  }
  open.value = false
}

async function fetchModels() {
  loading.value = true
  try {
    const { data } = await api.GET<AvailableModelDto[]>(ApiRoutes.Llm.models(props.feature))
    models.value = data ?? []
    if (models.value.length === 0) return

    const saved = import.meta.client ? localStorage.getItem(storageKey.value) : null
    const savedIsValid = !!saved && models.value.some(m => m.providerModelConfigId === saved)
    modelId.value = savedIsValid ? saved! : models.value[0]!.providerModelConfigId
  } catch {
    // No models configured/reachable — chat falls back to server-side tier routing
  } finally {
    loading.value = false
  }
}

onMounted(fetchModels)
</script>

<template>
  <UPopover
    v-if="!loading && models.length > 0"
    v-model:open="open"
    :content="{ align: 'start' }"
  >
    <UButton
      color="neutral"
      variant="outline"
      size="sm"
      trailing-icon="i-lucide-chevron-down"
      :disabled="disabled"
      class="shrink-0 max-w-48"
    >
      <span class="truncate font-medium">{{ selectedModel?.modelName ?? 'Select model' }}</span>
    </UButton>

    <template #content>
      <UCommandPalette
        :groups="groups"
        placeholder="Type to filter models..."
        class="w-80 max-h-96"
        :ui="{ input: 'text-sm' }"
      />
    </template>
  </UPopover>
</template>
