<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'

interface CardRelationshipDto {
  id: string
  sourceCardId: string
  targetCardId: string
  sourceCardNumber: number
  sourceCardTitle: string
  targetCardNumber: number
  targetCardTitle: string
  type: number
  createdAt: string
  createdByUserId: string
  archivedAt: string | null
}

const props = defineProps<{
  cardId: string
  projectId: string
  readonly?: boolean
}>()

const relationships = ref<CardRelationshipDto[]>([])
const loading = ref(true)
const showDeleteConfirm = ref(false)
const deleteTarget = ref<CardRelationshipDto | null>(null)

const api = useApi()
const toast = useAppToast()

const relationshipLabel: Record<number, string> = {
  1: 'Blocked by',
  2: 'Precedes',
  3: 'Relates to'
}

const relationshipColor: Record<number, string> = {
  1: 'error',
  2: 'warning',
  3: 'neutral'
}

function relatedTitle(rel: CardRelationshipDto): string {
  return rel.sourceCardId === props.cardId
    ? `#${rel.targetCardNumber} ${rel.targetCardTitle}`
    : `#${rel.sourceCardNumber} ${rel.sourceCardTitle}`
}

type BadgeColor = 'neutral' | 'primary' | 'info' | 'success' | 'warning' | 'error'

function badgeColor(rel: CardRelationshipDto): BadgeColor {
  return relationshipColor[rel.type] as BadgeColor
}

const showLinkForm = ref(false)
const searchTerm = ref('')
const searchResults = ref<{ id: string, cardNumber: number, title: string }[]>([])
const searchLoading = ref(false)
const selectedTargetCard = ref<{ id: string, cardNumber: number, title: string } | null>(null)
const selectedType = ref<number>(1)
const linking = ref(false)

let searchDebounce: ReturnType<typeof setTimeout> | null = null

const relationshipTypeOptions = [
  { label: 'Blocked by', value: 1 },
  { label: 'Precedes', value: 2 },
  { label: 'Relates to', value: 3 }
]

async function fetchRelationships() {
  loading.value = true
  try {
    const { data } = await api.GET<{ relationships: CardRelationshipDto[] }>(ApiRoutes.Relationships.list(props.projectId, props.cardId))
    relationships.value = data?.relationships ?? []
  } catch {
    toast.error('Failed to load dependencies')
  } finally {
    loading.value = false
  }
}

function promptDelete(rel: CardRelationshipDto) {
  deleteTarget.value = rel
  showDeleteConfirm.value = true
}

async function confirmDeleteRelationship() {
  if (!deleteTarget.value) return
  const rel = deleteTarget.value
  try {
    await api.DELETE(ApiRoutes.Relationships.relationship(props.projectId, props.cardId, rel.id))
    relationships.value = relationships.value.filter(r => r.id !== rel.id)
    showDeleteConfirm.value = false
    deleteTarget.value = null
  } catch {
    toast.error('Failed to delete dependency')
  }
}

async function searchCards(term: string) {
  if (!term.trim()) {
    searchResults.value = []
    return
  }
  searchLoading.value = true
  try {
    const { data } = await api.GET<{ cards: { id: string, cardNumber: number, title: string }[] }>(
      `${ApiRoutes.Cards.list(props.projectId)}?search=${encodeURIComponent(term)}`
    )
    searchResults.value = (data?.cards ?? []).filter(c => c.id !== props.cardId)
  } catch {
    searchResults.value = []
  } finally {
    searchLoading.value = false
  }
}

function onSearchInput(value: string) {
  searchTerm.value = value
  if (searchDebounce) clearTimeout(searchDebounce)
  searchDebounce = setTimeout(() => searchCards(value), 300)
}

function selectTargetCard(card: { id: string, cardNumber: number, title: string }) {
  selectedTargetCard.value = card
  if (searchDebounce) clearTimeout(searchDebounce)
  searchTerm.value = card.title.slice(0, 50)
  searchResults.value = []
}

function clearSelection() {
  selectedTargetCard.value = null
  searchTerm.value = ''
  searchResults.value = []
}

