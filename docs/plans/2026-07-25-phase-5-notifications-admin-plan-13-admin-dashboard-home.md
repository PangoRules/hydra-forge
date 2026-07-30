# Plan 13: Admin Dashboard Home Page

**Branch:** `task/admin-dashboard-home`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 13

**Updated 2026-07-29:** Original draft below was stale — written before `DataTable`/`useAppToast`/`ApiRoutes.Admin` conventions landed (Plans 9/10/12), and before app nav settled on Chats as the root landing page. Two concrete problems fixed by this revision:

1. `src/web-ui/app/pages/admin/index.vue` already exists but is a placeholder — it immediately `navigateTo`'s away (admin → `/admin/users`, non-admin → `/chats`) and renders nothing. No dashboard UI has actually shipped.
2. Nothing in the sidebar nav (`nav-config.ts`) points at `/admin` — the Admin group only lists Users / System Settings / Audit Log / Reports (disabled). Even once the dashboard page has real content, there is no way to reach it. This plan adds the missing nav entry.

## Task

Replace the `/admin` placeholder redirect with a real dashboard page — summary cards (users, projects, health status), recent audit log entries, quick-action links to admin sub-pages — and add a sidebar nav item so it's reachable.

## Files to modify

- `src/web-ui/app/pages/admin/index.vue` — replace redirect stub with dashboard content
- `src/web-ui/app/lib/nav-config.ts` — add a "Dashboard" item to the Admin group, pointing at `UiRoutes.Admin.Home`

## Files to create

None — `UiRoutes.Admin.Home` (`/admin`) and all `ApiRoutes.Admin.*` endpoints used below already exist (`src/web-ui/app/lib/routes.ts`).

## Implementation steps

### Step 1: Add nav entry

In `src/web-ui/app/lib/nav-config.ts`, add a `Dashboard` item as the **first** entry in the Admin group (before Users), so the group reads top-to-bottom as "where am I → users → settings → audit":

```ts
if (isAdmin) {
  groups.push([
    { label: 'Admin', type: 'label' },
    { label: 'Dashboard', icon: 'i-lucide-layout-dashboard', to: UiRoutes.Admin.Home },
    { label: 'Users', icon: 'i-lucide-users', to: UiRoutes.Admin.Users },
    { label: 'System Settings', icon: 'i-lucide-settings', to: UiRoutes.Admin.Settings },
    { label: 'Audit Log', icon: 'i-lucide-scroll-text', to: UiRoutes.Admin.AuditLog },
    { label: 'Reports', icon: 'i-lucide-bar-chart-3', disabled: true }
  ])
}
```

Named "Dashboard" (not "Home") — the app's actual landing page (root `/`, redirects to Chats) already owns "Home"-shaped meaning; "Dashboard" reads unambiguously as the admin-section overview and won't be confused with it. Use `i-lucide-layout-dashboard` — matches the icon already used for Projects, keep it distinct from the `i-lucide-users`/`i-lucide-settings`/`i-lucide-scroll-text` icons on the other three Admin items.

### Step 2: Replace the dashboard page

Rewrite `src/web-ui/app/pages/admin/index.vue`. Drop the `navigateTo` redirect for admins (non-admins still redirect away — this page is admin-only); use `useAppToast`/`DataTable`/`TableColumn` conventions consistent with `audit-log.vue` and `users.vue`, not the raw `useToast().add()` / v3 `UTable :rows` API in the original draft:

```vue
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
const loading = ref(false)

// AdminService.ListUsersAsync/ProjectService.GetAllAsync return totalCount but not an
// active/disabled or active/archived breakdown — fetch one page big enough to cover
// realistic install sizes and derive the breakdown client-side. Revisit with a dedicated
// counts endpoint if installs grow past this.
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
    const { data: usersPage, error: usersError } = await api.GET<{ items: any[], totalCount: number }>(
      ApiRoutes.Admin.usersList(0, DASHBOARD_BREAKDOWN_PAGE_SIZE)
    )
    if (usersError) throw usersError

    const { data: projectsPage, error: projectsError } = await api.GET<{ items: any[], totalCount: number }>(
      ApiRoutes.Admin.projectsList(0, DASHBOARD_BREAKDOWN_PAGE_SIZE)
    )
    if (projectsError) throw projectsError

    const { data: audit, error: auditError } = await api.GET<{ items: AuditEntry[] }>(
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
      <UButton block :to="UiRoutes.Admin.Users" label="Manage Users" color="neutral" />
      <UButton block :to="UiRoutes.Projects.List" label="All Projects" color="neutral" />
      <UButton block :to="UiRoutes.Admin.Settings" label="System Settings" color="neutral" />
      <UButton block :to="UiRoutes.Admin.AuditLog" label="Audit Log" color="neutral" />
    </div>

    <h2 class="text-lg font-semibold mb-3">
      Recent Audit Log
    </h2>
    <DataTable
      :data="recentAudit"
      :columns="columns"
      :loading="loading"
      :row-key="(item: AuditEntry) => item.id"
    >
      <template #timestamp-cell="{ row }">
        <span class="text-sm">{{ formatDate(row.original.timestamp) }}</span>
      </template>
    </DataTable>

    <UButton
      v-if="recentAudit.length > 0"
      :to="UiRoutes.Admin.AuditLog"
      label="View Full Audit Log"
      color="neutral"
      variant="ghost"
      class="mt-3"
    />
  </div>
</template>
```

Notes vs. the original draft:
- No "Cards" stat card / `totalCards` field — no card-count admin endpoint exists (still true as of this update; confirmed no `CardCount`/`TotalCards` anywhere in `HydraForge.Server`). Don't fake a `0`. Add the stat card in a follow-up if a counts endpoint ships.
- No real health-check signal exists yet either — the original draft's `healthStatus: 'Unknown'` was themselves an admission of this. Rendered as a static "Online" (reaching this page at all proves the API responded) rather than a fake/hardcoded state pretending to be live data. Wire to a real `/api/admin/health`-style endpoint if one ships later.
- `DataTable` (not raw `UTable`) for the recent-audit list, matching `audit-log.vue` — gets loading/empty state handling for free. No pagination props passed (fixed `take=10`, no `page`/`page-size`/`total-count`) since this is a preview, not the full log.
- `ApiRoutes.Admin.auditLog()` query string is untyped (`?take=10` appended manually) — matches the existing pattern in `audit-log.vue` (`${ApiRoutes.Admin.auditLog()}?${params.toString()}`); `ApiRoutes.Admin.auditLog` takes no params itself.

### Step 3: Verify

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
```

## Verification

- `pnpm typecheck` — no errors
- `pnpm lint` — no errors
- `pnpm build` — no errors
- Manual: login as admin, confirm "Dashboard" appears in the sidebar Admin group above Users, click it, land on `/admin` with summary cards + quick-action buttons + recent audit entries (not redirected to Users)
- Manual: login as non-admin, confirm no Admin group in sidebar and direct nav to `/admin` redirects to `/chats`

## Dependencies

- Task 9 (Admin user management page)
- Task 10 (System settings page)
- Task 12 (Audit log page + `DataTable` component)
- Task 11 (Audit log API for recent entries)
