<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import { ApiError } from '~/lib/api-error'
import AppModal from '~/components/shared/AppModal.vue'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import CardSpec from '~/components/card/CardSpec.vue'
import CardPlan from '~/components/card/CardPlan.vue'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  cardId: string
  projectId: string
  readonly?: boolean
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
const isReadonly = computed(() => props.readonly || isArchived.value)
const toast = useAppToast()
const showArchiveConfirm = ref(false)
const checklistRefresh = ref(0)

const DOCS_CARD_TYPES = ['Goal', 'Idea'] as const
const PLAN_CARD_TYPES = ['Goal'] as const

const hasDocsTab = computed(() =>
  card.value != null && DOCS_CARD_TYPES.includes(card.value.type as unknown as typeof DOCS_CARD_TYPES[number])
)

const hasPlan = computed(() =>
  card.value != null && PLAN_CARD_TYPES.includes(card.value.type as unknown as typeof PLAN_CARD_TYPES[number])
)

const activeTab = ref<'details' | 'checklist' | 'comments' | 'related' | 'docs'>('details')

const tabs = computed(() => [
  { label: 'Details', value: 'details' as const },
  { label: 'Checklist', value: 'checklist' as const },
  { label: 'Comments', value: 'comments' as const },
  { label: 'Related', value: 'related' as const },
  ...(hasDocsTab.value ? [{ label: 'Docs', value: 'docs' as const }] : [])
])

const desktopTabs = computed(() => [
  { label: 'Details', value: 'details' as const },
  { label: 'Checklist', value: 'checklist' as const },
  { label: 'Comments', value: 'comments' as const },
  ...(hasDocsTab.value ? [{ label: 'Docs', value: 'docs' as const }] : [])
])

watch(hasDocsTab, (has) => {
  if (!has && activeTab.value === 'docs') activeTab.value = 'details'
})

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
    toast.success('Card archived')
    emit('archived')
    closeWithAnimation()
  } catch {
    toast.error('Failed to archive card')
  }
}

async function handleRestore() {
  try {
    await api.POST(ApiRoutes.Cards.restore(props.projectId, card.value!.id), {
      body: { version: card.value!.version }
    })
    toast.success('Card restored')
    emit('restored')
    await fetchCard()
  } catch {
    toast.error('Failed to restore card')
  }
}

async function fetchCard() {
  loading.value = true
  try {
    const { data } = await api.GET(ApiRoutes.Cards.detail(props.projectId, props.cardId))
    card.value = data as CardResponse
  } catch (e: unknown) {
    error.value = e instanceof ApiError ? e.message : 'Failed to load card'
  } finally {
    loading.value = false
  }
}

function applyCardUpdate(updated: CardResponse) {
  card.value = updated
}

onMounted(() => {
  fetchCard()
  console.log('[presence] CardModal mounted, cardId:', props.cardId, 'focusedCards:', [...presenceStore.focusedCards.entries()])
})

// Presence indicator
const authStore = useAuthStore()
const presenceStore = usePresenceStore()
const boardStore = useBoardStore()

const currentUserId = computed(() => authStore.user?.userId)

watch(() => [...presenceStore.focusedCards.entries()], (entries) => {
  console.log('[presence] focusedCards for', props.cardId, ':', entries)
  console.log('[presence] otherViewers computed would show:', computeOtherViewers(entries))
})

function computeOtherViewers(entries: [string, string][]) {
  const myId = currentUserId.value
  if (!myId) return []
  const viewers: string[] = []
  for (const [userId, cardId] of entries) {
    if (cardId === props.cardId && userId !== myId) {
      const member = boardStore.members.find(m => m.userId === userId)
      if (member) viewers.push(member.username)
    }
  }
  return viewers
}

