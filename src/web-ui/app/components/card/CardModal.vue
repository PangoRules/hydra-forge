<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import { ApiError } from '~/lib/api-error'
import AppModal from '~/components/shared/AppModal.vue'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import ArchiveCardWarning from '~/components/card/ArchiveCardWarning.vue'
import CardSpec from '~/components/card/CardSpec.vue'
import CardPlan from '~/components/card/CardPlan.vue'
import { useKeyboard } from '~/composables/keyboard/useKeyboard'

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
const showArchiveWarning = ref(false)
const archiveDependents = ref<{ id: string, title: string, type: string }[]>([])
// Bumped both by a child panel's own @updated (immediate local feedback) and by the
// realtime watcher below (remote change to this open card) — one signal, every panel
// re-fetches off the same counter.
const contentRefresh = ref(0)

// API sends CardType as string (JsonStringEnumConverter). C# values: Task, Issue, Idea, Goal, Security
const SPEC_CARD_TYPES = ['Goal', 'Idea', 'Issue', 'Security', 'Task'] as const
const PLAN_CARD_TYPES = ['Issue', 'Task'] as const

// DocType from API (JsonStringEnumConverter): 'Specification', 'Concept', 'Report', 'ValidationMatrix'
const CARD_TYPE_TO_DOC_TYPE: Record<string, string> = {
  Goal: 'Specification',
  Idea: 'Concept',
  Issue: 'Report',
  Security: 'Report',
  Task: 'ValidationMatrix'
}

const hasSpec = computed(() =>
  card.value != null && (SPEC_CARD_TYPES as readonly string[]).includes(card.value.type as unknown as string)
)

const hasPlan = computed(() =>
  card.value != null && (PLAN_CARD_TYPES as readonly string[]).includes(card.value.type as unknown as string)
)

const hasDocsTab = computed(() => hasSpec.value || hasPlan.value)

const specDocType = computed(() => {
  if (!card.value) return 'Specification'
  return CARD_TYPE_TO_DOC_TYPE[String(card.value.type)] ?? 'Specification'
})

// Populated by CardSpec after it loads/creates the spec; passed to CardPlan for Goal linking
const linkedSpecId = ref<string | null>(null)

// Ref to CardPlan child for triggering auto-expand
const cardPlanRef = ref<InstanceType<typeof CardPlan> | null>(null)

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
  { label: 'Comments', value: 'comments' as const },
  ...(hasDocsTab.value ? [{ label: 'Docs', value: 'docs' as const }] : [])
])

watch(hasDocsTab, (has) => {
  if (!has && activeTab.value === 'docs') activeTab.value = 'details'
})

function expandFirstDoc() {
  // Spec is always inline in CardSpec — nothing to expand
  // Plans use accordion expand; trigger first plan open (loads first if needed)
  if (hasPlan.value) {
    nextTick(() => cardPlanRef.value?.loadAndExpandFirst())
  }
}

watch(activeTab, (tab) => {
  if (tab === 'docs') expandFirstDoc()
})

const api = useApi()
const keyboard = useKeyboard()

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
  if (card.value) {
    // Check for dependents before showing confirmation
    fetchCardRelationships()
  } else {
    showArchiveConfirm.value = true
  }
}

