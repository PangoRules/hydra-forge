<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes, UiRoutes } from '~/lib/routes'
import MemberManagementPanel from '~/components/project/MemberManagementPanel.vue'
import ProjectNarrativeModal from '~/components/project/ProjectNarrativeModal.vue'
import { useBoardStore } from '~/stores/board'
import { useCardPopupStore } from '~/stores/cardPopup'

definePageMeta({ middleware: ['auth'] })

const route = useRoute()
const projectId = route.params.id as string
const activeTab = ref<'board' | 'docs' | 'chats'>('board')

const api = useApi()
const toast = useAppToast()
const boardStore = useBoardStore()
const cardPopup = useCardPopupStore()
const projectName = ref('')
const projectArchived = ref(false)

// Card popups are rendered globally (CardPopupLayer, in layouts/default.vue) and
// have no view of this page's projectArchived — sync it into the store so
// CardPopup can gate editing without a prop chain through the layout.
watch(projectArchived, archived => cardPopup.setProjectArchived(projectId, archived), { immediate: true })

const showMembersPanel = ref(false)
const showNarrativeModal = ref(false)

const presence = usePresence()
const presenceStore = usePresenceStore()
const onlineUsers = computed(() => presenceStore.onlineUsers.get(projectId) ?? [])

function viewingCardNumber(userId: string): number | string | null {
  const cardId = presenceStore.focusedCards.get(userId)
  if (!cardId) return null
  for (const cards of boardStore.cardsByColumn.values()) {
    const found = cards.find(c => c.id === cardId)
    if (found) return found.cardNumber
  }
  return null
}

const AVATAR_COLORS = ['#6366f1', '#8b5cf6', '#ec4899', '#f43f5e', '#f97316', '#eab308', '#22c55e', '#14b8a6', '#06b6d4', '#3b82f6']
function hashColor(id: string): string {
  let hash = 0
  for (let i = 0; i < id.length; i++) hash += id.charCodeAt(i)
  return AVATAR_COLORS[hash % AVATAR_COLORS.length]!
}

async function handleRestore() {
  try {
    await api.POST(ApiRoutes.Projects.toggleArchive(projectId))
    projectArchived.value = false
    toast.success('Project restored')
    boardStore.fetchBoard(projectId)
  } catch {
    toast.error('Failed to restore project')
  }
}

onMounted(async () => {
  presence.connect(projectId)
  try {
    const { data } = await api.GET(ApiRoutes.Projects.detail(projectId))
    if (data) {
      const project = data as components['schemas']['ProjectResponse']
      projectName.value = project.name
      projectArchived.value = !!project.archivedAt
    }
  } catch {
    toast.error('Failed to load project details')
  }
})

onBeforeUnmount(() => {
  presence.disconnect(projectId)
})

