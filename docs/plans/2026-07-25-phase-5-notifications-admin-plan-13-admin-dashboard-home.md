# Plan 13: Admin Dashboard Home Page

**Branch:** `task/admin-dashboard-home`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 13

## Task

Create `/admin` index page with summary cards (total users, projects, cards, health status), recent audit log entries, and quick-action buttons linking to admin sub-pages.

## Files to create

- `src/web-ui/app/pages/admin/index.vue`

## Files to modify

None — routes already defined in Task 9.

## Implementation steps

### Step 1: Create admin dashboard home page

Create `src/web-ui/app/pages/admin/index.vue`:

```vue
<script setup lang="ts">
definePageMeta({ middleware: ['auth'] })

const { user } = useAuth()
if (!user.value?.isAdmin) {
  navigateTo('/projects')
}

const api = useApi()
const toast = useToast()

interface DashboardStats {
  totalUsers: number
  activeUsers: number
  disabledUsers: number
  totalProjects: number
  activeProjects: number
  archivedProjects: number
  totalCards: number
  healthStatus: string
}

const stats = ref<DashboardStats>({
  totalUsers: 0, activeUsers: 0, disabledUsers: 0,
  totalProjects: 0, activeProjects: 0, archivedProjects: 0,
  totalCards: 0, healthStatus: 'Unknown',
})

const recentAudit = ref<any[]>([])
const loading = ref(false)

// AdminService.ListUsersAsync/ProjectService.GetAllAsync return totalCount but not an
// active/disabled or active/archived breakdown — fetch one page big enough to cover
// realistic install sizes and derive the breakdown client-side. Revisit with a dedicated
// counts endpoint if installs grow past this.
const DASHBOARD_BREAKDOWN_PAGE_SIZE = 500

async function loadDashboard() {
  loading.value = true
  try {
    const usersPage = await api.GET<{ items: any[], totalCount: number }>(
      ApiRoutes.Admin.usersList(0, DASHBOARD_BREAKDOWN_PAGE_SIZE))
    const activeUsers = usersPage.items.filter(u => !u.isDisabled).length
    const disabledUsers = usersPage.items.filter(u => u.isDisabled).length

    const projects = await api.GET<{ items: any[], totalCount: number }>(
      ApiRoutes.Admin.projectsList(0, DASHBOARD_BREAKDOWN_PAGE_SIZE))
    const activeProjects = projects.items.filter(p => !p.archivedAt).length
    const archivedProjects = projects.items.filter(p => p.archivedAt).length

    const audit = await api.GET<{ items: any[] }>(`${ApiRoutes.Admin.auditLog()}?take=10`)
    recentAudit.value = audit.items

    stats.value = {
      totalUsers: usersPage.totalCount,
      activeUsers,
      disabledUsers,
      totalProjects: projects.totalCount,
      activeProjects,
      archivedProjects,
      totalCards: 0, // no card-count endpoint exists — leave off the card stat card, or add one if this matters
      healthStatus: 'Unknown',
    }
  } catch (e: any) {
    toast.add({ title: e.message || 'Failed to load dashboard', color: 'error' })
  } finally {
    loading.value = false
  }
}

function formatDate(ts: string): string {
  return new Date(ts).toLocaleString()
}

onMounted(() => loadDashboard())
</script>

<template>
  <div class="p-6">
    <h1 class="text-2xl font-bold mb-6">Admin Dashboard</h1>

    <!-- Summary Cards -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
      <UCard>
        <template #header>
          <h3 class="font-semibold">Users</h3>
        </template>
        <p class="text-3xl font-bold">{{ stats.totalUsers }}</p>
        <p class="text-sm text-gray-500">{{ stats.activeUsers }} active · {{ stats.disabledUsers }} disabled</p>
      </UCard>

      <UCard>
        <template #header>
          <h3 class="font-semibold">Projects</h3>
        </template>
        <p class="text-3xl font-bold">{{ stats.totalProjects }}</p>
        <p class="text-sm text-gray-500">{{ stats.activeProjects }} active · {{ stats.archivedProjects }} archived</p>
      </UCard>

      <UCard>
        <template #header>
          <h3 class="font-semibold">Cards</h3>
        </template>
        <p class="text-3xl font-bold">{{ stats.totalCards }}</p>
        <p class="text-sm text-gray-500">Across all projects</p>
      </UCard>

      <UCard>
        <template #header>
          <h3 class="font-semibold">Health</h3>
        </template>
        <div class="flex items-center gap-2">
          <span class="w-3 h-3 rounded-full bg-green-500" />
          <span class="text-sm">{{ stats.healthStatus }}</span>
        </div>
      </UCard>
    </div>

    <!-- Quick Actions -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-3 mb-8">
      <UButton block to="/admin/users" label="Manage Users" color="neutral" />
      <UButton block to="/admin/projects" label="All Projects" color="neutral" />
      <UButton block to="/admin/settings" label="System Settings" color="neutral" />
      <UButton block to="/admin/audit-log" label="Audit Log" color="neutral" />
    </div>

    <!-- Recent Audit Log -->
    <h2 class="text-lg font-semibold mb-3">Recent Audit Log</h2>
    <UTable :rows="recentAudit" :loading="loading">
      <template #timestamp-data="{ row }">
        <span class="text-sm">{{ formatDate(row.timestamp) }}</span>
      </template>
      <template #entityId-data="{ row }">
        <code class="text-xs">{{ row.entityId?.substring(0, 8) }}...</code>
      </template>
    </UTable>

    <UButton
      v-if="recentAudit.length > 0"
      to="/admin/audit-log"
      label="View Full Audit Log"
      color="neutral"
      variant="ghost"
      class="mt-3"
    />
  </div>
</template>
```

### Step 2: Verify

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
```

## Verification

- `pnpm typecheck` — no errors
- `pnpm lint` — no errors
- `pnpm build` — no errors
- Manual: login as admin, navigate to `/admin`, see summary cards, quick-action buttons, recent audit entries

## Dependencies

- Task 9 (Admin user management page)
- Task 10 (System settings page)
- Task 12 (Audit log page)
- Task 11 (Audit log API for recent entries)