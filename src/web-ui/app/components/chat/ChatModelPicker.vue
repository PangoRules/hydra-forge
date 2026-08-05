<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { AvailableModelDto } from '~/types/chat'

const props = withDefaults(
  defineProps<{
    feature?: string
    disabled?: boolean
    initialModelId?: string | null
    initialEffort?: string | null
  }>(),
  {
    feature: 'PersonalChat',
    disabled: false,
    initialModelId: null,
    initialEffort: null
  }
)

const modelId = defineModel<string | null>({ default: null })
const effort = defineModel<string | null>('effort', { default: null })

const EFFORT_LEVELS = [
  { value: 'low', label: 'Low' },
  { value: 'medium', label: 'Medium' },
  { value: 'high', label: 'High' }
]

const api = useApi()
const models = ref<AvailableModelDto[]>([])
const loading = ref(true)
const open = ref(false)

const storageKey = computed(() => `hydraforge:chat:preferredModel:${props.feature}`)
const effortStorageKey = computed(() => `hydraforge:chat:preferredEffort:${props.feature}`)

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

function applyEffortForModel(id: string) {
  const model = models.value.find(m => m.providerModelConfigId === id)
  if (model?.supportsReasoning) {
    const saved = import.meta.client ? localStorage.getItem(effortStorageKey.value) : null
    effort.value = saved ?? 'medium'
  } else {
    effort.value = null
  }
}

function selectEffort(value: string) {
  effort.value = value
  if (import.meta.client) {
    localStorage.setItem(effortStorageKey.value, value)
  }
}

function selectModel(id: string) {
  modelId.value = id
  if (import.meta.client) {
    localStorage.setItem(storageKey.value, id)
  }
  applyEffortForModel(id)
  open.value = false
}

async function fetchModels() {
  loading.value = true
  try {
    const { data } = await api.GET<AvailableModelDto[]>(ApiRoutes.Llm.models(props.feature))
    models.value = data ?? []
    if (models.value.length === 0) return

    // Prefer external initialModelId over localStorage
    if (props.initialModelId && models.value.some(m => m.providerModelConfigId === props.initialModelId)) {
      modelId.value = props.initialModelId
      if (props.initialEffort) effort.value = props.initialEffort
    } else {
      const saved = import.meta.client ? localStorage.getItem(storageKey.value) : null
      const savedIsValid = !!saved && models.value.some(m => m.providerModelConfigId === saved)
      modelId.value = savedIsValid ? saved! : models.value[0]!.providerModelConfigId
      applyEffortForModel(modelId.value)
    }
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
      <div
        v-if="selectedModel?.supportsReasoning"
        data-testid="reasoning-effort-row"
        class="flex items-center gap-1 border-t border-gray-200 dark:border-gray-700 px-3 py-2"
      >
        <span class="text-xs text-muted mr-1">Effort:</span>
        <UButton
          v-for="level in EFFORT_LEVELS"
          :key="level.value"
          :data-testid="`effort-option-${level.value}`"
          size="xs"
          :variant="effort === level.value ? 'solid' : 'ghost'"
          :color="effort === level.value ? 'primary' : 'neutral'"
          @click="selectEffort(level.value)"
        >
          {{ level.label }}
        </UButton>
      </div>
    </template>
  </UPopover>
</template>
