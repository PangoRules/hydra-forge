<script setup lang="ts">
import { ApiRoutes, UiRoutes } from '~/lib/routes'
import type { TableColumn } from '@nuxt/ui'
import DataTable from '~/components/shared/DataTable.vue'

definePageMeta({ middleware: ['auth'] })

const user = useAuthStore().user
if (!user?.isAdmin) navigateTo(UiRoutes.Chats)

const api = useApi()
const toast = useAppToast()

interface DashboardStats {
  totalUsers: number
  activeUsers: number
  disabledUsers: number
  totalProjects: number
  activeProjects: number
  archivedProjects: number
}

interface UserRow {
  id: string
  isDisabled: boolean
}

interface ProjectRow {
  id: string
  archivedAt: string | null
}

interface AuditEntry {
  id: string
  actorName: string
  entityType: string
  action: string
  timestamp: string
}

const stats = ref<DashboardStats>({
  totalUsers: 0, activeUsers: 0, disabledUsers: 0,
  totalProjects: 0, activeProjects: 0, archivedProjects: 0
})
const recentAudit = ref<AuditEntry[]>([])
const auditTotalCount = ref(0)
const loading = ref(false)

const DASHBOARD_BREAKDOWN_PAGE_SIZE = 500

const columns: TableColumn<AuditEntry>[] = [
  { accessorKey: 'timestamp', header: 'Timestamp' },
  { accessorKey: 'actorName', header: 'Actor' },
  { accessorKey: 'entityType', header: 'Entity Type' },
  { accessorKey: 'action', header: 'Action' }
]

function formatDate(ts: string): string {
  return new Date(ts).toLocaleString()
}

async function loadDashboard() {
  loading.value = true
  try {
    const { data: usersPage, error: usersError } = await api.GET<{ items: UserRow[], totalCount: number }>(
      ApiRoutes.Admin.usersList(0, DASHBOARD_BREAKDOWN_PAGE_SIZE)
    )
    if (usersError) throw usersError

    const { data: projectsPage, error: projectsError } = await api.GET<{ items: ProjectRow[], totalCount: number }>(
      ApiRoutes.Admin.projectsList(0, DASHBOARD_BREAKDOWN_PAGE_SIZE)
    )
    if (projectsError) throw projectsError

    const { data: audit, error: auditError } = await api.GET<{ items: AuditEntry[], totalCount: number }>(
      `${ApiRoutes.Admin.auditLog()}?take=10`
    )
    if (auditError) throw auditError

    stats.value = {
      totalUsers: usersPage!.totalCount,
      activeUsers: usersPage!.items.filter(u => !u.isDisabled).length,
      disabledUsers: usersPage!.items.filter(u => u.isDisabled).length,
      totalProjects: projectsPage!.totalCount,
      activeProjects: projectsPage!.items.filter(p => !p.archivedAt).length,
      archivedProjects: projectsPage!.items.filter(p => p.archivedAt).length
    }
    recentAudit.value = audit!.items
    auditTotalCount.value = audit!.totalCount
  } catch (e) {
    toast.showApiError(e as Error)
  } finally {
    loading.value = false
  }
}

onMounted(() => loadDashboard())
</script>

<template>
  <div class="p-6">
    <h1 class="text-2xl font-bold mb-6">
      Admin Dashboard
    </h1>

    <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-8">
      <UCard>
        <template #header>
          <h3 class="font-semibold">
            Users
          </h3>
        </template>
        <p class="text-3xl font-bold">
          {{ stats.totalUsers }}
        </p>
        <p class="text-sm text-muted">
          {{ stats.activeUsers }} active · {{ stats.disabledUsers }} disabled
        </p>
      </UCard>

      <UCard>
        <template #header>
          <h3 class="font-semibold">
            Projects
          </h3>
        </template>
        <p class="text-3xl font-bold">
          {{ stats.totalProjects }}
        </p>
        <p class="text-sm text-muted">
          {{ stats.activeProjects }} active · {{ stats.archivedProjects }} archived
        </p>
      </UCard>

      <UCard>
        <template #header>
          <h3 class="font-semibold">
            Health
          </h3>
        </template>
        <div class="flex items-center gap-2">
          <span class="w-3 h-3 rounded-full bg-success" />
          <span class="text-sm">Online</span>
        </div>
      </UCard>
    </div>

    <div class="grid grid-cols-2 md:grid-cols-4 gap-3 mb-8">
      <UButton
        block
        :to="UiRoutes.Admin.Users"
        label="Manage Users"
        color="neutral"
      />
      <UButton
        block
        :to="UiRoutes.Projects.List"
        label="All Projects"
        color="neutral"
      />
      <UButton
        block
        :to="UiRoutes.Admin.Settings"
        label="System Settings"
        color="neutral"
      />
      <UButton
        block
        :to="UiRoutes.Admin.AuditLog"
        label="Audit Log"
        color="neutral"
      />
    </div>

    <h2 class="text-lg font-semibold mb-3">
      Recent Audit Log
    </h2>
    <DataTable
      :data="recentAudit"
      :columns="columns"
      :loading="loading"
      :page="1"
      :page-size="10"
      :total-count="auditTotalCount"
      :row-key="(item: AuditEntry) => item.id"
      hide-footer
    >
      <template #timestamp-cell="{ row }">
        <span class="text-sm">{{ formatDate(row.original.timestamp) }}</span>
      </template>
    </DataTable>
  </div>
</template>
