# Navigation Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the flat `UHeader`-only global layout with a collapsible, grouped sidebar + slim topbar (Nuxt UI v4 `UDashboard*` components), make Chats the login landing page (stub only), and remove the redundant `/admin/projects` page.

**Architecture:** `app/layouts/default.vue` is rebuilt around `UDashboardGroup` (`AppSidebar` + `UDashboardPanel` with `AppTopbar` as its header). Both new components read from a single `nav-config.ts` data file rather than hand-rolled markup. Sidebar collapse state is a small localStorage-backed composable. All app routing continues through `UiRoutes`/`ApiRoutes` in `app/lib/routes.ts` — no inline path strings.

**Tech Stack:** Nuxt 4, Nuxt UI v4 (`UDashboardGroup`, `UDashboardSidebar`, `UDashboardNavbar`, `UDashboardPanel`, `UNavigationMenu`, `UDropdownMenu`), Vitest + `@nuxt/test-utils/runtime` (`mountSuspended`, `mockNuxtImport`), Vue 3 `<script setup>`.

## Global Constraints

- No `console.log`/`console.error`/`console.warn` in production code.
- No inline API/UI path strings — always `UiRoutes`/`ApiRoutes` from `app/lib/routes.ts`.
- `xUnit`/Vitest — plain assertions, no FluentAssertions-equivalent matcher libraries beyond what's already used (`expect` only).
- No new Pinia store for sidebar-collapse state — it's local UI state, use a composable (matches `useBoardFilters`/`useRovingFocus` precedent).
- Lucide icons only (`i-lucide-*`), matching existing icon usage throughout `src/web-ui`.
- No new files for any disabled/backlog nav item (Deep Research, Compare, Cookbook, Automations, Prompt Library, Gallery, Image Generator, Brain/Memory, Tasks, Calendar, Documents/Library, Theme, Voice Notes, System Settings, Audit Log, Reports) — these are `disabled: true` nav-config entries only, per explicit user instruction to avoid scope creep beyond the nav shell itself.
- Frontend change — per CLAUDE.md, must be verified in a running browser (`pnpm dev`) before considering the work complete, not just unit tests.

---

## Task 1: Routing changes — add `/chats`, remove `/admin/projects`

**Files:**
- Modify: `src/web-ui/app/lib/routes.ts:19-24` (`UiRoutes`), `:148-151` (`ApiRoutes.Admin.projectsList`)
- Modify: `src/web-ui/app/pages/index.vue:5`
- Modify: `src/web-ui/app/pages/login.vue:17`
- Modify: `src/web-ui/app/pages/admin/index.vue:6`
- Modify: `src/web-ui/app/pages/admin/users.vue:2,18`
- Delete: `src/web-ui/app/pages/admin/projects.vue`

