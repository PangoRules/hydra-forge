<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  cardId: string
  projectId: string
}>()

const emit = defineEmits<{
  close: []
}>()

const isOpen = ref(true)
const card = ref<CardResponse | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

const api = useApi()

async function fetchCard() {
  loading.value = true
  try {
    const { data, error: apiError } = await api.GET(ApiRoutes.Cards.detail(props.projectId, props.cardId))
    if (apiError) throw apiError
    card.value = data as CardResponse
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load card'
  } finally {
    loading.value = false
  }
}

onMounted(() => fetchCard())

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') emit('close')
}
</script>

<template>
  <UModal
    v-model:open="isOpen"
    class="sm:max-w-4xl"
    @close="emit('close')"
  >
    <div
      class="flex flex-col max-h-[85vh]"
      @keydown="onKeydown"
    >
      <div
        v-if="loading"
        class="flex items-center justify-center p-8"
      >
        <UIcon
          name="i-lucide-loader"
          class="animate-spin size-8"
        />
      </div>

      <UAlert
        v-else-if="error"
        color="error"
        :title="error"
      />

      <template v-else-if="card">
        <div class="flex items-center justify-between p-4 border-b">
          <h2 class="text-lg font-semibold truncate">
            {{ card.title }}
          </h2>
          <UButton
            icon="i-lucide-x"
            variant="ghost"
            size="sm"
            @click="emit('close')"
          />
        </div>

        <div class="hidden md:flex flex-1 overflow-hidden">
          <div class="flex-1 overflow-y-auto p-4 space-y-6">
            <CardDescription
              :card="card"
              :project-id="projectId"
            />
          </div>

          <div class="w-64 flex-shrink-0 border-l overflow-y-auto p-4">
            <CardMetadata :card="card" />
          </div>
        </div>

        <div class="md:hidden p-4">
          <p class="text-sm text-muted">
            Mobile card view coming in Task 10
          </p>
        </div>
      </template>
    </div>
  </UModal>
</template>
