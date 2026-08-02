<script setup lang="ts">
import AppModal from '~/components/shared/AppModal.vue'

export interface ProviderFormData {
  name: string
  baseUrl: string
  adapterType: string
  providerType: string
  tier: string
  fallbackProviderId: string | null
  apiKey: string
}

export interface ProviderDto {
  id: string
  name: string
  baseUrl: string
  adapterType: string
  providerType: string
  tier: string
  fallbackProviderId: string | null
  isEnabled: boolean
  createdAt: string
  updatedAt: string
}

const ADAPTER_TYPES = [
  { label: 'OpenAI Compatible', value: 'OpenAiCompatible' },
  { label: 'Anthropic', value: 'Anthropic' },
  { label: 'Ollama', value: 'Ollama' },
  { label: 'Diffusers', value: 'Diffusers' },
  { label: 'ComfyUI', value: 'ComfyUi' },
  { label: 'DallE', value: 'DallE' },
  { label: 'StabilityAI', value: 'StabilityAi' }
]

const PROVIDER_TYPES = [
  { label: 'Text', value: 'Text' },
  { label: 'Image', value: 'Image' },
  { label: 'Both', value: 'Both' }
]

const MODEL_TIERS = [
  { label: 'Economy', value: 'Economy' },
  { label: 'Standard', value: 'Standard' },
  { label: 'Premium', value: 'Premium' }
]

const props = defineProps<{
  open: boolean
  provider?: ProviderDto | null
  fallbackProviders?: ProviderDto[]
  loading?: boolean
  error?: string | null
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'close': []
  'submit': [data: ProviderFormData]
}>()

const name = ref('')
const baseUrl = ref('')
const adapterType = ref('OpenAiCompatible')
const providerType = ref('Text')
const tier = ref('Standard')
const fallbackProviderId = ref<string | null>(null)
const apiKey = ref('')
const localError = ref<string | null>(null)

const isEdit = computed(() => !!props.provider)
const title = computed(() => isEdit.value ? 'Edit Provider' : 'Add Provider')

watch(() => props.open, (val) => {
  if (val) {
    if (props.provider) {
      name.value = props.provider.name
      baseUrl.value = props.provider.baseUrl
      adapterType.value = ADAPTER_TYPES.some(a => a.value === props.provider!.adapterType)
        ? props.provider.adapterType
        : ADAPTER_TYPES[0]!.value
      providerType.value = props.provider.providerType
      tier.value = props.provider.tier
      fallbackProviderId.value = props.provider.fallbackProviderId ?? null
      apiKey.value = ''
    } else {
      resetForm()
    }
  }
})

function resetForm() {
  name.value = ''
  baseUrl.value = ''
  adapterType.value = 'OpenAiCompatible'
  providerType.value = 'Text'
  tier.value = 'Standard'
  fallbackProviderId.value = null
  apiKey.value = ''
  localError.value = null
}

function onClose() {
  emit('update:open', false)
  emit('close')
}

async function handleSubmit() {
  localError.value = null
  const data: ProviderFormData = {
    name: name.value,
    baseUrl: baseUrl.value,
    adapterType: adapterType.value,
    providerType: providerType.value,
    tier: tier.value,
    fallbackProviderId: fallbackProviderId.value,
    apiKey: apiKey.value
  }
  emit('submit', data)
}
</script>

<template>
  <AppModal
    :open="open"
    :title="title"
    :loading="loading"
    :error="error ?? localError"
    width="sm:max-w-lg"
    @update:open="emit('update:open', $event)"
    @close="onClose"
  >
    <template #body>
      <form
        class="space-y-4 p-4"
        @submit.prevent="handleSubmit"
      >
        <UFormField
          label="Name"
          required
        >
          <UInput
            v-model="name"
            placeholder="My Provider"
            required
            class="w-full"
          />
        </UFormField>

        <UFormField
          label="Base URL"
          required
        >
          <UInput
            v-model="baseUrl"
            placeholder="https://api.openai.com/v1"
            required
            class="w-full"
          />
        </UFormField>

        <div class="grid grid-cols-2 gap-4">
          <UFormField
            label="Adapter Type"
            required
          >
            <USelect
              v-model="adapterType"
              :items="ADAPTER_TYPES"
              :disabled="isEdit"
              class="w-full"
            />
          </UFormField>

          <UFormField
            label="Provider Type"
            required
          >
            <USelect
              v-model="providerType"
              :items="PROVIDER_TYPES"
              :disabled="isEdit"
              class="w-full"
            />
          </UFormField>
        </div>

        <div class="grid grid-cols-2 gap-4">
          <UFormField
            label="Tier"
            required
          >
            <USelect
              v-model="tier"
              :items="MODEL_TIERS"
              class="w-full"
            />
          </UFormField>

          <UFormField label="Fallback Provider">
            <USelect
              v-model="fallbackProviderId"
              :items="[{ label: 'None', value: null }, ...(fallbackProviders ?? []).map(p => ({ label: p.name, value: p.id }))]"
              class="w-full"
              placeholder="None"
            />
          </UFormField>
        </div>

        <UFormField :label="isEdit ? 'API Key (leave blank to keep existing)' : 'API Key'">
          <UInput
            v-model="apiKey"
            type="password"
            :placeholder="isEdit ? '••••••••' : 'sk-...'"
            autocomplete="off"
            class="w-full"
          />
        </UFormField>
      </form>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          variant="outline"
          @click="onClose"
        >
          Cancel
        </UButton>
        <UButton
          type="submit"
          :loading="loading"
          @click="handleSubmit"
        >
          {{ isEdit ? 'Save' : 'Create' }}
        </UButton>
      </div>
    </template>
  </AppModal>
</template>
