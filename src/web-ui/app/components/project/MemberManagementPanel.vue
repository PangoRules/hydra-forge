<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'

type MemberResponse = components['schemas']['MemberResponse']

const props = defineProps<{
  projectId: string
}>()

const emit = defineEmits<{
  update: []
}>()

const api = useApi()
const toast = useAppToast()

const members = ref<MemberResponse[]>([])
const loading = ref(false)
const searchQuery = ref('')
const searchResults = ref<Array<{ id: string, username: string }>>([])
const searching = ref(false)
const addingMember = ref(false)
const selectedRole = ref(2) // Member role

let searchTimer: ReturnType<typeof setTimeout> | null = null

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
  if (searchQuery.value.length < 2) {
    searchResults.value = []
    return
  }
  searching.value = true
  try {
    const { data, error } = await api.GET(ApiRoutes.Users.search(searchQuery.value))
    if (error) throw error
    const existingIds = new Set(members.value.map(m => m.userId))
    searchResults.value = ((data as Array<{ id: string, username: string }>) ?? [])
      .filter(u => !existingIds.has(u.id))
  } catch {
    searchResults.value = []
  } finally {
    searching.value = false
  }
}

function onSearchInput() {
  if (searchTimer) clearTimeout(searchTimer)
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

function roleLabel(role: number): string {
  switch (role) {
    case 0: return 'Owner'
    case 1: return 'Admin'
    case 2: return 'Member'
    default: return 'Unknown'
  }
}

function roleColor(role: number): 'warning' | 'error' | 'info' | 'neutral' {
  switch (role) {
    case 0: return 'warning'
    case 1: return 'error'
    case 2: return 'info'
    default: return 'neutral'
  }
}

onMounted(fetchMembers)
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
          v-if="member.role !== 0 || members.filter(m => m.role === 0).length > 1"
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
      <div class="relative">
        <UInput
          v-model="searchQuery"
          placeholder="Search users..."
          size="sm"
          class="w-full"
          :loading="searching"
          @input="onSearchInput"
        />
        <div
          v-if="searchResults.length > 0"
          class="absolute z-10 top-full left-0 right-0 mt-1 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg max-h-48 overflow-y-auto"
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
</template>
