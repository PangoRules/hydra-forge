<script setup lang="ts">
import { ApiRoutes, UiRoutes } from '~/lib/routes'

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
  navigateTo(UiRoutes.Chats)
}

const api = useApi()
const toast = useToast()

const users = ref<UserRow[]>([])
const totalCount = ref(0)
const loading = ref(false)
const search = ref('')
const page = ref(1)
const pageSize = 20

const columns = [
  { accessorKey: 'username', header: 'Username' },
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'email', header: 'Email' },
  { accessorKey: 'isAdmin', header: 'Role' },
  { accessorKey: 'isDisabled', header: 'Status' },
  { accessorKey: 'actions', header: 'Actions', enableSorting: false }
]

watch(search, () => {
  page.value = 1
  loadUsers()
})

async function loadUsers() {
  loading.value = true
  try {
    const { data, error } = await api.GET<{ items: UserRow[], totalCount: number }>(
      ApiRoutes.Admin.usersList((page.value - 1) * pageSize, pageSize, search.value || undefined))
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
const creating = ref(false)
const newUser = reactive({ username: '', password: '', name: '', lastName: '', email: '', isAdmin: false })

const showResetPasswordModal = ref(false)
const resetPasswordTarget = ref<string | null>(null)
const newPassword = ref('')
const resetting = ref(false)

function openResetPassword(userId: string) {
  resetPasswordTarget.value = userId
  newPassword.value = ''
  showResetPasswordModal.value = true
}

async function resetPassword() {
  if (!resetPasswordTarget.value) return
  resetting.value = true
  try {
    await api.POST(ApiRoutes.Admin.userResetPassword(resetPasswordTarget.value), { body: { newPassword: newPassword.value } })
    showResetPasswordModal.value = false
    toast.add({ title: 'Password reset', color: 'success' })
  } catch (e) {
    toast.add({ title: (e as Error).message || 'Reset failed', color: 'error' })
  } finally {
    resetting.value = false
  }
}

async function createUser() {
  creating.value = true
  try {
    await api.POST(ApiRoutes.Admin.userCreate(), { body: { ...newUser } })
    showCreateModal.value = false
    await loadUsers()
    toast.add({ title: 'User created', color: 'success' })
  } catch (e) {
    toast.add({ title: (e as Error).message || 'Create failed', color: 'error' })
  } finally {
    creating.value = false
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
    />

    <UTable
      :data="users"
      :columns="columns"
      :loading="loading"
    >
      <template #isDisabled-cell="{ row }">
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
          <UButton
            size="xs"
            color="neutral"
            @click="openResetPassword(row.original.id)"
          >
            Reset Password
          </UButton>
        </div>
      </template>
    </UTable>

    <UPagination
      v-model:page="page"
      :total="totalCount"
      :page-size="pageSize"
      class="mt-4"
      @update:page="loadUsers"
    />

    <p class="text-sm text-gray-500 mt-3">
      {{ totalCount }} total users
    </p>

    <UModal v-model:open="showResetPasswordModal">
      <template #body>
        <div class="p-4 space-y-3">
          <h2 class="text-lg font-semibold">
            Reset Password
          </h2>
          <UInput
            v-model="newPassword"
            type="password"
            placeholder="New password"
          />
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2 p-4">
          <UButton
            label="Cancel"
            color="neutral"
            @click="showResetPasswordModal = false"
          />
          <UButton
            label="Reset"
            :loading="resetting"
            @click="resetPassword"
          />
        </div>
      </template>
    </UModal>

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
            :loading="creating"
            @click="createUser"
          />
        </div>
      </template>
    </UModal>
  </div>
</template>