async function fetchCardRelationships() {
  try {
    const response = await api.GET(
      ApiRoutes.Relationships.list(props.projectId, card.value!.id)
    )

    // Type assertion for the response data
    const data = response.data as {
      relationships: Array<{
        sourceCardId: string
        targetCardId: string
        sourceCardTitle: string
        targetCardTitle: string
        type: string
      }>
    }

    // A relationship this card participates in can have it on either side —
    // e.g. a "Blocked by" link reverses which side is Source (see
    // CardDependencies.vue's `reverse` flag) — so archiving must warn about
    // relationships regardless of direction, not just the ones where this
    // card happens to be the source.
    const dependents = data.relationships.map(rel =>
      rel.sourceCardId === card.value!.id
        ? { id: rel.targetCardId, title: rel.targetCardTitle, type: rel.type }
        : { id: rel.sourceCardId, title: rel.sourceCardTitle, type: rel.type }
    )

    if (dependents.length > 0) {
      archiveDependents.value = dependents
      showArchiveWarning.value = true
    } else {
      showArchiveConfirm.value = true
    }
  } catch {
    // If we can't fetch relationships, proceed with normal archive
    showArchiveConfirm.value = true
  }
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

// Same fetch, but for a background realtime refresh — doesn't touch loading/error,
// so a remote edit updates the modal in place instead of flashing the spinner.
//
// Keeps the locally-held `version` even though everything else refreshes: `version`
// is the optimistic-concurrency token every save action in this modal (description,
// metadata, archive/restore) sends back. Silently adopting the server's newer version
// here would make a save from this tab succeed as if it read the latest state, even
// though the user never saw or acknowledged the change that happened elsewhere —
// defeating the stale-save conflict check the save endpoints are meant to enforce.
// Only an explicit reload (fetchCard, on mount/reopen) should advance it.
async function refetchCardQuietly() {
  try {
    const { data } = await api.GET(ApiRoutes.Cards.detail(props.projectId, props.cardId))
    if (data && card.value) card.value = { ...(data as CardResponse), version: card.value.version }
  } catch {
    // best-effort — the modal keeps showing its last-known-good state
  }
}

function applyCardUpdate(updated: CardResponse) {
  card.value = updated
}

onMounted(() => {
  fetchCard()
  linkedSpecId.value = null

  // Keyboard shortcuts. allowWhileEditing must stay false/omitted here:
  // useKeyboard's guard is the only thing stopping every "a" a user types
  // into the Tiptap description/spec/plan editors from popping this
  // archive confirm instead of inserting the letter (see D-47 note).
  keyboard.register('Card', 'a', (e) => {
    if (!isArchived.value && !props.readonly) {
      e.preventDefault()
      handleArchive()
    }
  }, 'Archive card')
})

onBeforeUnmount(() => {
  keyboard.unregister('Card')
})

// Presence indicator
const authStore = useAuthStore()
const presenceStore = usePresenceStore()

// Live-update while open — useRealtime.ts writes here on any BoardHub event; if it's
// about the card this modal has open, refresh in place instead of requiring close+reopen.
const board = useBoardStore()
watch(() => board.cardContentEvent, (event) => {
  if (!event || !card.value || event.cardId !== card.value.id) return
  if (event.entityType === 'Card') refetchCardQuietly()
  else contentRefresh.value++
})

const currentUserId = computed(() => authStore.user?.userId)

const isWatching = computed(() =>
  card.value?.watchers?.some(w => w.userId === currentUserId.value) ?? false
)

async function toggleWatch() {
  if (!card.value) return
  const wasWatching = isWatching.value
  try {
    const { data } = wasWatching
      ? await api.DELETE(ApiRoutes.Cards.watch(props.projectId, card.value.id))
      : await api.POST(ApiRoutes.Cards.watch(props.projectId, card.value.id))
    applyCardUpdate(data as CardResponse)
    toast.success(wasWatching ? 'Unwatched card' : 'Now watching card')
  } catch {
    toast.error(wasWatching ? 'Failed to unwatch card' : 'Failed to watch card')
  }
}

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
    width="sm:max-w-6xl"
    :show-close="!!card"
    @update:open="handleOpenChange"
    @close="onClose"
  >
    <template #header>
      <span
        v-if="isWatching"
        class="inline-flex items-center gap-1 px-2 py-0.5 text-xs rounded-full bg-primary/10 text-primary shrink-0"
      >
        <UIcon
          name="i-lucide-eye"
          class="size-3"
        />
        Watching
      </span>
    </template>

    <template #header-trailing>
      <div class="flex items-center gap-1">
        <UButton
          v-if="card"
          variant="ghost"
          size="sm"
          :icon="isWatching ? 'i-lucide-eye-off' : 'i-lucide-eye'"
          :color="isWatching ? 'primary' : 'neutral'"
          :title="isWatching ? 'Watching — click to unwatch' : 'Not watching — click to watch'"
          @click="toggleWatch"
        />
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
          class="hidden md:flex flex-col relative"
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
              <div v-else-if="activeTab === 'comments'">
                <CardComments
                  :card-id="card.id"
                  :project-id="projectId"
                  :readonly="isReadonly"
                  :refresh-key="contentRefresh"
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
                  :refresh-key="contentRefresh"
                  @update:spec-id="linkedSpecId = $event"
                />
                <template v-if="hasPlan">
                  <USeparator v-if="hasSpec" />
                  <CardPlan
                    ref="cardPlanRef"
                    :card-id="card.id"
                    :project-id="projectId"
                    :spec-id="String(card.type) === 'Goal' ? linkedSpecId : null"
                    :readonly="isReadonly"
                    :refresh-key="contentRefresh"
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
                :refresh-key="contentRefresh"
                :visible-limit="4"
                @updated="contentRefresh++"
              />
              <USeparator />
              <CardAttachments
                :card-id="card.id"
                :project-id="projectId"
                :readonly="isReadonly"
                :refresh-key="contentRefresh"
              />
              <USeparator />
              <CardDependencies
                :card-id="card.id"
                :project-id="projectId"
                :readonly="isReadonly"
                :refresh-key="contentRefresh"
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
                :refresh-key="contentRefresh"
              />
            </div>

            <div v-else-if="activeTab === 'comments'">
              <CardComments
                :card-id="card.id"
                :project-id="projectId"
                :readonly="isReadonly"
                :refresh-key="contentRefresh"
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
                :refresh-key="contentRefresh"
              />
              <USeparator />
              <CardDependencies
                :card-id="card.id"
                :project-id="projectId"
                :readonly="isReadonly"
                :refresh-key="contentRefresh"
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
                :refresh-key="contentRefresh"
                @update:spec-id="linkedSpecId = $event"
              />
              <template v-if="hasPlan">
                <USeparator v-if="hasSpec" />
                <CardPlan
                  ref="cardPlanRef"
                  :card-id="card.id"
                  :project-id="projectId"
                  :spec-id="String(card.type) === 'Goal' ? linkedSpecId : null"
                  :readonly="isReadonly"
                  :refresh-key="contentRefresh"
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

  <ArchiveCardWarning
    v-if="showArchiveWarning"
    :dependents="archiveDependents"
    @confirm="confirmArchive"
    @cancel="showArchiveWarning = false"
  />
</template>