async function linkCard() {
  if (!selectedTargetCard.value) return
  linking.value = true
  try {
    await api.POST(ApiRoutes.Relationships.create(props.projectId, props.cardId), {
      body: {
        targetCardId: selectedTargetCard.value.id,
        type: selectedType.value
      }
    })
    await fetchRelationships()
    cancelLinkForm()
  } catch {
    toast.error('Failed to link card')
  } finally {
    linking.value = false
  }
}

function cancelLinkForm() {
  showLinkForm.value = false
  searchTerm.value = ''
  searchResults.value = []
  selectedTargetCard.value = null
  selectedType.value = 1
  if (searchDebounce) clearTimeout(searchDebounce)
}

onMounted(() => fetchRelationships())
</script>

<template>
  <div class="space-y-2">
    <p class="text-xs font-medium text-muted uppercase">
      Dependencies
    </p>

    <div class="space-y-1 max-h-48 overflow-y-auto">
      <div
        v-if="loading"
        class="text-xs text-muted"
      >
        Loading...
      </div>
      <div
        v-else-if="relationships.length === 0"
        class="text-xs text-muted"
      >
        No dependencies
      </div>

      <div
        v-for="rel in relationships"
        :key="rel.id"
        class="group flex items-center gap-2 text-sm"
      >
        <UBadge
          variant="subtle"
          size="xs"
          :color="badgeColor(rel)"
        >
          {{ relationshipLabel[rel.type] }}
        </UBadge>
        <span class="truncate text-xs flex-1">{{ relatedTitle(rel) }}</span>
        <UButton
          v-if="!readonly"
          icon="i-lucide-trash-2"
          variant="ghost"
          size="xs"
          color="neutral"
          class="md:opacity-0 md:group-hover:opacity-100 flex-shrink-0"
          aria-label="Remove dependency"
          @click="promptDelete(rel)"
        />
      </div>
    </div>

    <div
      v-if="!readonly"
      class="pt-1"
    >
      <UButton
        v-if="!showLinkForm"
        size="xs"
        variant="ghost"
        @click="showLinkForm = true"
      >
        Link card
      </UButton>

      <div
        v-else
        class="space-y-2 rounded border border-muted p-2"
      >
        <UInput
          v-if="!selectedTargetCard"
          :model-value="searchTerm"
          placeholder="Search cards..."
          size="sm"
          :loading="searchLoading"
          @update:model-value="onSearchInput"
        />

        <div
          v-if="selectedTargetCard"
          class="flex items-center gap-1 rounded bg-primary/10 px-2 py-1 text-xs"
        >
          <span class="truncate font-medium">#{{ selectedTargetCard.cardNumber }} {{ selectedTargetCard.title }}</span>
          <UButton
            icon="i-lucide-x"
            variant="ghost"
            size="xs"
            color="neutral"
            class="ml-auto size-4"
            aria-label="Clear selection"
            @click="clearSelection"
          />
        </div>

        <div
          v-if="!selectedTargetCard && searchResults.length > 0"
          class="max-h-32 overflow-y-auto rounded border border-muted"
        >
          <button
            v-for="card in searchResults"
            :key="card.id"
            type="button"
            class="flex w-full items-center gap-1 px-2 py-1 text-left text-xs hover:bg-muted/50"
            @click="selectTargetCard(card)"
          >
            #{{ card.cardNumber }} {{ card.title }}
          </button>
        </div>

        <USelect
          v-model="selectedType"
          :items="relationshipTypeOptions"
          size="sm"
        />

        <div class="flex gap-2">
          <UButton
            size="sm"
            :disabled="!selectedTargetCard || linking"
            :loading="linking"
            @click="linkCard"
          >
            Link
          </UButton>
          <UButton
            size="sm"
            variant="ghost"
            @click="cancelLinkForm"
          >
            Cancel
          </UButton>
        </div>
      </div>
    </div>
  </div>

  <ConfirmDialog
    v-model:open="showDeleteConfirm"
    title="Remove dependency"
    :message="deleteTarget ? `Remove ${relationshipLabel[deleteTarget.type]} link with #${deleteTarget.sourceCardId === cardId ? deleteTarget.targetCardNumber : deleteTarget.sourceCardNumber} ${deleteTarget.sourceCardId === cardId ? deleteTarget.targetCardTitle : deleteTarget.sourceCardTitle}?` : ''"
    confirm-text="Remove"
    @confirm="confirmDeleteRelationship"
  />
</template>
