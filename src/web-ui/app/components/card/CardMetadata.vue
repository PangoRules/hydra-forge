<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import { CARD_TYPE_OPTIONS, cardTypeOption, cardTypeToApiString } from '~/lib/card-type'
import { formatDueDate, isOverdue } from '~/lib/date'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  card: CardResponse
  projectId: string
  isArchived?: boolean
}>()

const emit = defineEmits<{
  'update:card': [card: CardResponse]
}>()

const api = useApi()
const board = useBoardStore()
const toast = useAppToast()

const typeOption = computed(() => cardTypeOption(props.card.type))
const typeValue = computed(() => typeOption.value.apiValue)
const savingType = ref(false)
const savingColumn = ref(false)

const currentColumnOption = computed(() =>
  board.columns.find(c => c.id === props.card.columnId)
)
const columnOptions = computed(() =>
  board.columns.map(c => ({ label: c.name, value: c.id }))
)

async function handleColumnChange(targetColumnId: string) {
  if (props.isArchived || targetColumnId === props.card.columnId) return
  savingColumn.value = true
  const targetPosition = board.cardsByColumn.get(targetColumnId)?.length ?? 0
  try {
    const { data } = await api.POST(ApiRoutes.Cards.move(props.projectId, props.card.id), {
      body: {
        targetColumnId,
        targetPosition,
        confirmBlockedMove: true,
        version: props.card.version
      }
    })
    if (data) emit('update:card', data as CardResponse)
  } catch {
    toast.error('Failed to move card')
  } finally {
    savingColumn.value = false
  }
}

const editingDueDate = ref(false)
const savingDueDate = ref(false)
const dueDateInput = ref(toDateInputValue(props.card.dueAt))

function toDateInputValue(dueAt: string | null): string {
  return dueAt ? dueAt.slice(0, 10) : ''
}

watch(() => props.card.dueAt, (val) => {
  if (!editingDueDate.value) dueDateInput.value = toDateInputValue(val)
})

const availableMembers = computed(() =>
  board.members.filter(m => !props.card.assignees?.some(a => a.userId === m.userId))
)

async function persistCardFields(fields: { type?: string, dueAt?: string | null, parentCardId?: string | null }) {
  try {
    const { data } = await api.PUT(ApiRoutes.Cards.update(props.projectId, props.card.id), {
      body: {
        title: props.card.title,
        description: props.card.description,
        type: fields.type ?? cardTypeToApiString(props.card.type),
        version: props.card.version,
        parentCardId: fields.parentCardId !== undefined ? fields.parentCardId : props.card.parentCardId,
        dueAt: fields.dueAt !== undefined ? fields.dueAt : props.card.dueAt
      }
    })
    if (data) emit('update:card', data as CardResponse)
    return true
  } catch {
    toast.error('Failed to update card')
    return false
  }
}

async function handleTypeChange(value: string) {
  if (props.isArchived || value === cardTypeToApiString(props.card.type)) return
  savingType.value = true
  await persistCardFields({ type: value })
  savingType.value = false
}

async function saveDueDate() {
  if (props.isArchived) return
  savingDueDate.value = true
  const dueAt = dueDateInput.value ? `${dueDateInput.value}T00:00:00Z` : null
  const ok = await persistCardFields({ dueAt })
  savingDueDate.value = false
  if (ok) editingDueDate.value = false
}

function cancelDueDateEdit() {
  dueDateInput.value = toDateInputValue(props.card.dueAt)
  editingDueDate.value = false
}

async function handleAssign(userId: string) {
  if (!userId || props.isArchived) return
  try {
    const { data } = await api.POST(ApiRoutes.Cards.assignees(props.projectId, props.card.id), {
      body: { assigneeUserId: userId }
    })
    if (data) emit('update:card', data as CardResponse)
  } catch {
    toast.error('Failed to assign member')
  }
}

async function handleUnassign(userId: string) {
  if (props.isArchived) return
  try {
    const { data } = await api.DELETE(ApiRoutes.Cards.removeAssignee(props.projectId, props.card.id, userId))
    if (data) emit('update:card', data as CardResponse)
  } catch {
    toast.error('Failed to remove assignee')
  }
}

const parentCandidates = ref<CardResponse[]>([])
const savingParent = ref(false)

const parentCard = computed(() => {
  if (!props.card.parentCardId) return null
  for (const col of board.columns) {
    const found = board.cardsByColumn.get(col.id)?.find(c => c.id === props.card.parentCardId)
    if (found) return found
  }
  return null
})

