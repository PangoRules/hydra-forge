<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { AgentPersonalityDto, ChatSessionDetailDto } from '~/types/chat'
import { AiEditMode } from '~/types/chat'
import PersonalityManageModal from '~/components/chat/PersonalityManageModal.vue'

const props = withDefaults(
  defineProps<{
    session: ChatSessionDetailDto
    isOwner: boolean
    compact?: boolean
  }>(),
  {
    compact: false
  }
)

const emit = defineEmits<{
  dismiss: []
  closeSession: []
  archiveSession: []
  reopenSession: []
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
const showManageModal = ref(false)
watch(() => props.session.personalityId, (v) => {
  selectedPersonalityId.value = v
})

watch(selectedPersonalityId, (v, oldValue) => {
  if (!isActive.value) return
  if (oldValue !== v) {
    emit('editPersonality', v)
  }
})

const showCloseConfirm = ref(false)
const showArchiveConfirm = ref(false)

function confirmClose() {
  showCloseConfirm.value = false
  emit('closeSession')
}

function confirmArchive() {
  showArchiveConfirm.value = false
  emit('archiveSession')
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

const compactMenuItems = computed(() => {
  const groups: Array<Array<{ label: string, icon?: string, disabled?: boolean, children?: Array<{ label: string, icon?: string, disabled?: boolean, onSelect: () => void }>, onSelect?: () => void }>> = []

  if (props.isOwner) {
    groups.push([
      { label: 'Rename chat', icon: 'i-lucide-pencil', disabled: !isActive.value, onSelect: () => emit('startEditTitle') },
      { label: 'Export chat', icon: 'i-lucide-download', onSelect: () => emit('exportChat') },
      { label: 'Find in conversation', icon: 'i-lucide-search', onSelect: () => emit('toggleFind') }
    ])
  } else {
    groups.push([
      { label: 'Find in conversation', icon: 'i-lucide-search', onSelect: () => emit('toggleFind') }
    ])
  }

  if (props.isOwner && isActive.value && !props.session.projectId) {
    groups.push([
      {
        label: 'Select Personality',
        icon: 'i-lucide-user-round',
        children: [
          ...personalities.value.map(p => ({
            label: p.name,
            icon: selectedPersonalityId.value === p.id ? 'i-lucide-check' : undefined,
            onSelect: () => { selectedPersonalityId.value = p.id }
          }))
        ]
      },
      { label: 'Manage personalities…', icon: 'i-lucide-users', onSelect: () => { showManageModal.value = true } }
    ])
  }

  if (props.isOwner && isActive.value && props.session.projectId) {
    groups.push(
      aiEditModeOptions.map(opt => ({
        label: opt.label,
        icon: props.session.aiEditMode === opt.value ? 'i-lucide-check' : undefined,
        onSelect: () => emit('editMode', opt.value)
      }))
    )
  }

  if (props.session.isShared && props.session.projectId && !props.isOwner) {
    groups.push([
      { label: 'Fork (summarize → start my own)', icon: 'i-lucide-git-fork', onSelect: () => emit('fork') }
    ])
  }

  if (props.isOwner && isActive.value) {
    groups.push([
      {
        label: 'Close chat',
        icon: 'i-lucide-check-circle',
        onSelect: () => { showCloseConfirm.value = true }
      }
    ])
  } else if (props.isOwner && props.session.status === 'Closed' && !props.session.archivedAt) {
    groups.push([
      {
        label: 'Archive chat',
        icon: 'i-lucide-archive',
        onSelect: () => { showArchiveConfirm.value = true }
      }
    ])
  } else if (props.isOwner && props.session.archivedAt) {
    groups.push([
      {
        label: 'Reopen chat',
        icon: 'i-lucide-folder-open',
        onSelect: () => emit('reopenSession')
      }
    ])
  }

  return groups
})

onMounted(fetchPersonalities)

defineExpose({ compactMenuItems })
</script>

<template>
  <div class="shrink-0 border-b border-gray-200 dark:border-gray-700 px-4 py-3 flex items-center gap-2">
    <!-- Title (editable by owner) -->
    <h2
      v-if="!compact"
      class="font-semibold truncate flex-1 min-w-0 text-sm"
    >
      {{ session.title || 'Chat' }}
    </h2>
    <div
      v-else
      class="flex-1 min-w-0"
    />

    <!-- Title edit pencil (owner, active only) -->
    <UButton
      v-if="!compact && isOwner"
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
      v-if="!compact && isOwner"
      icon="i-lucide-download"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Export chat"
      @click="emit('exportChat')"
    />

    <!-- Find -->
    <UButton
      v-if="!compact"
      icon="i-lucide-search"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Find in conversation"
      @click="emit('toggleFind')"
    />

    <!-- Lifecycle actions — owner, non-compact only -->
    <template v-if="!compact && isOwner && isActive">
      <!-- AI edit mode picker — owner, active, project chats -->
      <USelect
        v-if="session.projectId"
        :model-value="session.aiEditMode"
        :items="aiEditModeOptions"
        size="xs"
        class="w-32 shrink-0"
        @update:model-value="handleEditModeChange"
      />

      <!-- Inline lifecycle buttons (mirror kebab items in compact) -->
      <UButton
        v-if="session.status === 'Active'"
        icon="i-lucide-check-circle"
        variant="ghost"
        color="neutral"
        size="xs"
        title="Close chat"
        @click="showCloseConfirm = true"
      />
      <UButton
        v-if="session.status === 'Closed'"
        icon="i-lucide-archive"
        variant="ghost"
        color="neutral"
        size="xs"
        title="Archive chat"
        @click="showArchiveConfirm = true"
      />
      <UButton
        v-if="session.archivedAt"
        icon="i-lucide-folder-open"
        variant="ghost"
        color="neutral"
        size="xs"
        title="Reopen chat"
        @click="emit('reopenSession')"
      />
    </template>

    <!-- Fork button: visible on shared project chats the caller doesn't own -->
    <UButton
      v-if="!compact && session.isShared && session.projectId && !isOwner"
      icon="i-lucide-git-fork"
      variant="soft"
      color="neutral"
      size="xs"
      title="Summarize → start my own"
      @click="emit('fork')"
    >
      Fork
    </UButton>

    <!-- Kebab menu — compact mode only -->
    <UDropdownMenu
      v-if="compact"
      :items="compactMenuItems"
    >
      <UButton
        icon="i-lucide-more-vertical"
        variant="ghost"
        color="neutral"
        size="xs"
        title="More actions"
      />
    </UDropdownMenu>

    <!-- Dismiss button — owner, active -->
    <UButton
      v-if="isOwner && isActive"
      icon="i-lucide-x"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Dismiss"
      @click="emit('dismiss')"
    />

    <PersonalityManageModal
      v-model:open="showManageModal"
      @changed="fetchPersonalities"
    />

    <ConfirmDialog
      v-model:open="showCloseConfirm"
      data-testid="close-session-confirm"
      title="Close chat"
      message="This chat will be marked as closed. You can still view the conversation and messages."
      confirm-text="Close"
      @confirm="confirmClose"
    />

    <ConfirmDialog
      v-model:open="showArchiveConfirm"
      title="Archive chat"
      message="This chat will be archived. You can reopen it later from the chat list."
      confirm-text="Archive"
      confirm-color="error"
      @confirm="confirmArchive"
    />
  </div>
</template>
