# Navigation Redesign — Sidebar + Topbar

**Date:** 2026-07-29
**Status:** Approved, ready for implementation plan
**Discovered during:** manual e2e validation of Plan 9 (admin controller + user management)

## Problem

The app currently has one global layout (`app/layouts/default.vue`) built from `UHeader` + `UMain` only. There is no sidebar, no dedicated Navbar/Header/Admin component — the top bar (logo, "Admin" button, notification bell, color-mode toggle, username, Logout) is inlined directly in the layout file. The admin section (`/admin/users`, `/admin/projects`) has no shared chrome or sub-navigation between its pages beyond that single header. As the app grows into a full AI workspace (chat, personal tools, creative tools, admin), a flat header can't hold the number of sections planned — it needs a structured, collapsible, grouped side navigation, in the pattern of ChatGPT/Claude/similar AI-workspace products.

`@nuxt/ui` v4.8.1 is already installed and provides a `UDashboard*` component family (`UDashboardGroup`, `UDashboardSidebar`, `UDashboardNavbar`, `UDashboardPanel`) suited exactly for this, but it is unused anywhere in the codebase today.

## Goals

- Replace the flat header-only layout with a persistent, collapsible sidebar + slim topbar, applied globally (every page, not just admin).
- Structure all current and near-future feature areas into a small number of logical nav groups.
- Make Chats the primary landing page/entry point (matching ChatGPT/Claude conventions), with a pinned "New Chat" action at the top of the sidebar.
- Show not-yet-built feature areas as visible-but-disabled nav entries, so the full intended structure is legible from day one, without requiring pages that don't exist yet.
- Keep this piece of work scoped to the nav shell itself — do not build out System Settings, Audit Log, or Admin Dashboard page content; those are already fully specified in Plans 10–13 and will be executed separately.
- Fix a redundancy surfaced during this design: `/admin/projects` duplicates (with a strictly worse UI — no pagination, no archive/edit, no role filter) what `/projects` already shows an admin via the existing admin-bypass (Plan 8). Delete `/admin/projects`.

## Non-goals

- No real Chat/LLM feature (only a minimal stub landing page).
- No System Settings, Audit Log, or Admin Dashboard page implementation — covered by Plans 10, 11, 12, 13 respectively, executed independently of this work.
- No global search / command palette (raised as a possible future addition, not in scope now).
- No new pages for any disabled/backlog nav item (Automations, Prompt Library, Integrations, Voice Notes, Reports, Deep Research, Compare, Cookbook, Gallery, Image Generator, Brain/Memory, Tasks, Calendar, Documents/Library, Theme). All of these render as either disabled placeholders or, where a page already exists, keep their current implementation untouched.

## Architecture

`app/layouts/default.vue` is rebuilt around Nuxt UI's `UDashboardGroup` (containing `UDashboardSidebar` + a main panel with `UDashboardNavbar` at its top). This replaces the current `UApp > UHeader + UMain` structure. `layouts/auth.vue` (login/setup) is untouched — it has no sidebar/topbar today and doesn't need one.

Two new components own the chrome:

- **`app/components/layout/AppSidebar.vue`** — renders the pinned "New Chat" action, then the grouped nav tree, filtering admin-only groups by `authStore.user?.isAdmin`. Wraps Nuxt UI's `UDashboardSidebar`, using its built-in collapsible/slideover behavior (desktop: collapses to an icon-only rail; mobile: becomes a slideover drawer triggered by a hamburger in the topbar) rather than any custom breakpoint logic.
- **`app/components/layout/AppTopbar.vue`** — brand/logo (links to `/chats`), existing `NotificationPanel` and `UColorModeButton` (unchanged), and a `UDropdownMenu` replacing the old inline `username + Logout button` (dropdown trigger = avatar/initials; menu shows username, admin badge if applicable, Logout item). The old standalone "Admin" button is removed — Admin access now lives only in the sidebar.

A single config file, **`app/lib/nav-config.ts`**, declares the nav data. Both components read from it; no group/item markup is hand-written per-component:

```ts
export type NavItem = {
  label: string
  icon: string       // lucide icon name, matches existing Nuxt UI icon usage
  to?: string         // omitted for disabled items
  disabled?: boolean
}
export type NavGroup = {
  label: string
  items: NavItem[]
  adminOnly?: boolean
}
```