async function fetchParentCandidates() {
  try {
    const { data } = await api.GET<{ cards: CardResponse[] }>(ApiRoutes.Cards.list(props.projectId))
    parentCandidates.value = (data?.cards ?? []).filter(c => c.id !== props.card.id)
  } catch {
    parentCandidates.value = []
  }
}

async function handleParentChange(value: string | null) {
  if (props.isArchived) return
  const newParentId = value || null
  if (newParentId === props.card.parentCardId) return
  savingParent.value = true
  await persistCardFields({ parentCardId: newParentId })
  savingParent.value = false
}

const savingChild = ref(false)

const childCards = computed(() => {
  const children: CardResponse[] = []
  for (const cards of board.cardsByColumn.values()) {
    for (const c of cards) {
      if (c.parentCardId === props.card.id) children.push(c)
    }
  }
  return children
})

const addChildCandidates = computed(() => {
  const excludeIds = new Set(childCards.value.map(c => c.id))
  excludeIds.add(props.card.id)
  const candidates: CardResponse[] = []
  for (const cards of board.cardsByColumn.values()) {
    for (const c of cards) {
      if (!excludeIds.has(c.id)) candidates.push(c)
    }
  }
  return candidates
})

async function addChild(childId: string) {
  const child = addChildCandidates.value.find(c => c.id === childId)
  if (!child) return
  savingChild.value = true
  try {
    const { data } = await api.PUT(ApiRoutes.Cards.update(props.projectId, childId), {
      body: {
        title: child.title,
        description: child.description,
        type: cardTypeToApiString(child.type),
        version: child.version,
        parentCardId: props.card.id,
        dueAt: child.dueAt
      }
    })
    if (data) {
      const updated = data as CardResponse
      for (const cards of board.cardsByColumn.values()) {
        const idx = cards.findIndex(c => c.id === updated.id)
        if (idx !== -1) cards[idx] = updated
      }
      toast.success('Child added')
    }
  } catch {
    toast.error('Failed to add child')
  } finally {
    savingChild.value = false
  }
}

async function removeChild(child: CardResponse) {
  savingChild.value = true
  try {
    const { data } = await api.PUT(ApiRoutes.Cards.update(props.projectId, child.id), {
      body: {
        title: child.title,
        description: child.description,
        type: cardTypeToApiString(child.type),
        version: child.version,
        parentCardId: null,
        dueAt: child.dueAt
      }
    })
    if (data) {
      const updated = data as CardResponse
      for (const cards of board.cardsByColumn.values()) {
        const idx = cards.findIndex(c => c.id === updated.id)
        if (idx !== -1) cards[idx] = updated
      }
      toast.success('Child removed')
    }
  } catch {
    toast.error('Failed to remove child')
  } finally {
    savingChild.value = false
  }
}

onMounted(() => {
  if (!props.isArchived) fetchParentCandidates()
})
</script>

