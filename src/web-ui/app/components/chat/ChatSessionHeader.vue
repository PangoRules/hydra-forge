<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { ChatSessionDetailDto, PromptPresetDto } from '~/types/chat'
import { AiEditMode } from '~/types/chat'

const props = defineProps<{
  session: ChatSessionDetailDto
}>()

const emit = defineEmits<{
  close: []
  toggleScope: [searchAllMyDocs: boolean]
  editPersonality: [personalityId: string | null]
  editMode: [mode: AiEditMode]
  fork: []
}>()

const api = useApi()

const aiEditModeOptions = [
  { label: 'Per mutation', value: AiEditMode.PerMutation },
  { label: 'Blanket', value: AiEditMode.Blanket }
]

const selectedPersonalityId = ref<string | null>(props.session.personalityId)
const personalities = ref<PromptPresetDto[]>([])
const loadingPersonalities = ref(false)

watch(() => props.session.personalityId, (v) => {
  selectedPersonalityId.value = v
})

watch(selectedPersonalityId, (v) => {
  emit('editPersonality', v)
})

function handleScopeToggle(e: Event) {
  const target = e.target as HTMLInputElement
  emit('toggleScope', target.checked)
}

async function handleEditModeChange(val: AiEditMode) {
  emit('editMode', val)
}

async function fetchPersonalities() {
  loadingPersonalities.value = true
  try {
    const { data } = await api.GET<PromptPresetDto[]>(ApiRoutes.Chat.personalities.list())
    personalities.value = (data ?? []).filter(p => !p.archivedAt)
  } catch {
    // personalities degrade to "none" on error
  } finally {
    loadingPersonalities.value = false
  }
}

const personalityItems = computed(() => [
  { label: 'Default', value: null as string | null },
  ...personalities.value.map(p => ({ label: p.name, value: p.id }))
])

onMounted(fetchPersonalities)
</script>

<template>
  <div class="shrink-0 border-b border-gray-200 dark:border-gray-700 px-4 py-3 flex items-center gap-3">
    <!-- Title -->
    <h2 class="font-semibold truncate flex-1 min-w-0 text-sm">
      {{ session.title || 'Chat' }}
    </h2>

    <!-- Scope toggle (personal chats only) -->
    <label
      v-if="!session.projectId"
      class="flex items-center gap-1.5 text-xs text-muted shrink-0 cursor-pointer"
      title="When enabled, the AI searches all your documents"
    >
      <input
        type="checkbox"
        :checked="session.searchAllMyDocs"
        class="size-3.5 rounded border-gray-300 text-primary focus:ring-primary"
        @change="handleScopeToggle"
      >
      <span>All docs</span>
    </label>

    <!-- Personality picker -->
    <USelect
      v-if="!session.projectId"
      v-model="selectedPersonalityId"
      :items="personalityItems"
      :loading="loadingPersonalities"
      size="xs"
      class="w-36 shrink-0"
      placeholder="Personality"
    />

    <!-- AI edit mode picker (project chats only) -->
    <USelect
      v-if="session.projectId"
      :model-value="session.aiEditMode"
      :items="aiEditModeOptions"
      size="xs"
      class="w-32 shrink-0"
      @update:model-value="handleEditModeChange"
    />

    <!-- Fork button: visible on shared project chats the caller doesn't own -->
    <UButton
      v-if="session.isShared && session.projectId"
      icon="i-lucide-git-fork"
      variant="soft"
      color="neutral"
      size="xs"
      title="Summarize → start my own"
      @click="emit('fork')"
    >
      Fork
    </UButton>

    <!-- Close button -->
    <UButton
      v-if="session.status === 'Active'"
      icon="i-lucide-x"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Close chat"
      @click="emit('close')"
    />
  </div>
</template>