Sidebar collapse state (expanded vs icon-rail) is UI-only and persisted via a small new composable, **`app/composables/useSidebarCollapse.ts`** — a `localStorage`-backed boolean ref, read once on mount. This mirrors the existing pattern used by `useBoardFilters`/`useRovingFocus` (a plain composable, not a new Pinia store), since it's local UI state with no cross-component sharing requirement.

## Final nav structure

```
[+ New Chat]  (pinned action, above all groups)

WORKSPACE
  Chats                 → /chats (new stub page, see below)
  Projects               → /projects (existing, unchanged)

AI TOOLS
  Deep Research          (disabled)
  Compare                (disabled)
  Cookbook               (disabled)
  Automations            (disabled)
  Prompt Library         (disabled)

CREATIVE
  Gallery                (disabled — will house an in-context editor, no separate "Gallery Editor" entry)
  Image Generator        (disabled — output will persist to Gallery once built)

PERSONAL
  Brain / Memory         (disabled)
  Tasks                  (disabled)
  Calendar               (disabled)
  Documents / Library    (disabled)
  Theme                  (disabled — folded in here rather than its own "Appearance" group; revisit grouping once more appearance-type settings exist, e.g. accent color, density, default sidebar state)
  Voice Notes            (disabled)

ADMIN  (adminOnly — group hidden entirely for non-admin users)
  Users                  → /admin/users (existing, unchanged)
  System Settings        (disabled — real page comes from Plan 10)
  Audit Log              (disabled — real page comes from Plans 11/12)
  Reports                (disabled — no backend yet)
```

Naming note: "Projects" appears only once in this structure now (`/admin/projects` is deleted — see below), so there's no collision with Workspace > Projects. "System Settings" (admin-only, org-wide config) and the Theme entry under Personal are conceptually distinct — no other "Settings" label exists to collide with it.

## Routing changes

| Route | Change |
|---|---|
| `/chats` | **New.** Minimal stub page: empty state, disabled/mock message input, "New Chat" pinned button routes here. No LLM wiring, no persistence. Becomes the login/root landing page (replaces the current default of `/projects`). |
| `/admin/projects` | **Deleted.** Duplicated `/projects`'s admin-bypass view with a strictly worse UI (no pagination, no archive/edit, no role filter). Admin's project oversight need is already met by `/projects` for admin users. |
| `/admin/settings`, `/admin/audit-log` | Already declared in `UiRoutes`/`ApiRoutes` from earlier work; still no page — stays that way until Plans 10–12 execute. Nav entries are disabled placeholders pointing nowhere. |
| `/admin` (index redirect) | Unchanged — still redirects to `/admin/users`. Plan 13 will turn this into a real dashboard later; no Admin > Dashboard nav entry is added until that lands. |
| Login default landing | Changes from `/projects` to `/chats`. |

## Data flow / auth gating

- Admin group visibility: `authStore.user?.isAdmin`, same source used today by the old inline "Admin" button and by each admin page's own guard.
- Disabled items: rendered greyed, `cursor-not-allowed`, no `to` prop, no click-through — not merely a dead link.
- Sidebar collapse state: `useSidebarCollapse()`, `localStorage`-backed, no server round-trip.

## Testing

- `AppSidebar.spec.ts` (Vitest + Vue Test Utils): correct groups/items render for an admin user vs a non-admin user; disabled items are non-navigable; New Chat pinned action always renders.
- `AppTopbar.spec.ts`: dropdown menu renders username/admin badge/Logout; notification bell and color toggle unaffected.
- Extend existing Playwright E2E suite: login lands on `/chats`; sidebar navigation to `/projects` and `/admin/users` works; Admin group is absent for a non-admin user session.

## Out of scope / explicitly deferred

- Building `/admin/settings.vue`, `/admin/audit-log.vue`, or reworking `/admin/index.vue` into a dashboard — Plans 10, 11, 12, 13 respectively, already written, executed separately.
- Any page for Deep Research, Compare, Cookbook, Automations, Prompt Library, Gallery, Image Generator, Brain/Memory, Tasks, Calendar, Documents/Library, Voice Notes, Reports — all remain disabled nav entries with no backing route.
- Global search / command palette.
