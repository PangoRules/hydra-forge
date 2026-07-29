<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'

definePageMeta({ middleware: ['auth'] })

interface UserRow {
  id: string
  username: string
  name: string
  email: string
  isAdmin: boolean
  isDisabled: boolean
  lastLoginAt: string | null
  createdAt: string
}

const user = useAuthStore().user
if (!user?.isAdmin) {
  navigateTo('/projects')
}

const api = useApi()
const toast = useToast()

const users = ref<UserRow[]>([])
const totalCount = ref(0)
const loading = ref(false)
const search = ref('')
const page = ref(0)
const pageSize = 20

async function loadUsers() {
  loading.value = true
  try {
    const { data, error } = await api.GET<{ items: UserRow[], totalCount: number }>(
      ApiRoutes.Admin.usersList(page.value * pageSize, pageSize, search.value || undefined))
    if (error) throw error
    users.value = data!.items
    totalCount.value = data!.totalCount
  } catch (e) {
    toast.add({ title: (e as Error).message || 'Failed to load users', color: 'error' })
  } finally {
    loading.value = false
  }
}

async function toggleDisable(userId: string, currentlyDisabled: boolean) {
  try {
    if (currentlyDisabled) {
      await api.PATCH(ApiRoutes.Admin.userEnable(userId))
    } else {
      await api.PATCH(ApiRoutes.Admin.userDisable(userId))
    }
    await loadUsers()
    toast.add({ title: `User ${currentlyDisabled ? 'enabled' : 'disabled'}`, color: 'success' })
  } catch (e) {
    toast.add({ title: (e as Error).message || 'Action failed', color: 'error' })
  }
}

async function toggleAdmin(userId: string) {
  try {
    await api.PATCH(ApiRoutes.Admin.userRole(userId))
    await loadUsers()
    toast.add({ title: 'Admin role toggled', color: 'success' })
  } catch (e) {
    toast.add({ title: (e as Error).message || 'Action failed', color: 'error' })
  }
}

const showCreateModal = ref(false)
const newUser = reactive({ username: '', password: '', name: '', lastName: '', email: '', isAdmin: false })

async function createUser() {
  try {
    await api.POST(ApiRoutes.Admin.userCreate(), { body: { ...newUser } })
    showCreateModal.value = false
    await loadUsers()
    toast.add({ title: 'User created', color: 'success' })
  } catch (e) {
    toast.add({ title: (e as Error).message || 'Create failed', color: 'error' })
  }
}

onMounted(() => loadUsers())
</script>

<template>
  <div class="p-6">
    <div class="flex items-center justify-between mb-4">
      <h1 class="text-2xl font-bold">
        Users
      </h1>
      <UButton
        label="Create User"
        @click="showCreateModal = true"
      />
    </div>

    <UInput
      v-model="search"
      placeholder="Search users..."
      class="mb-4"
      @update:model-value="loadUsers"
    />

    <UTable
      :data="users"
      :loading="loading"
    >
      <template #disabled-cell="{ row }">
        <UBadge :color="row.original.isDisabled ? 'error' : 'success'">
          {{ row.original.isDisabled ? 'Disabled' : 'Active' }}
        </UBadge>
      </template>
      <template #isAdmin-cell="{ row }">
        <UBadge
          v-if="row.original.isAdmin"
          color="info"
        >
          Admin
        </UBadge>
        <span
          v-else
          class="text-gray-400"
        >—</span>
      </template>
      <template #actions-cell="{ row }">
        <div class="flex gap-1">
          <UButton
            size="xs"
            color="neutral"
            @click="toggleDisable(row.original.id, row.original.isDisabled)"
          >
            {{ row.original.isDisabled ? 'Enable' : 'Disable' }}
          </UButton>
          <UButton
            size="xs"
            color="neutral"
            @click="toggleAdmin(row.original.id)"
          >
            {{ row.original.isAdmin ? 'Remove Admin' : 'Make Admin' }}
          </UButton>
        </div>
      </template>
    </UTable>

    <p class="text-sm text-gray-500 mt-3">
      {{ totalCount }} total users
    </p>

    <UModal v-model:open="showCreateModal">
      <template #body>
        <div class="p-4 space-y-3">
          <h2 class="text-lg font-semibold">
            Create User
          </h2>
          <UInput
            v-model="newUser.username"
            label="Username"
          />
          <UInput
            v-model="newUser.password"
            label="Password"
            type="password"
          />
          <UInput
            v-model="newUser.name"
            label="First Name"
          />
          <UInput
            v-model="newUser.lastName"
            label="Last Name"
          />
          <UInput
            v-model="newUser.email"
            label="Email"
          />
          <UCheckbox
            v-model="newUser.isAdmin"
            label="Admin"
          />
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2 p-4">
          <UButton
            label="Cancel"
            color="neutral"
            @click="showCreateModal = false"
          />
          <UButton
            label="Create"
            @click="createUser"
          />
        </div>
      </template>
    </UModal>
  </div>
</template>