const otherViewers = computed(() => {
  const myId = currentUserId.value
  if (!myId) return []
  const viewers: string[] = []
  for (const [userId, cardId] of presenceStore.focusedCards) {
    if (cardId === props.cardId && userId !== myId) {
      const member = boardStore.members.find(m => m.userId === userId)
      if (member) viewers.push(member.username)
    }
  }
  return viewers
})
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
          v-if="card && !props.readonly && !isArchived"
          variant="ghost"
          size="sm"
          icon="i-lucide-archive"
          title="Archive card"
          @click="handleArchive"
        />
        <UButton
          v-if="card && !props.readonly && isArchived"
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
        <!-- Desktop: two-column — UModal handles overflow naturally -->
        <div
          data-testid="card-modal-desktop"
          class="hidden md:flex flex-col"
        >
          <div
            v-if="otherViewers.length > 0"
            class="px-4 py-1.5 text-xs text-muted italic border-b"
          >
            <UIcon
              name="i-lucide-eye"
              class="size-3 inline mr-1"
            />
            {{ otherViewers.join(', ') }} {{ otherViewers.length === 1 ? 'is' : 'are' }} viewing
          </div>
          <UTabs
            v-model="activeTab"
            :items="desktopTabs"
            class="border-b px-3"
          />
          <div class="flex p-4">
            <div class="flex-1 pr-4">
              <div v-if="activeTab === 'details'">
                <CardDescription
                  :card="card"
                  :project-id="projectId"
                  :is-archived="isReadonly"
                  @update:card="applyCardUpdate"
                />
              </div>
              <div v-else-if="activeTab === 'checklist'">
                <CardChecklist
                  :card-id="card.id"
                  :project-id="projectId"
                  :readonly="isReadonly"
                  :refresh-key="checklistRefresh"
                  @updated="checklistRefresh++"
                />
              </div>
              <div v-else-if="activeTab === 'comments'">
                <CardComments
                  :card-id="card.id"
                  :project-id="projectId"
                  :readonly="isReadonly"
                />
              </div>
              <div v-else-if="activeTab === 'related'">
                <div class="space-y-4">
                  <p class="text-sm text-muted">
                    Attachments, dependencies, specs, plans coming soon
                  </p>
                </div>
              </div>
              <div
                v-else-if="activeTab === 'docs'"
                class="space-y-8"
              >
                <CardSpec
                  :card-id="card.id"
                  :project-id="projectId"
                  :readonly="isReadonly"
                />
                <template v-if="hasPlan">
                  <USeparator />
                  <CardPlan
                    :card-id="card.id"
                    :project-id="projectId"
                    :readonly="isReadonly"
                  />
                </template>
              </div>
            </div>

            <div class="w-64 flex-shrink-0 border-l pl-4 space-y-6">
              <CardMetadata
                :card="card"
                :project-id="projectId"
                :is-archived="isReadonly"
                @update:card="applyCardUpdate"
              />
              <USeparator />
              <CardChecklist
                :card-id="card.id"
                :project-id="projectId"
                :readonly="isReadonly"
                :refresh-key="checklistRefresh"
                :visible-limit="4"
                @updated="checklistRefresh++"
              />
              <USeparator />
              <CardAttachments
                :card-id="card.id"
                :project-id="projectId"
                :readonly="isReadonly"
              />
              <USeparator />
              <CardDependencies
                :card-id="card.id"
                :project-id="projectId"
                :readonly="isReadonly"
              />
            </div>
          </div>
        </div>

        <!-- Mobile: tabbed — UModal handles overflow naturally -->
        <div
          data-testid="card-modal-mobile"
          class="md:hidden flex flex-col"
        >
          <div
            v-if="otherViewers.length > 0"
            class="px-4 py-1.5 text-xs text-muted italic border-b"
          >
            <UIcon
              name="i-lucide-eye"
              class="size-3 inline mr-1"
            />
            {{ otherViewers.join(', ') }} {{ otherViewers.length === 1 ? 'is' : 'are' }} viewing
          </div>
          <UTabs
            v-model="activeTab"
            :items="tabs"
            class="border-b px-3"
          />

          <div class="p-4">
            <div
              v-if="activeTab === 'details'"
              class="space-y-4"
            >
              <CardDescription
                :card="card"
                :project-id="projectId"
                :is-archived="isReadonly"
                @update:card="applyCardUpdate"
              />
              <CardMetadata
                :card="card"
                :project-id="projectId"
                :is-archived="isReadonly"
                @update:card="applyCardUpdate"
              />
            </div>

            <div v-else-if="activeTab === 'checklist'">
              <CardChecklist
                :card-id="card.id"
                :project-id="projectId"
                :readonly="isReadonly"
              />
            </div>

            <div v-else-if="activeTab === 'comments'">
              <CardComments
                :card-id="card.id"
                :project-id="projectId"
                :readonly="isReadonly"
              />
            </div>

            <div
              v-else-if="activeTab === 'related'"
              class="space-y-4"
            >
              <CardAttachments
                :card-id="card.id"
                :project-id="projectId"
                :readonly="isReadonly"
              />
              <USeparator />
              <CardDependencies
                :card-id="card.id"
                :project-id="projectId"
                :readonly="isReadonly"
              />
            </div>
            <div
              v-else-if="activeTab === 'docs'"
              class="space-y-8"
            >
              <CardSpec
                :card-id="card.id"
                :project-id="projectId"
                :readonly="isReadonly"
              />
              <template v-if="hasPlan">
                <USeparator />
                <CardPlan
                  :card-id="card.id"
                  :project-id="projectId"
                  :readonly="isReadonly"
                />
              </template>
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
