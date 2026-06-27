<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'

type MemberResponse = components['schemas']['MemberResponse']

const props = defineProps<{
  projectId: string
  initialName: string
  initialDescription: string | null
}>()

const emit = defineEmits<{
  close: []
  updated: []
}>()

const api = useApi()
const toast = useAppToast()

const isOpen = ref(true)

/** Close with animation: set isOpen=false (triggers UModal scale-out 200ms), then emit close */
function closeWithAnimation() {
  if (!isOpen.value) return
  isOpen.value = false
  setTimeout(() => emit('close'), 200)
}

const name = ref(props.initialName)
const description = ref(props.initialDescription ?? '')
const saving = ref(false)

const members = ref<MemberResponse[]>([])
const loadingMembers = ref(false)
const searchQuery = ref('')
const searchResults = ref<Array<{ id: string, username: string }>>([])
const searching = ref(false)
const addingMember = ref(false)
const selectedRole = ref(2)

let searchTimer: ReturnType<typeof setTimeout> | null = null

async function fetchMembers() {
  loadingMembers.value = true
  try {
    const { data, error } = await api.GET(ApiRoutes.Projects.members(props.projectId))
    if (error) throw error
    members.value = (data as MemberResponse[]) ?? []
  } catch {
    toast.error('Failed to load members')
  } finally {
    loadingMembers.value = false
  }
}

async function searchUsers() {
  const q = searchQuery.value.trim()
  if (q.length === 0) {
    // On empty query, fetch initial batch to show available users
    searching.value = true
    try {
      const { data, error } = await api.GET(ApiRoutes.Users.search(''))
      if (error) throw error
      const existingIds = new Set(members.value.map(m => m.userId))
      searchResults.value = ((data as Array<{ id: string, username: string }>) ?? [])
        .filter(u => !existingIds.has(u.id))
    } catch {
      searchResults.value = []
    } finally {
      searching.value = false
    }
    return
  }
  searching.value = true
  try {
    const { data, error } = await api.GET(ApiRoutes.Users.search(q))
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
  // Avoid re-fetching empty results that are already visible (e.g. re-focus)
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
  } catch (e: unknown) {
    toast.error(e instanceof Error ? e.message : 'Failed to remove member')
  }
}

async function saveProject() {
  saving.value = true
  try {
    const { error } = await api.PUT(ApiRoutes.Projects.update(props.projectId), {
      body: { name: name.value, description: description.value || null }
    })
    if (error) throw error
    toast.success('Project updated')
    emit('updated')
  } catch (e: unknown) {
    toast.error(e instanceof Error ? e.message : 'Failed to update project')
  } finally {
    saving.value = false
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
</script>

<template>
  <AppModal
    :open="isOpen"
    title="Edit Project"
    @update:open="closeWithAnimation"
    @close="closeWithAnimation"
  >
    <template #body>
      <div class="space-y-4">
        <div>
          <label class="block text-sm font-medium mb-1">Name</label>
          <UInput
            v-model="name"
            placeholder="Project name"
            class="w-full"
          />
        </div>

        <div>
          <label class="block text-sm font-medium mb-1">Description</label>
          <UTextarea
            v-model="description"
            placeholder="Project description (optional)"
            class="w-full"
            :rows="3"
          />
        </div>

        <div class="border-t border-gray-200 dark:border-gray-700 pt-4">
          <h4 class="text-sm font-semibold mb-3">
            Members
          </h4>

          <div
            v-if="loadingMembers"
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

          <div class="border-t border-gray-200 dark:border-gray-700 pt-3">
            <h5 class="text-xs font-semibold text-muted mb-2">
              Add Member
            </h5>
            <div class="relative">
              <UInput
                v-model="searchQuery"
                placeholder="Search users..."
                size="sm"
                class="w-full"
                :loading="searching"
                @input="onSearchInput"
                @focus="onSearchInput"
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
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          variant="ghost"
          @click="closeWithAnimation"
        >
          Cancel
        </UButton>
        <UButton
          :loading="saving"
          @click="saveProject"
        >
          Save Changes
        </UButton>
      </div>
    </template>
  </AppModal>
</template>
