<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'created': []
  'close': []
}>()

const name = ref('')
const description = ref('')
const gitRemoteUrl = ref('')
const gitProvider = ref<string | undefined>(undefined)
const showAdvanced = ref(false)
const loading = ref(false)
const error = ref<string | null>(null)

const selectedMembers = ref<Array<{ id: string, username: string }>>([])
const searchQuery = ref('')
const searchResults = ref<Array<{ id: string, username: string }>>([])
const searching = ref(false)

const gitProviders = [
  { label: 'GitHub', value: 'github' },
  { label: 'GitLab', value: 'gitlab' },
  { label: 'Gitea', value: 'gitea' },
  { label: 'Self-hosted', value: 'self-hosted' }
]

const api = useApi()
let searchTimer: ReturnType<typeof setTimeout> | null = null

function onClose() {
  emit('update:open', false)
  emit('close')
}

function resetForm() {
  name.value = ''
  description.value = ''
  gitRemoteUrl.value = ''
  gitProvider.value = undefined
  showAdvanced.value = false
  error.value = null
  selectedMembers.value = []
  searchQuery.value = ''
  searchResults.value = []
}

watch(() => props.open, (val) => {
  if (!val) resetForm()
})

async function searchUsers() {
  if (searchQuery.value.length < 2) {
    searchResults.value = []
    return
  }
  searching.value = true
  try {
    const { data, error: searchError } = await api.GET(ApiRoutes.Users.search(searchQuery.value))
    if (searchError) throw searchError
    const existingIds = new Set(selectedMembers.value.map(m => m.id))
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

function addMember(user: { id: string, username: string }) {
  selectedMembers.value.push(user)
  searchQuery.value = ''
  searchResults.value = []
}

function removeMember(userId: string) {
  selectedMembers.value = selectedMembers.value.filter(m => m.id !== userId)
}

async function handleSubmit() {
  error.value = null
  loading.value = true
  try {
    const { data, error: apiError } = await api.POST(ApiRoutes.Projects.create(), {
      body: {
        name: name.value,
        description: description.value,
        gitRemoteUrl: gitRemoteUrl.value || null,
        gitProvider: gitProvider.value ?? null
      }
    })
    if (apiError) throw apiError

    // Add selected members
    const projectId = (data as { id: string }).id
    for (const member of selectedMembers.value) {
      await api.POST(ApiRoutes.Projects.members(projectId), {
        body: { userId: member.id, role: 2 }
      })
    }

    onClose()
    emit('created')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to create project'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <AppModal
    :open="open"
    title="Create Project"
    :loading="loading"
    :error="error"
    width="sm:max-w-lg"
    @update:open="emit('update:open', $event)"
    @close="onClose"
  >
    <template #body>
      <form
        class="space-y-4 p-4"
        @submit.prevent="handleSubmit"
      >
        <UFormField
          label="Project Name"
          required
        >
          <UInput
            v-model="name"
            placeholder="My Project"
            required
            class="w-full"
          />
        </UFormField>

        <UFormField
          label="Description"
          class="w-full"
        >
          <UTextarea
            v-model="description"
            placeholder="Optional description"
            class="w-full"
          />
        </UFormField>

        <!-- Members -->
        <div>
          <UFormField
            label="Add Members"
            class="w-full"
          >
            <UInput
              v-model="searchQuery"
              placeholder="Search users to add..."
              size="sm"
              :loading="searching"
              @input="onSearchInput"
            />
            <div
              v-if="searchResults.length > 0"
              class="relative"
            >
              <div class="absolute z-10 top-1 left-0 right-0 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg max-h-40 overflow-y-auto">
                <button
                  v-for="user in searchResults"
                  :key="user.id"
                  type="button"
                  class="w-full text-left px-3 py-2 text-sm hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center gap-2"
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
          </UFormField>
          <div
            v-if="selectedMembers.length > 0"
            class="flex flex-wrap gap-2 mt-2"
          >
            <UBadge
              v-for="member in selectedMembers"
              :key="member.id"
              variant="subtle"
              class="flex items-center gap-1"
            >
              {{ member.username }}
              <button
                type="button"
                class="hover:text-red-500"
                @click="removeMember(member.id)"
              >
                <UIcon
                  name="i-lucide-x"
                  class="size-3"
                />
              </button>
            </UBadge>
          </div>
        </div>

        <!-- Advanced -->
        <div>
          <UButton
            variant="ghost"
            size="sm"
            class="mb-2"
            @click="showAdvanced = !showAdvanced"
          >
            <UIcon
              :name="showAdvanced ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'"
              class="size-4"
            />
            <span>Advanced</span>
          </UButton>

          <div
            v-if="showAdvanced"
            class="space-y-4"
          >
            <UFormField
              label="Git Remote URL"
              class="w-full"
            >
              <UInput
                v-model="gitRemoteUrl"
                placeholder="https://github.com/user/repo.git"
                class="w-full"
              />
            </UFormField>

            <UFormField
              label="Git Provider"
              class="w-full"
            >
              <div class="relative">
                <USelect
                  v-model="gitProvider"
                  :items="gitProviders"
                  placeholder="Select provider"
                  class="w-full"
                />
                <UButton
                  v-if="gitProvider"
                  variant="ghost"
                  size="sm"
                  class="absolute inset-y-0 right-6 px-2 hover:!bg-transparent text-gray-400 hover:text-gray-600"
                  icon="i-lucide-x"
                  @click="gitProvider = undefined"
                />
              </div>
            </UFormField>
          </div>
        </div>
      </form>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          variant="outline"
          @click="onClose"
        >
          Cancel
        </UButton>
        <UButton
          type="submit"
          :loading="loading"
          @click="handleSubmit"
        >
          Create
        </UButton>
      </div>
    </template>
  </AppModal>
</template>
