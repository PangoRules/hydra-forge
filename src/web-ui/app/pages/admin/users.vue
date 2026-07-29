<script setup lang="ts">
import { ApiRoutes, UiRoutes } from '~/lib/routes'
import DataTable from '~/components/shared/DataTable.vue'
import UserCreateModal from '~/components/admin/UserCreateModal.vue'
import UserResetPasswordModal from '~/components/admin/UserResetPasswordModal.vue'

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
const pageSize = ref(20)

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
watch(pageSize, () => {
  page.value = 1
  loadUsers()
})

async function loadUsers() {
  loading.value = true
  try {
    const { data, error } = await api.GET<{ items: UserRow[], totalCount: number }>(
      ApiRoutes.Admin.usersList((page.value - 1) * pageSize.value, pageSize.value, search.value || undefined))
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
const resetPasswordTarget = ref<string | null>(null)

function openResetPassword(userId: string) {
  resetPasswordTarget.value = userId
}

function onUserCreated() {
  loadUsers()
  toast.add({ title: 'User created', color: 'success' })
}

function onPasswordReset() {
  resetPasswordTarget.value = null
  toast.add({ title: 'Password reset', color: 'success' })
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

    <DataTable
      :data="users"
      :columns="columns"
      :loading="loading"
      :page="page"
      :page-size="pageSize"
      :total-count="totalCount"
      :row-key="(item: UserRow) => item.id"
      @update:page="page = $event"
      @update:page-size="pageSize = $event"
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

      <template #card="{ item }">
        <UCard>
          <div class="flex items-center justify-between gap-2 mb-2">
            <div class="min-w-0">
              <p class="font-medium truncate">
                {{ item.username }}
              </p>
              <p class="text-xs text-muted truncate">
                {{ item.name }}
              </p>
            </div>
            <UBadge
              v-if="item.isAdmin"
              color="info"
            >
              Admin
            </UBadge>
          </div>
          <p class="text-sm text-muted truncate mb-2">
            {{ item.email }}
          </p>
          <UBadge
            :color="item.isDisabled ? 'error' : 'success'"
            class="mb-3"
          >
            {{ item.isDisabled ? 'Disabled' : 'Active' }}
          </UBadge>
          <div class="flex flex-wrap gap-1">
            <UButton
              size="xs"
              color="neutral"
              @click="toggleDisable(item.id, item.isDisabled)"
            >
              {{ item.isDisabled ? 'Enable' : 'Disable' }}
            </UButton>
            <UButton
              size="xs"
              color="neutral"
              @click="toggleAdmin(item.id)"
            >
              {{ item.isAdmin ? 'Remove Admin' : 'Make Admin' }}
            </UButton>
            <UButton
              size="xs"
              color="neutral"
              @click="openResetPassword(item.id)"
            >
              Reset Password
            </UButton>
          </div>
        </UCard>
      </template>
    </DataTable>

    <UserCreateModal
      v-model:open="showCreateModal"
      @created="onUserCreated"
    />

    <UserResetPasswordModal
      v-if="resetPasswordTarget"
      :user-id="resetPasswordTarget"
      @close="resetPasswordTarget = null"
      @reset="onPasswordReset"
    />
  </div>
</template>
