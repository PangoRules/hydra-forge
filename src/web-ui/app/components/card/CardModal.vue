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

// API sends CardType as string (JsonStringEnumConverter). C# values: Task, Issue, Idea, Goal
const SPEC_CARD_TYPES = ['Goal', 'Idea', 'Issue'] as const
const PLAN_CARD_TYPES = ['Goal', 'Issue', 'Task'] as const

// DocType enum (API int): Specification=1, Concept=2, Report=3
const CARD_TYPE_TO_DOC_TYPE: Record<string, number> = { Goal: 1, Idea: 2, Issue: 3 }

const hasSpec = computed(() =>
  card.value != null && (SPEC_CARD_TYPES as readonly string[]).includes(card.value.type as unknown as string)
)

const hasPlan = computed(() =>
  card.value != null && (PLAN_CARD_TYPES as readonly string[]).includes(card.value.type as unknown as string)
)

const hasDocsTab = computed(() => hasSpec.value || hasPlan.value)

const specDocType = computed(() => {
  if (!card.value) return 1
  return CARD_TYPE_TO_DOC_TYPE[String(card.value.type)] ?? 1
})

// Populated by CardSpec after it loads/creates the spec; passed to CardPlan for Goal linking
const linkedSpecId = ref<string | null>(null)

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
  linkedSpecId.value = null
})

// Presence indicator
const authStore = useAuthStore()
const presenceStore = usePresenceStore()

const currentUserId = computed(() => authStore.user?.userId)

/** Look up username from presence store (online users) instead of
 *  boardStore.members — members may not be loaded yet when CardModal
 *  mounts, but online presence data is always available for connected users. */
function viewerName(userId: string): string | undefined {
  for (const users of presenceStore.onlineUsers.values()) {
    const found = users.find(u => u.userId === userId)
    if (found) return found.username
  }
  return undefined
}

const otherViewers = computed(() => {
  const myId = currentUserId.value
  if (!myId) return []
  const viewers: string[] = []
  for (const [userId, cardId] of presenceStore.focusedCards) {
    if (cardId === props.cardId && userId !== myId) {
      const name = viewerName(userId)
      if (name) viewers.push(name)
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
                  v-if="hasSpec"
                  :card-id="card.id"
                  :project-id="projectId"
                  :doc-type="specDocType"
                  :readonly="isReadonly"
                  @update:spec-id="linkedSpecId = $event"
                />
                <template v-if="hasPlan">
                  <USeparator v-if="hasSpec" />
                  <CardPlan
                    :card-id="card.id"
                    :project-id="projectId"
                    :spec-id="String(card.type) === 'Goal' ? linkedSpecId : null"
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
                v-if="hasSpec"
                :card-id="card.id"
                :project-id="projectId"
                :doc-type="specDocType"
                :readonly="isReadonly"
                @update:spec-id="linkedSpecId = $event"
              />
              <template v-if="hasPlan">
                <USeparator v-if="hasSpec" />
                <CardPlan
                  :card-id="card.id"
                  :project-id="projectId"
                  :spec-id="String(card.type) === 'Goal' ? linkedSpecId : null"
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
