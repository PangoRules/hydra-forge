<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import MarkdownEditor from '~/components/shared/MarkdownEditor.vue'

type CardResponse = components['schemas']['CardResponse']

// Maps numeric enum values to API string enum values
const CARD_TYPE_MAP: Record<number, string> = {
  0: 'Task',
  1: 'Bug',
  2: 'Epic',
  3: 'Spec',
  4: 'Idea'
}

const props = defineProps<{
  card: CardResponse
  projectId: string
  isArchived?: boolean
}>()

const description = ref(props.card.description ?? '')
const saving = ref(false)
const dirty = ref(false)
const saveError = ref<string | null>(null)
const currentVersion = ref(props.card.version)

const api = useApi()
const board = useBoardStore()

let saveTimer: ReturnType<typeof setTimeout> | null = null

/** Normalize card.type to API string enum value — handles both number (legacy)
 *  and string (post-fix) values from the server. */
function toTypeString(type: CardResponse['type']): string {
  return typeof type === 'string' ? type : (CARD_TYPE_MAP[type as number] ?? 'Task')
}

function onDescriptionChange(value: string) {
  if (props.isArchived) return
  description.value = value
  dirty.value = true
  if (saveTimer) clearTimeout(saveTimer)
  saveTimer = setTimeout(saveDescription, 2000)
}

async function saveDescription() {
  if (!dirty.value || props.isArchived) return
  saving.value = true
  saveError.value = null
  try {
    const { data, error } = await api.PUT(ApiRoutes.Cards.update(props.projectId, props.card.id), {
      body: {
        title: props.card.title,
        description: description.value,
        type: toTypeString(props.card.type),
        version: currentVersion.value,
        parentCardId: props.card.parentCardId,
        dueAt: props.card.dueAt
      }
    })
    if (error) throw error
    if (data) currentVersion.value = (data as CardResponse).version
    board.updateCard(props.card.id, { description: description.value })
    dirty.value = false
  } catch (e: unknown) {
    saveError.value = e instanceof Error ? e.message : 'Failed to save'
  } finally {
    saving.value = false
  }
}

function handleSaveClick() {
  if (saveTimer) clearTimeout(saveTimer)
  saveDescription()
}

function onKeydown(e: KeyboardEvent) {
  if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
    e.preventDefault()
    if (saveTimer) clearTimeout(saveTimer)
    saveDescription()
  }
}
</script>

<template>
  <div @keydown="onKeydown">
    <div class="flex items-center justify-between mb-2">
      <p class="text-xs font-medium text-muted uppercase">
        Description
      </p>
      <UButton
        v-if="!isArchived"
        label="Save"
        size="xs"
        variant="soft"
        :loading="saving"
        :disabled="!dirty || saving"
        @click="handleSaveClick"
      />
      <span
        v-else
        class="text-xs text-gray-400"
      >Archived</span>
    </div>

    <MarkdownEditor
      :model-value="description"
      :editable="!isArchived"
      placeholder="Add a description..."
      @update:model-value="onDescriptionChange"
    />

    <p
      v-if="saveError"
      class="text-xs text-error mt-1"
    >
      {{ saveError }}
    </p>
  </div>
</template>
