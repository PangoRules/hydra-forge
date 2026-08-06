<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { AgentPersonalityDto, ChatSessionDetailDto } from '~/types/chat'
import { AiEditMode } from '~/types/chat'

const props = defineProps<{
  session: ChatSessionDetailDto
  isOwner: boolean
}>()

const emit = defineEmits<{
  close: []
  toggleScope: [searchAllMyDocs: boolean]
  editPersonality: [personalityId: string | null]
  editMode: [mode: AiEditMode]
  fork: []
  startEditTitle: []
  exportChat: []
  toggleFind: []
}>()

const api = useApi()
const toast = useAppToast()

const isActive = computed(() => props.session.status === 'Active')

const aiEditModeOptions = [
  { label: 'Per mutation', value: AiEditMode.PerMutation },
  { label: 'Blanket', value: AiEditMode.Blanket }
]

const selectedPersonalityId = ref<string | null>(props.session.personalityId)
const personalities = ref<AgentPersonalityDto[]>([])
const loadingPersonalities = ref(false)
watch(() => props.session.personalityId, (v) => {
  selectedPersonalityId.value = v
})

watch(selectedPersonalityId, (v, oldValue) => {
  if (!isActive.value) return
  if (oldValue !== v) {
    emit('editPersonality', v)
  }
})

function handleScopeToggle(e: Event) {
  if (!isActive.value) return
  const target = e.target as HTMLInputElement
  emit('toggleScope', target.checked)
}

async function handleEditModeChange(val: AiEditMode) {
  if (!isActive.value) return
  emit('editMode', val)
}

async function fetchPersonalities() {
  loadingPersonalities.value = true
  try {
    const { data } = await api.GET<AgentPersonalityDto[]>(ApiRoutes.Chat.personalities.list())
    personalities.value = (data ?? []).filter(p => !p.archivedAt)
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to load personalities')
  } finally {
    loadingPersonalities.value = false
  }
}

const personalityItems = computed(() => {
  const items = personalities.value.map(p => ({ label: p.name, value: p.id }))
  // Only include "Default" option when there is an actual default personality to clear to
  const defaultPersonality = personalities.value.find(p => p.isDefault)
  if (defaultPersonality) {
    return [{ label: 'Default', value: defaultPersonality.id as string | null }, ...items]
  }
  return items
})

onMounted(fetchPersonalities)
</script>

<template>
  <div class="shrink-0 border-b border-gray-200 dark:border-gray-700 px-4 py-3 flex items-center gap-2">
    <!-- Title (editable by owner) -->
    <h2 class="font-semibold truncate flex-1 min-w-0 text-sm">
      {{ session.title || 'Chat' }}
    </h2>

    <!-- Title edit pencil (owner, active only) -->
    <UButton
      v-if="isOwner"
      icon="i-lucide-pencil"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Rename chat"
      :disabled="!isActive"
      @click="emit('startEditTitle')"
    />

    <!-- Export (owner only) -->
    <UButton
      v-if="isOwner"
      icon="i-lucide-download"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Export chat"
      @click="emit('exportChat')"
    />

    <!-- Find -->
    <UButton
      icon="i-lucide-search"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Find in conversation"
      @click="emit('toggleFind')"
    />

    <!-- Scope toggle — owner, active, non-project -->
    <label
      v-if="isOwner && isActive && !session.projectId"
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

    <!-- Personality picker — owner, active, non-project -->
    <USelect
      v-if="isOwner && isActive && !session.projectId"
      v-model="selectedPersonalityId"
      :items="personalityItems"
      :loading="loadingPersonalities"
      size="xs"
      class="w-36 shrink-0"
      placeholder="Personality"
    />

    <!-- AI edit mode picker — owner, active, project chats -->
    <USelect
      v-if="isOwner && isActive && session.projectId"
      :model-value="session.aiEditMode"
      :items="aiEditModeOptions"
      size="xs"
      class="w-32 shrink-0"
      @update:model-value="handleEditModeChange"
    />

    <!-- Fork button: visible on shared project chats the caller doesn't own -->
    <UButton
      v-if="session.isShared && session.projectId && !isOwner"
      icon="i-lucide-git-fork"
      variant="soft"
      color="neutral"
      size="xs"
      title="Summarize → start my own"
      @click="emit('fork')"
    >
      Fork
    </UButton>

    <!-- Close button — owner, active -->
    <UButton
      v-if="isOwner && isActive"
      icon="i-lucide-x"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Close chat"
      @click="emit('close')"
    />
  </div>
</template>