watch(activeTab, (tab) => {
  useHead({
    bodyAttrs: {
      class: tab === 'board' ? 'md:overflow-hidden' : ''
    }
  })
}, { immediate: true })
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0 min-w-0">
    <!-- Project header: always visible so it survives tab switches -->
    <div class="flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-gray-700">
      <div class="flex items-center gap-2 min-w-0">
        <UButton
          icon="i-lucide-arrow-left"
          variant="ghost"
          size="sm"
          :to="UiRoutes.Projects.List"
          aria-label="Back to projects"
        />
        <h1 class="text-xl font-bold truncate">
          {{ projectName || 'Project' }}
        </h1>
        <UButton
          variant="ghost"
          size="sm"
          icon="i-lucide-sparkles"
          title="View AI narrative"
          @click="showNarrativeModal = true"
        />
        <UBadge
          v-if="projectArchived"
          variant="subtle"
          size="xs"
          color="neutral"
        >
          Archived
        </UBadge>
      </div>
      <div class="flex items-center gap-1">
        <UPopover v-if="onlineUsers.length > 0">
          <button
            type="button"
            class="flex items-center -space-x-1.5 ml-2 cursor-pointer"
            :aria-label="`${onlineUsers.length} online`"
          >
            <span
              v-for="u in onlineUsers"
              :key="u.userId"
              class="inline-flex items-center justify-center size-6 rounded-full text-xs font-medium text-white ring-2 ring-white dark:ring-gray-900"
              :style="{ backgroundColor: hashColor(u.userId) }"
            >
              {{ (u.username[0] ?? '').toUpperCase() }}
            </span>
          </button>

          <template #content>
            <div class="w-56 py-1">
              <div class="px-3 py-1.5 text-xs font-medium text-muted uppercase">
                Online — {{ onlineUsers.length }}
              </div>
              <div
                v-for="u in onlineUsers"
                :key="u.userId"
                class="px-3 py-1.5 flex items-center gap-2 text-sm"
              >
                <span
                  class="inline-flex items-center justify-center size-5 rounded-full text-xs font-medium text-white shrink-0"
                  :style="{ backgroundColor: hashColor(u.userId) }"
                >
                  {{ (u.username[0] ?? '').toUpperCase() }}
                </span>
                <span class="truncate">{{ u.username }}</span>
                <span
                  v-if="viewingCardNumber(u.userId)"
                  class="text-xs text-muted shrink-0 ml-auto"
                >viewing #{{ viewingCardNumber(u.userId) }}</span>
              </div>
            </div>
          </template>
        </UPopover>
        <UButton
          v-if="projectArchived"
          variant="ghost"
          size="sm"
          icon="i-lucide-archive-restore"
          title="Restore project"
          @click="handleRestore"
        />
        <UButton
          variant="ghost"
          size="sm"
          icon="i-lucide-users"
          title="Members"
          @click="showMembersPanel = !showMembersPanel"
        />
        <UButton
          variant="ghost"
          size="sm"
          icon="i-lucide-refresh-cw"
          title="Refresh"
          @click="boardStore.fetchBoard(projectId)"
        />
      </div>
    </div>

    <div
      v-if="showMembersPanel"
      class="px-4 pt-3"
    >
      <MemberManagementPanel
        :project-id="projectId"
        @update="boardStore.fetchMembers(projectId)"
      />
    </div>

    <!-- Tab bar -->
    <div class="flex items-center gap-1 px-4 pt-2 border-b border-gray-200 dark:border-gray-700">
      <UButton
        :variant="activeTab === 'board' ? 'solid' : 'ghost'"
        size="sm"
        @click="activeTab = 'board'"
      >
        <UIcon
          name="i-lucide-columns-3"
          class="size-4 mr-1"
        />
        Board
      </UButton>
      <UButton
        :variant="activeTab === 'docs' ? 'solid' : 'ghost'"
        size="sm"
        @click="activeTab = 'docs'"
      >
        <UIcon
          name="i-lucide-file-text"
          class="size-4 mr-1"
        />
        Docs
      </UButton>
      <UButton
        :variant="activeTab === 'chats' ? 'solid' : 'ghost'"
        size="sm"
        @click="activeTab = 'chats'"
      >
        <UIcon
          name="i-lucide-messages-square"
          class="size-4 mr-1"
        />
        Chats
      </UButton>
    </div>

    <!-- Tab content -->
    <ProjectBoard
      v-if="activeTab === 'board'"
      :project-id="projectId"
      :project-archived="projectArchived"
      :presence="presence"
      :external-modal-open="showMembersPanel || showNarrativeModal"
    />
    <ProjectDocuments
      v-else-if="activeTab === 'docs'"
      :project-id="projectId"
    />
    <ProjectChatsTab
      v-else-if="activeTab === 'chats'"
      :project-id="projectId"
    />

    <ProjectNarrativeModal
      v-if="showNarrativeModal"
      :project-id="projectId"
      @close="showNarrativeModal = false"
    />
  </div>
</template>