**Interfaces:**
- Produces: `UiRoutes.Chats` (`'/chats'`), used by Task 4 (Chats stub page), Task 5 (`AppSidebar`'s nav-config), and the redirects below. `UiRoutes.Admin.Projects` and `ApiRoutes.Admin.projectsList` no longer exist after this task — nothing outside the deleted page referenced them (verified: `grep -rn "projectsList\|UiRoutes.Admin.Projects"` only matched the page itself).

- [ ] **Step 1: Add `Chats` route, remove `Admin.Projects`**

In `src/web-ui/app/lib/routes.ts`, replace the `UiRoutes` block:

```ts
export const UiRoutes = {
  Login: '/login',
  Setup: '/setup',
  Chats: '/chats',
  Projects: {
    List: '/projects',
    Board: (projectId: string) => `/projects/${projectId}/board`
  },
  Admin: {
    Home: '/admin',
    Users: '/admin/users',
    Settings: '/admin/settings',
    AuditLog: '/admin/audit-log'
  }
} as const
```

(Note: `Admin.Projects` is removed — `/admin/projects` is deleted in Step 4 below.)

- [ ] **Step 2: Remove the now-unused `projectsList` API route**

In `src/web-ui/app/lib/routes.ts`, in the `ApiRoutes.Admin` object, delete the `projectsList` entry:

```ts
  Admin: {
    usersList: (skip = 0, take = 20, search?: string) =>
      `/api/admin/users?skip=${skip}&take=${take}${search ? `&search=${encodeURIComponent(search)}` : ''}`,
    userGet: (userId: string) => `/api/admin/users/${userId}`,
    userCreate: () => '/api/admin/users',
    userDisable: (userId: string) => `/api/admin/users/${userId}/disable`,
    userEnable: (userId: string) => `/api/admin/users/${userId}/enable`,
    userResetPassword: (userId: string) => `/api/admin/users/${userId}/reset-password`,
    userRole: (userId: string) => `/api/admin/users/${userId}/role`,
    projectGet: (projectId: string) => `/api/admin/projects/${projectId}`,
    settingsGet: () => '/api/admin/settings',
    settingsUpdate: () => '/api/admin/settings',
    auditLog: () => '/api/admin/audit-log'
  }
```

(`projectGet` stays — unrelated to the deleted list page, may be used by future admin project-detail work.)

- [ ] **Step 3: Point login-default routes at `/chats`**

In `src/web-ui/app/pages/index.vue`, change:
```ts
await navigateTo(UiRoutes.Projects.List, { redirectCode: 302 })
```
to:
```ts
await navigateTo(UiRoutes.Chats, { redirectCode: 302 })
```

In `src/web-ui/app/pages/login.vue`, change:
```ts
await navigateTo(UiRoutes.Projects.List)
```
to:
```ts
await navigateTo(UiRoutes.Chats)
```

- [ ] **Step 4: Delete `/admin/projects` and fix its redirect siblings**

Delete `src/web-ui/app/pages/admin/projects.vue` entirely — it duplicated `/projects`'s existing admin-bypass view (Plan 8) with a strictly worse UI (no pagination, no archive/edit, no role filter).

In `src/web-ui/app/pages/admin/index.vue`, change line 6:
```ts
if (!user?.isAdmin) navigateTo('/projects')
```
to:
```ts
if (!user?.isAdmin) navigateTo(UiRoutes.Chats)
```

In `src/web-ui/app/pages/admin/users.vue`, add the `UiRoutes` import (currently only `ApiRoutes` is imported at line 2):
```ts
import { ApiRoutes, UiRoutes } from '~/lib/routes'
```
and change line 18:
```ts
navigateTo('/projects')
```
to:
```ts
navigateTo(UiRoutes.Chats)
```

- [ ] **Step 5: Run the full web-ui test suite to confirm nothing broke**

Run: `cd src/web-ui && pnpm test`
Expected: PASS (no test referenced `/admin/projects.vue` or `UiRoutes.Admin.Projects` — confirmed via `find`/`grep` during planning).

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/lib/routes.ts src/web-ui/app/pages/index.vue src/web-ui/app/pages/login.vue src/web-ui/app/pages/admin/index.vue src/web-ui/app/pages/admin/users.vue
git rm src/web-ui/app/pages/admin/projects.vue
git commit -m "refactor: add /chats route as login default, remove redundant /admin/projects"
```

---

## Task 2: Nav config data model

**Files:**
- Create: `src/web-ui/app/lib/nav-config.ts`
- Test: `src/web-ui/app/lib/__tests__/nav-config.test.ts`

**Interfaces:**
- Consumes: `UiRoutes.Chats` (`'/chats'`), `UiRoutes.Projects.List` (`'/projects'`), `UiRoutes.Admin.Users` (`'/admin/users'`) from Task 1.
- Produces: `getNavGroups(isAdmin: boolean): NavigationMenuItem[][]` — an array of groups, each group a `NavigationMenuItem[]` whose first element is `{ label: <GroupName>, type: 'label' }` followed by real items. Consumed by Task 5 (`AppSidebar.vue`). `NavigationMenuItem` is Nuxt UI's own exported type (`import type { NavigationMenuItem } from '@nuxt/ui'`).

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/lib/__tests__/nav-config.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { getNavGroups } from '~/lib/nav-config'

describe('nav-config', () => {
  it('includes Workspace, AI Tools, Creative, Personal groups for a non-admin user', () => {
    const groups = getNavGroups(false)
    const groupLabels = groups.map(g => g[0]!.label)
    expect(groupLabels).toEqual(['Workspace', 'AI Tools', 'Creative', 'Personal'])
  })

  it('adds an Admin group only for admin users', () => {
    const nonAdminGroups = getNavGroups(false)
    const adminGroups = getNavGroups(true)
    expect(nonAdminGroups.some(g => g[0]!.label === 'Admin')).toBe(false)
    expect(adminGroups.some(g => g[0]!.label === 'Admin')).toBe(true)
  })

  it('Chats and Projects are enabled with real routes', () => {
    const [workspace] = getNavGroups(false)
    const chats = workspace!.find(i => i.label === 'Chats')!
    const projects = workspace!.find(i => i.label === 'Projects')!
    expect(chats.disabled).toBeUndefined()
    expect(chats.to).toBe('/chats')
    expect(projects.disabled).toBeUndefined()
    expect(projects.to).toBe('/projects')
  })

  it('backlog items are disabled with no route', () => {
    const groups = getNavGroups(false)
    const aiTools = groups.find(g => g[0]!.label === 'AI Tools')!
    const deepResearch = aiTools.find(i => i.label === 'Deep Research')!
    expect(deepResearch.disabled).toBe(true)
    expect(deepResearch.to).toBeUndefined()
  })

  it('Admin group only exposes Users with a real route; the rest are disabled', () => {
    const groups = getNavGroups(true)
    const admin = groups.find(g => g[0]!.label === 'Admin')!
    const users = admin.find(i => i.label === 'Users')!
    const settings = admin.find(i => i.label === 'System Settings')!
    const auditLog = admin.find(i => i.label === 'Audit Log')!
    const reports = admin.find(i => i.label === 'Reports')!
    expect(users.to).toBe('/admin/users')
    expect(users.disabled).toBeUndefined()
    expect(settings.disabled).toBe(true)
    expect(auditLog.disabled).toBe(true)
    expect(reports.disabled).toBe(true)
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test -- nav-config`
Expected: FAIL with "Failed to resolve import ~/lib/nav-config" or "getNavGroups is not a function"

- [ ] **Step 3: Write the implementation**

Create `src/web-ui/app/lib/nav-config.ts`:

```ts
import type { NavigationMenuItem } from '@nuxt/ui'
import { UiRoutes } from '~/lib/routes'

/**
 * Sidebar nav structure. Each group is an array whose first item is a
 * `type: 'label'` header, followed by real/disabled items. Disabled items
 * have no `to` — they render greyed and non-navigable until their feature
 * ships (System Settings/Audit Log: Plans 10-12; everything else: backlog).
 */
export function getNavGroups(isAdmin: boolean): NavigationMenuItem[][] {
  const groups: NavigationMenuItem[][] = [
    [
      { label: 'Workspace', type: 'label' },
      { label: 'Chats', icon: 'i-lucide-message-circle', to: UiRoutes.Chats },
      { label: 'Projects', icon: 'i-lucide-layout-dashboard', to: UiRoutes.Projects.List }
    ],
    [
      { label: 'AI Tools', type: 'label' },
      { label: 'Deep Research', icon: 'i-lucide-search', disabled: true },
      { label: 'Compare', icon: 'i-lucide-columns-2', disabled: true },
      { label: 'Cookbook', icon: 'i-lucide-book-open', disabled: true },
      { label: 'Automations', icon: 'i-lucide-workflow', disabled: true },
      { label: 'Prompt Library', icon: 'i-lucide-library', disabled: true }
    ],
    [
      { label: 'Creative', type: 'label' },
      { label: 'Gallery', icon: 'i-lucide-image', disabled: true },
      { label: 'Image Generator', icon: 'i-lucide-wand-2', disabled: true }
    ],
    [
      { label: 'Personal', type: 'label' },
      { label: 'Brain / Memory', icon: 'i-lucide-brain', disabled: true },
      { label: 'Tasks', icon: 'i-lucide-check-square', disabled: true },
      { label: 'Calendar', icon: 'i-lucide-calendar', disabled: true },
      { label: 'Documents / Library', icon: 'i-lucide-folder', disabled: true },
      { label: 'Theme', icon: 'i-lucide-palette', disabled: true },
      { label: 'Voice Notes', icon: 'i-lucide-mic', disabled: true }
    ]
  ]

  if (isAdmin) {
    groups.push([
      { label: 'Admin', type: 'label' },
      { label: 'Users', icon: 'i-lucide-users', to: UiRoutes.Admin.Users },
      { label: 'System Settings', icon: 'i-lucide-settings', disabled: true },
      { label: 'Audit Log', icon: 'i-lucide-scroll-text', disabled: true },
      { label: 'Reports', icon: 'i-lucide-bar-chart-3', disabled: true }
    ])
  }

  return groups
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm test -- nav-config`
Expected: PASS (5 tests)

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/lib/nav-config.ts src/web-ui/app/lib/__tests__/nav-config.test.ts
git commit -m "feat: add sidebar nav-config data model"
```

---

## Task 3: Sidebar collapse persistence composable

**Files:**
- Create: `src/web-ui/app/composables/useSidebarCollapse.ts`
- Test: `src/web-ui/app/composables/__tests__/useSidebarCollapse.test.ts`

**Interfaces:**
- Produces: `useSidebarCollapse(): { collapsed: Ref<boolean> }`, consumed by Task 5 (`AppSidebar.vue`) via `v-model:collapsed`.

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/composables/__tests__/useSidebarCollapse.test.ts`:

```ts
import { describe, it, expect, beforeEach } from 'vitest'
import { nextTick } from 'vue'
import { useSidebarCollapse } from '~/composables/useSidebarCollapse'

describe('useSidebarCollapse', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('defaults to expanded (not collapsed) with no stored value', () => {
    const { collapsed } = useSidebarCollapse()
    expect(collapsed.value).toBe(false)
  })

  it('restores collapsed=true from localStorage', () => {
    localStorage.setItem('hydraforge-sidebar-collapsed', 'true')
    const { collapsed } = useSidebarCollapse()
    expect(collapsed.value).toBe(true)
  })

  it('persists changes to localStorage', async () => {
    const { collapsed } = useSidebarCollapse()
    collapsed.value = true
    await nextTick()
    expect(localStorage.getItem('hydraforge-sidebar-collapsed')).toBe('true')
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test -- useSidebarCollapse`
Expected: FAIL with "Failed to resolve import ~/composables/useSidebarCollapse"

- [ ] **Step 3: Write the implementation**

Create `src/web-ui/app/composables/useSidebarCollapse.ts`:

```ts
const STORAGE_KEY = 'hydraforge-sidebar-collapsed'

/**
 * Persists the sidebar's expanded/collapsed state across sessions.
 * Client-only read/write — SSR has no localStorage, so the server always
 * renders expanded and the client corrects itself on hydration.
 */
export function useSidebarCollapse() {
  const collapsed = ref(false)

  if (import.meta.client) {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored !== null) collapsed.value = stored === 'true'
  }

  watch(collapsed, (value) => {
    if (import.meta.client) localStorage.setItem(STORAGE_KEY, String(value))
  })

  return { collapsed }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm test -- useSidebarCollapse`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/composables/useSidebarCollapse.ts src/web-ui/app/composables/__tests__/useSidebarCollapse.test.ts
git commit -m "feat: add useSidebarCollapse composable for persisted sidebar state"
```

---

## Task 4: Chats stub page

**Files:**
- Create: `src/web-ui/app/pages/chats/index.vue`
- Test: `src/web-ui/app/pages/chats/__tests__/index.test.ts`

**Interfaces:**
- Consumes: nothing new (no API calls — pure static shell).
- Produces: the `/chats` route content, already wired as the login/root default in Task 1.

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/pages/chats/__tests__/index.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ChatsPage from '~/pages/chats/index.vue'

describe('chats/index.vue', () => {
  it('renders a Chats heading and a disabled New Chat button', async () => {
    const wrapper = await mountSuspended(ChatsPage)
    expect(wrapper.find('h1').text()).toBe('Chats')
    const button = wrapper.find('button')
    expect(button.attributes('disabled')).toBeDefined()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test -- chats/index`
Expected: FAIL with "Failed to resolve import ~/pages/chats/index.vue"

- [ ] **Step 3: Write the implementation**

Create `src/web-ui/app/pages/chats/index.vue`:

```vue
<script setup lang="ts">
definePageMeta({ middleware: ['auth'] })
</script>

<template>
  <div class="flex flex-col items-center justify-center h-full text-center p-8">
    <UIcon
      name="i-lucide-message-circle"
      class="w-12 h-12 text-muted mb-4"
    />
    <h1 class="text-2xl font-bold mb-2">
      Chats
    </h1>
    <p class="text-muted mb-6">
      Your AI conversations will live here. This feature is coming soon.
    </p>
    <UButton
      label="New Chat"
      icon="i-lucide-plus"
      disabled
    />
  </div>
</template>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm test -- chats/index`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/pages/chats/index.vue src/web-ui/app/pages/chats/__tests__/index.test.ts
git commit -m "feat: add Chats stub landing page"
```

---

## Task 5: AppSidebar component

**Files:**
- Create: `src/web-ui/app/components/layout/AppSidebar.vue`
- Test: `src/web-ui/app/components/layout/__tests__/AppSidebar.test.ts`

**Interfaces:**
- Consumes: `getNavGroups(isAdmin: boolean): NavigationMenuItem[][]` (Task 2), `useSidebarCollapse(): { collapsed: Ref<boolean> }` (Task 3), `useAuthStore().user?.isAdmin` (existing store), `UiRoutes.Chats` (Task 1).
- Produces: `AppSidebar.vue` component, consumed by Task 7 (`default.vue`).

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/components/layout/__tests__/AppSidebar.test.ts`:

```ts
import { describe, it, expect, beforeEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import AppSidebar from '~/components/layout/AppSidebar.vue'

const mockUser = { userId: 'u1', username: 'admin', isAdmin: true }

mockNuxtImport('useAuthStore', () => () => ({ user: mockUser }))

describe('AppSidebar', () => {
  beforeEach(() => {
    mockUser.isAdmin = true
  })

  it('renders the pinned New Chat action', async () => {
    const wrapper = await mountSuspended(AppSidebar)
    expect(wrapper.text()).toContain('New Chat')
  })

  it('shows the Admin group for an admin user', async () => {
    const wrapper = await mountSuspended(AppSidebar)
    expect(wrapper.text()).toContain('System Settings')
  })

  it('hides the Admin group for a non-admin user', async () => {
    mockUser.isAdmin = false
    const wrapper = await mountSuspended(AppSidebar)
    expect(wrapper.text()).not.toContain('System Settings')
  })

  it('renders backlog items as non-navigable (no anchor tag)', async () => {
    const wrapper = await mountSuspended(AppSidebar)
    expect(wrapper.text()).toContain('Deep Research')
    const links = wrapper.findAll('a')
    expect(links.some(a => a.text().includes('Deep Research'))).toBe(false)
  })

  it('renders Projects as a real navigable link', async () => {
    const wrapper = await mountSuspended(AppSidebar)
    const links = wrapper.findAll('a')
    expect(links.some(a => a.attributes('href') === '/projects')).toBe(true)
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test -- AppSidebar`
Expected: FAIL with "Failed to resolve import ~/components/layout/AppSidebar.vue"

- [ ] **Step 3: Write the implementation**

Create `src/web-ui/app/components/layout/AppSidebar.vue`:

```vue
<script setup lang="ts">
import { getNavGroups } from '~/lib/nav-config'
import { useSidebarCollapse } from '~/composables/useSidebarCollapse'
import { UiRoutes } from '~/lib/routes'

const authStore = useAuthStore()
const { collapsed } = useSidebarCollapse()

const navGroups = computed(() => getNavGroups(authStore.user?.isAdmin ?? false))
</script>

<template>
  <UDashboardSidebar
    v-model:collapsed="collapsed"
    :collapsible="true"
    :resizable="false"
    mode="slideover"
  >
    <template #header>
      <UButton
        :label="collapsed ? undefined : 'New Chat'"
        icon="i-lucide-plus"
        block
        :to="UiRoutes.Chats"
      />
    </template>

    <UNavigationMenu
      :items="navGroups"
      orientation="vertical"
      :collapsed="collapsed"
    />

    <template #footer>
      <UDashboardSidebarCollapse />
    </template>
  </UDashboardSidebar>
</template>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm test -- AppSidebar`
Expected: PASS (5 tests). If the "non-navigable" or "real navigable link" assertions fail because `UNavigationMenu` renders disabled/enabled items differently than expected (e.g. as `<button>` vs `<a>` in this Nuxt UI version), inspect `wrapper.html()` in the failing test to see the actual output and adjust the assertion (e.g. check `href` attribute presence/absence instead of tag name) — the underlying behavior (disabled items don't navigate) is what matters, not the exact selector.

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/components/layout/AppSidebar.vue src/web-ui/app/components/layout/__tests__/AppSidebar.test.ts
git commit -m "feat: add AppSidebar component with grouped nav-config-driven items"
```

---

## Task 6: AppTopbar component

**Files:**
- Create: `src/web-ui/app/components/layout/AppTopbar.vue`
- Test: `src/web-ui/app/components/layout/__tests__/AppTopbar.test.ts`

**Interfaces:**
- Consumes: `useAuth(): { logout, isAuthenticated }` (existing composable), `useAuthStore().user` (existing store), `NotificationPanel` (existing component at `~/components/notifications/NotificationPanel.vue`), `UiRoutes.Chats` (Task 1).
- Produces: `AppTopbar.vue` component, consumed by Task 7 (`default.vue`).

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/components/layout/__tests__/AppTopbar.test.ts`:

```ts
import { describe, it, expect, vi } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import AppTopbar from '~/components/layout/AppTopbar.vue'

const mockLogout = vi.fn()

mockNuxtImport('useAuth', () => () => ({
  logout: mockLogout,
  isAuthenticated: true
}))

mockNuxtImport('useAuthStore', () => () => ({
  user: { userId: 'u1', username: 'testadmin', isAdmin: true }
}))

describe('AppTopbar', () => {
  it('shows the username as the user-menu trigger label', async () => {
    const wrapper = await mountSuspended(AppTopbar, {
      global: { stubs: { NotificationPanel: true } }
    })
    expect(wrapper.text()).toContain('testadmin')
  })

  it('renders a brand link to the Chats home page', async () => {
    const wrapper = await mountSuspended(AppTopbar, {
      global: { stubs: { NotificationPanel: true } }
    })
    const brandLink = wrapper.find('a[href="/chats"]')
    expect(brandLink.exists()).toBe(true)
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test -- AppTopbar`
Expected: FAIL with "Failed to resolve import ~/components/layout/AppTopbar.vue"

- [ ] **Step 3: Write the implementation**

Create `src/web-ui/app/components/layout/AppTopbar.vue`:

```vue
<script setup lang="ts">
import NotificationPanel from '~/components/notifications/NotificationPanel.vue'
import { UiRoutes } from '~/lib/routes'

const { logout, isAuthenticated } = useAuth()
const authStore = useAuthStore()

const userMenuItems = computed(() => [
  [{ label: authStore.user?.username ?? '', type: 'label' as const }],
  [{ label: 'Logout', icon: 'i-lucide-log-out', onSelect: () => logout() }]
])
</script>

<template>
  <UDashboardNavbar>
    <template #leading>
      <NuxtLink
        :to="UiRoutes.Chats"
        class="flex items-center gap-2"
      >
        <span class="text-lg font-bold">HydraForge</span>
      </NuxtLink>
    </template>

    <template #right>
      <ClientOnly>
        <NotificationPanel v-if="isAuthenticated" />
      </ClientOnly>
      <UColorModeButton />
      <ClientOnly>
        <UDropdownMenu
          v-if="isAuthenticated"
          :items="userMenuItems"
        >
          <UButton
            :label="authStore.user?.username"
            trailing-icon="i-lucide-chevron-down"
            color="neutral"
            variant="ghost"
          />
        </UDropdownMenu>
      </ClientOnly>
    </template>
  </UDashboardNavbar>
</template>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm test -- AppTopbar`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/components/layout/AppTopbar.vue src/web-ui/app/components/layout/__tests__/AppTopbar.test.ts
git commit -m "feat: add AppTopbar component with brand link and user dropdown"
```

---

## Task 7: Rewrite default.vue layout + browser verification

**Files:**
- Modify: `src/web-ui/app/layouts/default.vue` (full rewrite)
- Test: `src/web-ui/app/layouts/__tests__/default.test.ts`

**Interfaces:**
- Consumes: `AppSidebar.vue` (Task 5), `AppTopbar.vue` (Task 6), plus existing `useAuth`, `useNotifications`, `useNotificationHub`, `useSessionManager`, `SessionExpiryModal` (all unchanged from the current file).

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/layouts/__tests__/default.test.ts`:

```ts
import { describe, it, expect, vi } from 'vitest'
import { ref } from 'vue'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import DefaultLayout from '~/layouts/default.vue'

mockNuxtImport('useAuth', () => () => ({
  logout: vi.fn(),
  isAuthenticated: true,
  checkAuth: vi.fn(),
  listenForAuthChanges: vi.fn()
}))
mockNuxtImport('useNotifications', () => () => ({
  fetchUnreadCount: vi.fn()
}))
mockNuxtImport('useNotificationHub', () => () => ({
  connect: vi.fn(),
  disconnect: vi.fn()
}))
mockNuxtImport('useSessionManager', () => () => ({
  isExpired: ref(false),
  isExpiringSoon: ref(false),
  isExtending: ref(false),
  timeRemaining: ref(0),
  remainingFormatted: ref('0:00'),
  extendSession: vi.fn(),
  start: vi.fn(),
  stop: vi.fn()
}))

describe('layouts/default.vue', () => {
  it('renders AppSidebar and AppTopbar around the page slot', async () => {
    const wrapper = await mountSuspended(DefaultLayout, {
      slots: { default: () => 'Page Content' },
      global: {
        stubs: {
          AppSidebar: true,
          AppTopbar: true,
          SessionExpiryModal: true
        }
      }
    })
    expect(wrapper.find('app-sidebar-stub').exists()).toBe(true)
    expect(wrapper.find('app-topbar-stub').exists()).toBe(true)
    expect(wrapper.text()).toContain('Page Content')
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/web-ui && pnpm test -- layouts/default`
Expected: FAIL — old `default.vue` doesn't render `AppSidebar`/`AppTopbar` yet.

- [ ] **Step 3: Rewrite the implementation**

Replace the full contents of `src/web-ui/app/layouts/default.vue`:

```vue
<script setup lang="ts">
import SessionExpiryModal from '~/components/shared/SessionExpiryModal.vue'
import AppSidebar from '~/components/layout/AppSidebar.vue'
import AppTopbar from '~/components/layout/AppTopbar.vue'

const { logout, isAuthenticated, checkAuth, listenForAuthChanges } = useAuth()
const { fetchUnreadCount } = useNotifications()
const notificationHub = useNotificationHub()
const {
  isExpired,
  isExpiringSoon,
  isExtending,
  timeRemaining,
  remainingFormatted,
  extendSession,
  start: startSessionManager,
  stop: stopSessionManager
} = useSessionManager()

// Restore session from cookie immediately during setup — before any page
// mounts or API calls fire. onMounted is too late: the page's onMounted
// (which calls fetchBoard) fires right after the layout's onMounted.
checkAuth()
listenForAuthChanges()

onMounted(() => {
  startSessionManager()
  if (isAuthenticated) {
    fetchUnreadCount()
    notificationHub.connect()
  }
})
onUnmounted(() => {
  stopSessionManager()
  notificationHub.disconnect()
})

const showSessionModal = computed(() => isExpiringSoon.value || isExpired.value)

function handleExtend() {
  extendSession()
}

function handleSessionLogout() {
  logout()
}
</script>

<template>
  <UApp
    :toaster="{ position: 'bottom-right', duration: 5000 }"
    class="h-full flex flex-col overflow-hidden"
  >
    <UDashboardGroup class="flex-1 overflow-hidden">
      <AppSidebar />

      <UDashboardPanel class="flex-1 flex flex-col overflow-hidden">
        <template #header>
          <AppTopbar />
        </template>

        <template #body>
          <slot />
        </template>
      </UDashboardPanel>
    </UDashboardGroup>

    <ClientOnly>
      <SessionExpiryModal
        :open="showSessionModal"
        :expired="isExpired"
        :time-remaining="timeRemaining"
        :remaining-formatted="remainingFormatted"
        :extending="isExtending"
        @extend="handleExtend"
        @logout="handleSessionLogout"
      />
    </ClientOnly>
  </UApp>
</template>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/web-ui && pnpm test -- layouts/default`
Expected: PASS

- [ ] **Step 5: Run the full test suite**

Run: `cd src/web-ui && pnpm test`
Expected: PASS (all suites, including Tasks 1-6 and every pre-existing test)

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/layouts/default.vue src/web-ui/app/layouts/__tests__/default.test.ts
git commit -m "feat: wire AppSidebar/AppTopbar into default layout via UDashboardGroup"
```

- [ ] **Step 7: Manual browser verification (required — do not skip)**

Run: `cd src/web-ui && pnpm dev` (with the .NET API server also running in `Development`, per CLAUDE.md)

Check, on a desktop-width window, logged in as `testadmin`/`TestAdmin123!`:
- Login lands on `/chats`, showing the stub heading and disabled "New Chat" button.
- Sidebar shows all 5 groups (Workspace, AI Tools, Creative, Personal, Admin) with correct labels; Admin group is visible.
- Clicking "Projects" navigates to `/projects`; clicking "Users" under Admin navigates to `/admin/users`.
- Disabled items (Deep Research, System Settings, etc.) render greyed and do nothing on click.
- The pinned "New Chat" button at the sidebar top navigates to `/chats`.
- Clicking the `UDashboardSidebarCollapse` footer button collapses the sidebar to an icon rail; reloading the page keeps it collapsed (localStorage persistence).
- Topbar: brand text links to `/chats`; notification bell and color-mode toggle still work as before; the user-menu dropdown shows the username, and "Logout" logs out correctly.

Check, on a mobile-width window (or browser dev tools mobile emulation) logged in as `testuser1`/`TestUser123!`:
- Sidebar is hidden by default; a hamburger/menu toggle in the topbar opens it as a slide-over drawer.
- Admin group is absent (non-admin user).
- Opening a nav item closes the drawer (`autoClose` default behavior).

If any of the above doesn't match — most likely candidates are `UDashboardSidebarCollapse`'s exact placement/behavior, or the `#leading` slot ordering relative to the built-in mobile toggle button — adjust the slot/prop usage in `AppSidebar.vue`/`AppTopbar.vue` accordingly and re-run this checklist. Do not report this task complete until every item above is confirmed working in the browser.

---

## Self-Review Notes

**Spec coverage:** Architecture (Task 7), nav-config data model (Task 2), collapse persistence (Task 3), Chats stub + login redirect (Tasks 1 + 4), AppSidebar/AppTopbar components (Tasks 5-6), `/admin/projects` deletion (Task 1), disabled backlog items (Task 2), testing (every task has a test). All spec sections have a corresponding task.

**Placeholder scan:** No TBD/TODO; every step has concrete code. Task 5/Task 7's final steps include an explicit fallback instruction for Nuxt UI internals that can't be verified without running the actual component tree (documented as a known risk, with concrete guidance on what to check and adjust — not a vague "handle edge cases").

**Type consistency:** `getNavGroups(isAdmin: boolean): NavigationMenuItem[][]` (Task 2) is the exact signature used in Task 5. `useSidebarCollapse(): { collapsed: Ref<boolean> }` (Task 3) matches its usage in Task 5. `UiRoutes.Chats` (Task 1) is used identically in Tasks 4, 5, 6.

**Scope check:** Single cohesive plan — nav shell only. System Settings/Audit Log/Admin Dashboard content explicitly deferred to Plans 10-13, not duplicated here.
