<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import MarkdownEditor from '~/components/shared/MarkdownEditor.vue'
import { toTypeString } from '~/lib/card-type'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  card: CardResponse
  projectId: string
  isArchived?: boolean
}>()

const emit = defineEmits<{
  'update:card': [card: CardResponse]
}>()

const description = ref(props.card.description ?? '')
const saving = ref(false)
const dirty = ref(false)
const saveError = ref<string | null>(null)

const api = useApi()
const board = useBoardStore()

let saveTimer: ReturnType<typeof setTimeout> | null = null

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
        version: props.card.version,
        parentCardId: props.card.parentCardId,
        dueAt: props.card.dueAt
      }
    })
    if (error) throw error
    if (data) emit('update:card', data as CardResponse)
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
      role="alert"
    >
      {{ saveError }}
    </p>
    <p
      v-if="saving"
      class="sr-only"
      aria-live="polite"
    >
      Saving description…
    </p>
  </div>
</template>
