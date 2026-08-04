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

const storageKey = computed(() => `hydraforge:chat:preferredModel:${props.feature}`)

const selectedModel = computed(
  () => models.value.find(m => m.providerModelConfigId === modelId.value) ?? null
)

const menuItems = computed(() => [
  models.value.map(m => ({
    label: m.modelName,
    description: m.providerName,
    icon: m.providerModelConfigId === modelId.value ? 'i-lucide-check' : undefined,
    onSelect: () => selectModel(m.providerModelConfigId)
  }))
])

function selectModel(id: string) {
  modelId.value = id
  if (import.meta.client) {
    localStorage.setItem(storageKey.value, id)
  }
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
  <UDropdownMenu
    v-if="!loading && models.length > 0"
    :items="menuItems"
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
  </UDropdownMenu>
</template>
