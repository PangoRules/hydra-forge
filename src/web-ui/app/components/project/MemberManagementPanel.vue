<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'

type MemberResponse = components['schemas']['MemberResponse']

const props = defineProps<{
  projectId: string
}>()

const emit = defineEmits<{
  update: []
}>()

const api = useApi()
const toast = useAppToast()
const authStore = useAuthStore()

const members = ref<MemberResponse[]>([])
const loading = ref(false)
const searchQuery = ref('')
const searchResults = ref<Array<{ id: string, username: string }>>([])
const searching = ref(false)
const addingMember = ref(false)
const selectedRole = ref(2) // Member role
const selfRemoveConfirm = ref(false)
const pendingMemberId = ref<string | null>(null)
const pendingUsername = ref<string | null>(null)

let searchTimer: ReturnType<typeof setTimeout> | null = null
const searchContainerRef = ref<HTMLElement | null>(null)

function onDocumentClick(e: MouseEvent) {
  if (
    searchResults.value.length > 0
    && searchContainerRef.value
    && !searchContainerRef.value.contains(e.target as Node)
  ) {
    searchResults.value = []
  }
}

function onSearchKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') searchResults.value = []
}

onMounted(() => document.addEventListener('click', onDocumentClick))
onUnmounted(() => document.removeEventListener('click', onDocumentClick))

async function fetchMembers() {
  loading.value = true
  try {
    const { data, error } = await api.GET(ApiRoutes.Projects.members(props.projectId))
    if (error) throw error
    members.value = (data as MemberResponse[]) ?? []
  } catch {
    toast.error('Failed to load members')
  } finally {
    loading.value = false
  }
}

async function searchUsers() {
  const q = searchQuery.value.trim()
  searching.value = true
  try {
    const { data, error } = await api.GET(ApiRoutes.Users.search(q, 10, props.projectId))
    if (error) throw error
    searchResults.value = (data as Array<{ id: string, username: string }>) ?? []
  } catch {
    searchResults.value = []
  } finally {
    searching.value = false
  }
}

function onSearchInput() {
  if (searchTimer) clearTimeout(searchTimer)
  if (searchQuery.value.trim() === '' && searchResults.value.length > 0) return
  searchTimer = setTimeout(searchUsers, 300)
}

async function addMember(user: { id: string, username: string }) {
  addingMember.value = true
  try {
    const { error } = await api.POST(ApiRoutes.Projects.members(props.projectId), {
      body: { userId: user.id, role: selectedRole.value }
    })
    if (error) throw error
    toast.success(`${user.username} added to project`)
    searchQuery.value = ''
    searchResults.value = []
    await fetchMembers()
    emit('update')
  } catch (e: unknown) {
    toast.error(e instanceof Error ? e.message : 'Failed to add member')
  } finally {
    addingMember.value = false
  }
}

async function removeMember(memberId: string, username: string) {
  const isSelf = memberId === authStore.user?.userId
  const member = members.value.find(m => m.id === memberId)
  const isNonOwner = member ? String(member.role) !== 'Owner' : false
  if (isSelf && isNonOwner) {
    pendingMemberId.value = memberId
    pendingUsername.value = username
    selfRemoveConfirm.value = true
    return
  }
  await doRemoveMember(memberId, username)
}

async function doRemoveMember(memberId: string, username: string) {
  try {
    const { error } = await api.DELETE(ApiRoutes.Projects.member(props.projectId, memberId))
    if (error) throw error
    toast.success(`${username} removed from project`)
    await fetchMembers()
    emit('update')
  } catch (e: unknown) {
    toast.error(e instanceof Error ? e.message : 'Failed to remove member')
  }
}

function roleLabel(role: string | number): string {
  switch (role) {
    case 'Owner':
    case 1: return 'Owner'
    case 'Member':
    case 2: return 'Member'
    default: return String(role)
  }
}

function roleColor(role: string | number): 'warning' | 'error' | 'info' | 'neutral' {
  switch (role) {
    case 'Owner':
    case 1: return 'warning'
    case 'Member':
    case 2: return 'info'
    default: return 'neutral'
  }
}

onMounted(fetchMembers)

function confirmSelfRemove() {
  if (pendingMemberId.value && pendingUsername.value) {
    doRemoveMember(pendingMemberId.value, pendingUsername.value)
  }
  pendingMemberId.value = null
  pendingUsername.value = null
}
</script>

<template>
  <div class="border border-gray-200 dark:border-gray-700 rounded-lg p-4 bg-white dark:bg-gray-900">
    <h3 class="text-sm font-semibold mb-3">
      Project Members
    </h3>

    <!-- Members list -->
    <div
      v-if="loading"
      class="flex justify-center py-4"
    >
      <UIcon
        name="i-lucide-loader"
        class="animate-spin size-5"
      />
    </div>

    <div
      v-else
      class="space-y-2 mb-4"
    >
      <div
        v-for="member in members"
        :key="member.id"
        class="flex items-center justify-between gap-2 text-sm"
      >
        <div class="flex items-center gap-2 min-w-0">
          <UAvatar
            :username="member.username"
            size="xs"
          />
          <span class="truncate">{{ member.username }}</span>
          <UBadge
            :color="roleColor(member.role)"
            variant="subtle"
            size="xs"
            class="shrink-0"
          >
            {{ roleLabel(member.role) }}
          </UBadge>
        </div>
        <UButton
          v-if="String(member.role) !== 'Owner' || members.filter(m => String(m.role) === 'Owner').length > 1"
          variant="ghost"
          size="xs"
          icon="i-lucide-x"
          title="Remove member"
          @click="removeMember(member.id, member.username)"
        />
      </div>
    </div>

    <!-- Add member section -->
    <div class="border-t border-gray-200 dark:border-gray-700 pt-3">
      <h4 class="text-xs font-semibold text-muted mb-2">
        Add Member
      </h4>
      <div
        ref="searchContainerRef"
        class="relative"
      >
        <UInput
          v-model="searchQuery"
          placeholder="Search users..."
          size="sm"
          class="w-full"
          :loading="searching"
          @input="onSearchInput"
          @focus="onSearchInput"
          @keydown="onSearchKeydown"
        />
        <div
          v-if="searchResults.length > 0"
          class="absolute z-10 bottom-full left-0 right-0 mb-1 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg max-h-48 overflow-y-auto"
        >
          <button
            v-for="user in searchResults"
            :key="user.id"
            type="button"
            class="w-full text-left px-3 py-2 text-sm hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center gap-2"
            :disabled="addingMember"
            @click="addMember(user)"
          >
            <UAvatar
              :username="user.username"
              size="xs"
            />
            {{ user.username }}
          </button>
        </div>
      </div>
    </div>
  </div>

  <ConfirmDialog
    v-model:open="selfRemoveConfirm"
    title="Remove yourself from project"
    message="You are about to remove yourself from this project. You will lose access to it and it will be removed from your project list. You will need to contact an admin or the project owner to be reassigned."
    confirm-text="Remove"
    confirm-color="error"
    @confirm="confirmSelfRemove"
  />
</template>
