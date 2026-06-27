<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'

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
const showArchiveConfirm = ref(false)

const activeTab = ref<'details' | 'checklist' | 'comments' | 'related'>('details')

const tabs = [
  { label: 'Details', value: 'details' as const },
  { label: 'Checklist', value: 'checklist' as const },
  { label: 'Comments', value: 'comments' as const },
  { label: 'Related', value: 'related' as const }
]

const api = useApi()

/** Close with animation: set isOpen=false (triggers UModal scale-out 200ms), then emit close */
function closeWithAnimation() {
  if (!isOpen.value) return
  isOpen.value = false
  setTimeout(() => emit('close'), 200)
}

function onClose() {
  closeWithAnimation()
}

function handleOpenChange(val: boolean) {
  if (!val) {
    // UModal requesting close — always trigger animation if not already closing
    if (isOpen.value) {
      closeWithAnimation()
    }
  } else {
    isOpen.value = val
  }
}

function handleArchive() {
  showArchiveConfirm.value = true
}

async function confirmArchive() {
  try {
    await api.POST(ApiRoutes.Cards.archive(props.projectId, card.value!.id), {
      body: { version: card.value!.version }
    })
    toast.add({ title: 'Card archived', color: 'success', duration: 4000 })
    emit('archived')
    closeWithAnimation()
  } catch {
    toast.add({ title: 'Failed to archive card', color: 'error' })
  }
}

async function handleRestore() {
  try {
    await api.POST(ApiRoutes.Cards.restore(props.projectId, card.value!.id), {
      body: { version: card.value!.version }
    })
    toast.add({ title: 'Card restored', color: 'success', duration: 4000 })
    emit('restored')
    closeWithAnimation()
  } catch {
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

function applyCardUpdate(updated: CardResponse) {
  card.value = updated
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
    @update:open="handleOpenChange"
    @close="onClose"
  >
    <template #header-trailing>
      <div class="flex items-center gap-1">
        <UButton
          v-if="card && !isArchived"
          variant="ghost"
          size="sm"
          icon="i-lucide-archive"
          title="Archive card"
          @click="handleArchive"
        />
        <UButton
          v-else-if="card && isArchived"
          variant="ghost"
          size="sm"
          icon="i-lucide-archive-restore"
          title="Restore card"
          @click="handleRestore"
        />
      </div>
    </template>

    <template #body>
      <template v-if="card">
        <!-- Desktop: two-column with tabs in left pane -->
        <div class="hidden md:flex max-h-[70vh] overflow-hidden">
          <div class="flex-1 flex flex-col overflow-hidden">
            <UTabs
              v-model="activeTab"
              :items="tabs"
              class="border-b flex-shrink-0 px-3"
            />
            <div class="flex-1 overflow-y-auto p-4">
              <div v-if="activeTab === 'details'">
                <CardDescription
                  :card="card"
                  :project-id="projectId"
                  :is-archived="isArchived"
                  @update:card="applyCardUpdate"
                />
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
              <div v-else-if="activeTab === 'related'">
                <div class="space-y-4">
                  <p class="text-sm text-muted">
                    Attachments, dependencies, specs, plans coming soon
                  </p>
                </div>
              </div>
            </div>
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
            class="border-b px-3"
          />

          <div class="flex-1 overflow-y-auto p-4">
            <div
              v-if="activeTab === 'details'"
              class="space-y-4"
            >
              <CardDescription
                :card="card"
                :project-id="projectId"
                :is-archived="isArchived"
                @update:card="applyCardUpdate"
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

  <ConfirmDialog
    v-model:open="showArchiveConfirm"
    title="Archive card"
    :message="card ? `Archive #${card.cardNumber} ${card.title}?` : ''"
    confirm-text="Archive"
    @confirm="confirmArchive"
  />
</template>
