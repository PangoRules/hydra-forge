<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'

interface ChecklistItemResponse {
  id: string
  text: string
  isCompleted: boolean
  position: number
}

const props = defineProps<{
  cardId: string
  projectId: string
}>()

const api = useApi()
const toast = useAppToast()

const items = ref<ChecklistItemResponse[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const newItemText = ref('')
const adding = ref(false)

const completedCount = computed(() => items.value.filter((i: ChecklistItemResponse) => i.isCompleted).length)
const totalCount = computed(() => items.value.length)
const progressPercent = computed(() =>
  totalCount.value === 0 ? 0 : Math.round((completedCount.value / totalCount.value) * 100)
)

async function fetchItems() {
  loading.value = true
  error.value = null
  try {
    const { data } = await api.GET<{ items: ChecklistItemResponse[] }>(
      ApiRoutes.Checklist.list(props.projectId, props.cardId)
    )
    items.value = data?.items ?? ([] as ChecklistItemResponse[])
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load checklist'
  } finally {
    loading.value = false
  }
}

async function addItem() {
  const text = newItemText.value.trim()
  if (!text) return
  adding.value = true
  try {
    const { data, error: apiError } = await api.POST<ChecklistItemResponse>(
      ApiRoutes.Checklist.create(props.projectId, props.cardId),
      { body: { text } }
    )
    if (apiError) throw apiError
    items.value.push(data as ChecklistItemResponse)
    newItemText.value = ''
  } catch {
    toast.error('Failed to add item')
  } finally {
    adding.value = false
  }
}

async function toggleItem(item: ChecklistItemResponse) {
  const idx = items.value.findIndex((i: ChecklistItemResponse) => i.id === item.id)
  if (idx === -1) return
  const original = items.value[idx]!
  items.value[idx] = { id: original.id, text: original.text, isCompleted: !original.isCompleted, position: original.position }
  try {
    await api.PATCH(ApiRoutes.Checklist.toggle(props.projectId, props.cardId, item.id))
  } catch {
    items.value[idx] = original
    toast.error('Failed to update item')
  }
}

async function deleteItem(item: ChecklistItemResponse) {
  const idx = items.value.findIndex((i: ChecklistItemResponse) => i.id === item.id)
  if (idx === -1) return
  const removed = items.value.splice(idx, 1)[0]!
  try {
    await api.DELETE(ApiRoutes.Checklist.item(props.projectId, props.cardId, item.id))
  } catch {
    items.value.splice(idx, 0, removed)
    toast.error('Failed to delete item')
  }
}

async function moveItem(item: ChecklistItemResponse, direction: 'up' | 'down') {
  const idx = items.value.findIndex((i: ChecklistItemResponse) => i.id === item.id)
  if (idx === -1) return
  const newIdx = direction === 'up' ? idx - 1 : idx + 1
  if (newIdx < 0 || newIdx >= items.value.length) return
  const reordered = [...items.value]
  const [moved] = reordered.splice(idx, 1)
  reordered.splice(newIdx, 0, moved!)
  items.value = reordered
  try {
    await api.PUT(ApiRoutes.Checklist.reorder(props.projectId, props.cardId, item.id), {
      body: { newPosition: newIdx }
    })
  } catch {
    await fetchItems()
    toast.error('Failed to reorder item')
  }
}

onMounted(() => fetchItems())
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between">
      <h3 class="font-medium text-sm">
        Checklist
      </h3>
      <span class="text-xs text-muted">
        {{ completedCount }}/{{ totalCount }}
      </span>
    </div>

    <div
      v-if="totalCount > 0"
      class="space-y-1"
    >
      <div class="h-2 bg-muted rounded-full overflow-hidden">
        <div
          class="h-full bg-primary transition-all"
          :style="{ width: `${progressPercent}%` }"
        />
      </div>
    </div>

    <div
      v-if="loading"
      class="py-4 text-center text-sm text-muted"
    >
      Loading...
    </div>
    <div
      v-else-if="error"
      class="py-4 text-center text-sm text-error"
    >
      {{ error }}
    </div>
    <ul
      v-else-if="items.length > 0"
      class="space-y-1"
    >
      <li
        v-for="(item, idx) in items"
        :key="item.id"
        class="group flex items-start gap-2 rounded p-1 hover:bg-muted/50"
      >
        <button
          class="mt-0.5 flex-shrink-0 w-4 h-4 rounded border cursor-pointer flex items-center justify-center"
          :class="item.isCompleted ? 'bg-primary border-primary' : 'border-muted'"
          :aria-label="item.isCompleted ? 'Mark incomplete' : 'Mark complete'"
          @click="toggleItem(item)"
        >
          <UIcon
            v-if="item.isCompleted"
            name="i-lucide-check"
            class="w-3 h-3 text-white"
          />
        </button>

        <span
          class="flex-1 text-sm leading-6"
          :class="item.isCompleted ? 'line-through text-muted' : ''"
        >
          {{ item.text }}
        </span>

        <div class="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 flex-shrink-0">
          <UButton
            variant="ghost"
            size="xs"
            icon="i-lucide-chevron-up"
            class="w-5 h-5"
            :disabled="idx === 0"
            @click="moveItem(item, 'up')"
          />
          <UButton
            variant="ghost"
            size="xs"
            icon="i-lucide-chevron-down"
            class="w-5 h-5"
            :disabled="idx === items.length - 1"
            @click="moveItem(item, 'down')"
          />
          <UButton
            variant="ghost"
            size="xs"
            icon="i-lucide-trash-2"
            class="w-5 h-5 text-error"
            @click="deleteItem(item)"
          />
        </div>
      </li>
    </ul>
    <p
      v-else
      class="text-sm text-muted py-2"
    >
      No items yet
    </p>

    <form
      class="flex gap-2"
      @submit.prevent="addItem"
    >
      <UInput
        v-model="newItemText"
        placeholder="Add item..."
        size="sm"
        class="flex-1"
        :disabled="adding"
      />
      <UButton
        type="submit"
        size="sm"
        :loading="adding"
        :disabled="!newItemText.trim() || adding"
      >
        Add
      </UButton>
    </form>
  </div>
</template>
