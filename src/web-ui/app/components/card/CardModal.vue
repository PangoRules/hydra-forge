<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  cardId: string
  projectId: string
}>()

const emit = defineEmits<{
  close: []
  archived: []
  restored: []
}>()

const isOpen = ref(true)
const card = ref<CardResponse | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

const isArchived = computed(() => !!card.value?.archivedAt)
const toast = useToast()

const activeTab = ref<'details' | 'checklist' | 'comments' | 'related'>('details')

const tabs = [
  { label: 'Details', value: 'details' as const },
  { label: 'Checklist', value: 'checklist' as const },
  { label: 'Comments', value: 'comments' as const },
  { label: 'Related', value: 'related' as const }
]

const api = useApi()

function onClose() {
  emit('close')
}

async function handleArchive() {
  const { error: apiError } = await api.POST(ApiRoutes.Cards.archive(props.projectId, card.value!.id), {
    body: { version: card.value!.version }
  })
  if (!apiError) {
    emit('archived')
    emit('close')
  } else {
    toast.add({ title: 'Failed to archive card', color: 'error' })
  }
}

async function handleRestore() {
  const { error: apiError } = await api.POST(ApiRoutes.Cards.restore(props.projectId, card.value!.id), {})
  if (!apiError) {
    emit('restored')
  } else {
    toast.add({ title: 'Failed to restore card', color: 'error' })
  }
}

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
</script>

<template>
  <AppModal
    :open="isOpen"
    :title="card?.title"
    :loading="loading"
    :error="error"
    width="sm:max-w-4xl"
    :show-close="!!card"
    @update:open="isOpen = $event"
    @close="onClose"
  >
    <template #header>
      <div class="flex items-center gap-2">
        <UButton
          v-if="card && !isArchived"
          variant="ghost"
          size="sm"
          icon="i-lucide-archive"
          @click="handleArchive"
        >
          Archive
        </UButton>
        <UButton
          v-else-if="card && isArchived"
          variant="ghost"
          size="sm"
          icon="i-lucide-archive-restore"
          @click="handleRestore"
        >
          Restore
        </UButton>
      </div>
    </template>

    <template #body>
      <template v-if="card">
        <!-- Desktop: two-column -->
        <div class="hidden md:flex max-h-[70vh] overflow-hidden">
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

        <!-- Mobile: tabbed -->
        <div class="md:hidden flex flex-col max-h-[70vh] overflow-hidden">
          <UTabs
            v-model="activeTab"
            :items="tabs"
            class="border-b"
          />

          <div class="flex-1 overflow-y-auto p-4">
            <div
              v-if="activeTab === 'details'"
              class="space-y-4"
            >
              <CardDescription
                :card="card"
                :project-id="projectId"
              />
              <CardMetadata :card="card" />
            </div>

            <div v-else-if="activeTab === 'checklist'">
              <p class="text-sm text-muted">
                Checklist coming soon
              </p>
            </div>

            <div v-else-if="activeTab === 'comments'">
              <p class="text-sm text-muted">
                Comments coming soon
              </p>
            </div>

            <div
              v-else-if="activeTab === 'related'"
              class="space-y-4"
            >
              <p class="text-sm text-muted">
                Attachments, dependencies, specs, plans coming soon
              </p>
            </div>
          </div>
        </div>
      </template>
    </template>
  </AppModal>
</template>
