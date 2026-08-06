<script setup lang="ts">
import type { ChatSessionDetailDto } from '~/types/chat'
import { AiEditMode } from '~/types/chat'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'

const props = withDefaults(
  defineProps<{
    session: ChatSessionDetailDto
    isOwner: boolean
    compact?: boolean
    showBackButton?: boolean
    showNewChatButton?: boolean
    showFullHeightToggle?: boolean
    isFullHeight?: boolean
  }>(),
  {
    compact: false,
    showBackButton: false,
    showNewChatButton: false,
    showFullHeightToggle: false,
    isFullHeight: false
  }
)

const emit = defineEmits<{
  dismiss: []
  closeSession: []
  archiveSession: []
  reopenSession: []
  editMode: [mode: AiEditMode]
  fork: []
  startEditTitle: []
  exportChat: []
  toggleFind: []
  back: []
  newChat: []
  toggleFullHeight: []
}>()

const isActive = computed(() => props.session.status === 'Active')

const aiEditModeOptions = [
  { label: 'Per mutation', value: AiEditMode.PerMutation },
  { label: 'Blanket', value: AiEditMode.Blanket }
]

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

const compactMenuItems = computed(() => {
  const groups: Array<Array<{ label: string, icon?: string, disabled?: boolean, onSelect: () => void }>> = []

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

  // Close/Archive/Reopen are independent, not mutually exclusive — Archive is
  // reachable regardless of Active/Closed (same as the /chats list page's
  // always-present archive button), and Reopen covers both Closed-only and
  // Archived-only (including an Active-but-Archived session).
  if (props.isOwner) {
    const lifecycle: Array<{ label: string, icon: string, onSelect: () => void }> = []
    if (isActive.value) {
      lifecycle.push({
        label: 'Close chat',
        icon: 'i-lucide-check-circle',
        onSelect: () => { showCloseConfirm.value = true }
      })
    }
    if (!isActive.value || props.session.archivedAt) {
      lifecycle.push({
        label: 'Reopen chat',
        icon: 'i-lucide-folder-open',
        onSelect: () => emit('reopenSession')
      })
    }
    lifecycle.push({
      label: 'Archive chat',
      icon: 'i-lucide-archive',
      onSelect: () => { showArchiveConfirm.value = true }
    })
    groups.push(lifecycle)
  }

  return groups
})

defineExpose({ compactMenuItems })
</script>

<template>
  <div class="shrink-0 border-b border-gray-200 dark:border-gray-700 px-4 py-3 flex items-center gap-2">
    <!-- Back to history — dock context only -->
    <UButton
      v-if="compact && showBackButton"
      icon="i-lucide-chevron-left"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Back to history"
      @click="emit('back')"
    />

    <!-- Title — always visible. Clickable-to-rename shortcut in compact mode
         (same rename flow as the kebab's "Rename chat" item); non-compact
         keeps the dedicated pencil button below instead. -->
    <h2
      class="font-semibold truncate flex-1 min-w-0 text-sm"
      :class="compact && isOwner && isActive ? 'cursor-pointer hover:text-primary' : ''"
      :title="compact && isOwner && isActive ? 'Click to rename' : undefined"
      @click="compact && isOwner && isActive ? emit('startEditTitle') : undefined"
    >
      {{ session.title || 'Chat' }}
    </h2>

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

    <!-- AI edit mode picker — owner, active, project chats -->
    <USelect
      v-if="!compact && isOwner && isActive && session.projectId"
      :model-value="session.aiEditMode"
      :items="aiEditModeOptions"
      size="xs"
      class="w-32 shrink-0"
      @update:model-value="handleEditModeChange"
    />

    <!-- Inline lifecycle buttons (mirror kebab items in compact) — each
         condition is independent, not mutually exclusive: Archive is always
         reachable for the owner regardless of Active/Closed, matching the
         /chats list page's own archive button. -->
    <UButton
      v-if="!compact && isOwner && isActive"
      icon="i-lucide-check-circle"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Close chat"
      @click="showCloseConfirm = true"
    />
    <UButton
      v-if="!compact && isOwner && (!isActive || session.archivedAt)"
      icon="i-lucide-folder-open"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Reopen chat"
      @click="emit('reopenSession')"
    />
    <UButton
      v-if="!compact && isOwner"
      icon="i-lucide-archive"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Archive chat"
      @click="showArchiveConfirm = true"
    />

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

    <!-- New chat — dock context only -->
    <UButton
      v-if="compact && showNewChatButton"
      icon="i-lucide-plus"
      variant="ghost"
      color="neutral"
      size="xs"
      title="New chat"
      @click="emit('newChat')"
    />

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

    <!-- Full-height toggle — dock context only -->
    <UButton
      v-if="compact && showFullHeightToggle"
      :icon="isFullHeight ? 'i-lucide-minimize-2' : 'i-lucide-maximize-2'"
      variant="ghost"
      color="neutral"
      size="xs"
      :title="isFullHeight ? 'Exit full height' : 'Full height'"
      @click="emit('toggleFullHeight')"
    />

    <!-- Dismiss button — always available, no API call, no ownership gate.
         Ending the conversation (Close) or hiding it from the list (Archive)
         are the deliberate, confirmed actions above; this just stops showing it. -->
    <UButton
      icon="i-lucide-x"
      variant="ghost"
      color="neutral"
      size="xs"
      title="Dismiss"
      @click="emit('dismiss')"
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
