<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  card: CardResponse
  projectId: string
}>()

const description = ref(props.card.description ?? '')
const saving = ref(false)
const saveError = ref<string | null>(null)

const api = useApi()
const board = useBoardStore()

let saveTimer: ReturnType<typeof setTimeout> | null = null

function onDescriptionChange(value: string) {
  description.value = value
  if (saveTimer) clearTimeout(saveTimer)
  saveTimer = setTimeout(saveDescription, 1500)
}

async function saveDescription() {
  saving.value = true
  saveError.value = null
  try {
    const { error } = await api.PUT(ApiRoutes.Cards.update(props.projectId, props.card.id), {
      body: { description: description.value }
    })
    if (error) throw error
    board.updateCard(props.card.id, { description: description.value })
  } catch (e: unknown) {
    saveError.value = e instanceof Error ? e.message : 'Failed to save'
  } finally {
    saving.value = false
  }
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
      <span
        v-if="saving"
        class="text-xs text-muted"
      >
        Saving...
      </span>
    </div>

    <MarkdownEditor
      :model-value="description"
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