<template>
  <div class="space-y-4">
    <!-- Type + Column -->
    <div class="grid grid-cols-2 gap-3">
      <!-- Type -->
      <div>
        <p class="text-xs font-medium text-muted uppercase mb-1">
          Type
        </p>
        <USelect
          v-if="!isArchived"
          :model-value="typeValue"
          :items="CARD_TYPE_OPTIONS.map(o => ({ label: o.label, value: o.apiValue }))"
          :loading="savingType"
          size="xs"
          class="w-full"
          @update:model-value="handleTypeChange"
        />
        <UBadge
          v-else
          :color="typeOption.color"
          :icon="typeOption.icon"
          variant="subtle"
        >
          {{ typeOption.label }}
        </UBadge>
      </div>

      <!-- Column -->
      <div>
        <p class="text-xs font-medium text-muted uppercase mb-1">
          Column
        </p>
        <USelect
          v-if="!isArchived"
          :model-value="currentColumnOption?.id"
          :items="columnOptions"
          :loading="savingColumn"
          size="xs"
          class="w-full"
          @update:model-value="handleColumnChange"
        />
        <UBadge
          v-else
          color="neutral"
          variant="subtle"
        >
          {{ currentColumnOption?.name ?? props.card.columnId.slice(0, 8) }}
        </UBadge>
      </div>
    </div>

    <!-- Parent + Children -->
    <div class="grid grid-cols-2 gap-3">
      <!-- Parent -->
      <div>
        <p class="text-xs font-medium text-muted uppercase mb-1">
          Parent
        </p>
        <USelect
          v-if="!isArchived"
          :model-value="card.parentCardId ?? null"
          :items="[{ label: 'None', value: null }, ...parentCandidates.map(c => ({ label: c.title, value: c.id }))]"
          :loading="savingParent"
          size="xs"
          class="w-full"
          @update:model-value="handleParentChange"
        />
        <span
          v-else
          class="text-sm"
        >{{ parentCard?.title ?? 'None' }}</span>
      </div>

      <!-- Children -->
      <div>
        <p class="text-xs font-medium text-muted uppercase mb-1">
          Children
        </p>
        <div
          v-if="!isArchived"
          class="space-y-2"
        >
          <div
            v-if="childCards.length > 0"
            class="flex flex-wrap gap-1.5"
          >
            <span
              v-for="child in childCards"
              :key="child.id"
              class="inline-flex items-center gap-1 px-2 py-0.5 text-xs rounded-full border border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-800"
            >
              <UIcon
                :name="cardTypeOption(child.type).icon"
                class="size-3"
              />
              {{ cardTypeOption(child.type).label }} #{{ child.cardNumber }}
              <button
                class="ml-0.5 text-gray-400 hover:text-red-500"
                :disabled="savingChild"
                @click="removeChild(child)"
              >
                <UIcon
                  name="i-lucide-x"
                  class="size-3"
                />
              </button>
            </span>
          </div>
          <p
            v-else
            class="text-xs text-gray-400"
          >
            None
          </p>
          <select
            class="w-full px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
            :value="''"
            :disabled="savingChild"
            @change="(e: Event) => addChild((e.target as HTMLSelectElement).value)"
          >
            <option
              value=""
              disabled
            >
              Add child...
            </option>
            <option
              v-for="c in addChildCandidates"
              :key="c.id"
              :value="c.id"
            >
              {{ cardTypeOption(c.type).label }} #{{ c.cardNumber }} — {{ c.title }}
            </option>
          </select>
        </div>
        <div
          v-else
          class="space-y-1"
        >
          <span
            v-if="childCards.length === 0"
            class="text-sm text-muted"
          >None</span>
          <div
            v-else
            class="flex flex-wrap gap-1.5"
          >
            <span
              v-for="child in childCards"
              :key="child.id"
              class="inline-flex items-center gap-1 px-2 py-0.5 text-xs rounded-full border border-gray-300 dark:border-gray-600"
            >
              <UIcon
                :name="cardTypeOption(child.type).icon"
                class="size-3"
              />
              {{ cardTypeOption(child.type).label }} #{{ child.cardNumber }}
            </span>
          </div>
        </div>
      </div>
    </div>

    <!-- Assignees -->
    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1.5">
        Assignees
      </p>
      <div class="flex flex-wrap gap-1.5 mb-2">
        <span
          v-for="a in card.assignees"
          :key="a.userId"
          class="inline-flex items-center gap-1 px-2 py-0.5 text-xs rounded-full bg-primary/10 text-primary dark:bg-primary/20"
        >
          {{ a.username }}
          <button
            v-if="!isArchived"
            class="hover:text-red-500 leading-none"
            @click="handleUnassign(a.userId)"
          >×</button>
        </span>
        <span
          v-if="!card.assignees?.length"
          class="text-xs text-muted"
        >None</span>
      </div>
      <select
        v-if="!isArchived && availableMembers.length > 0"
        class="w-full px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
        @change="(e: Event) => { const s = e.target as HTMLSelectElement; if (s.value) handleAssign(s.value); s.value = '' }"
      >
        <option value="">
          + Add assignee
        </option>
        <option
          v-for="m in availableMembers"
          :key="m.userId"
          :value="m.userId"
        >
          {{ m.username }}
        </option>
      </select>
    </div>

    <!-- Due Date -->
    <div>
      <p class="text-xs font-medium text-muted uppercase mb-1">
        Due Date
      </p>
      <div
        v-if="!isArchived && editingDueDate"
        class="flex items-center gap-1"
      >
        <input
          v-model="dueDateInput"
          type="date"
          class="text-sm px-2 py-1 border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
        >
        <UButton
          icon="i-lucide-check"
          size="xs"
          variant="soft"
          :loading="savingDueDate"
          @click="saveDueDate"
        />
        <UButton
          icon="i-lucide-x"
          size="xs"
          variant="ghost"
          @click="cancelDueDateEdit"
        />
      </div>
      <button
        v-else
        type="button"
        class="text-sm text-left"
        :class="[isOverdue(card.dueAt) ? 'text-red-500 font-medium' : '', isArchived ? 'cursor-default' : 'hover:underline']"
        :disabled="isArchived"
        @click="editingDueDate = true"
      >
        {{ formatDueDate(card.dueAt) ?? 'None' }}
      </button>
    </div>
  </div>
</template>
