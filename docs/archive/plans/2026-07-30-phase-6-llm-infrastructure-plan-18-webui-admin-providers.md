# Plan 18: Web UI admin providers + models + nav

**Branch:** `task/webui-admin-providers`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 18

## Steps

### 1. Add API route constants
- File: `src/web-ui/app/lib/routes.ts`
- Extend `ApiRoutes.Admin`:
  ```ts
  providers: {
    list: () => '/api/admin/providers',
    create: () => '/api/admin/providers',
    detail: (id: string) => `/api/admin/providers/${id}`,
    update: (id: string) => `/api/admin/providers/${id}`,
    disable: (id: string) => `/api/admin/providers/${id}`,
    probeModels: (id: string) => `/api/admin/providers/${id}/models`,
    createModel: (id: string) => `/api/admin/providers/${id}/models`,
    updateModel: (id: string, modelId: string) => `/api/admin/providers/${id}/models/${modelId}`,
    deleteModel: (id: string, modelId: string) => `/api/admin/providers/${id}/models/${modelId}`,
  },
  routing: {
    list: () => '/api/admin/routing',
    update: (feature: string) => `/api/admin/routing/${feature}`,
  },
  usage: {
    tokens: (params: string) => `/api/admin/usage/tokens?${params}`,
    images: (params: string) => `/api/admin/usage/images?${params}`,
  },
  userBudget: {
    get: (userId: string) => `/api/admin/users/${userId}/budget`,
    update: (userId: string) => `/api/admin/users/${userId}/budget`,
  }
  ```
- Extend `UiRoutes.Admin`:
  ```ts
  Providers: '/admin/providers',
  ProviderModels: '/admin/providers/models',
  Routing: '/admin/routing',
  Usage: '/admin/usage',
  ```

### 2. Create providers page
- File: `src/web-ui/app/pages/admin/providers.vue`
- `DataTable` listing all providers: name, adapter type, provider type, tier, enabled status.
- "Add Provider" button → `AppModal` with form: name, base URL, adapter type (select), provider type (select), tier (select), fallback provider (select), API key (password field, write-only).
- Edit: same modal pre-filled (API key field empty — leave blank to keep existing).
- Disable: confirm dialog, PATCH to disable endpoint.
- "Probe Models" button per row → calls probe endpoint, shows results in modal.

### 3. Create provider-models page
- File: `src/web-ui/app/pages/admin/provider-models.vue`
- Select provider from dropdown, then `DataTable` of `ProviderModelConfig` rows.
- Add/edit modal: model ID, display name, tier, price per token, max tokens, enabled.
- Delete with confirmation.

### 4. Update admin nav
- File: `src/web-ui/app/lib/nav-config.ts`
- Add to admin group:
  ```ts
  { label: 'Providers', icon: 'i-lucide-server', to: UiRoutes.Admin.Providers },
  { label: 'Routing', icon: 'i-lucide-route', to: UiRoutes.Admin.Routing },
  { label: 'Usage', icon: 'i-lucide-bar-chart-3', to: UiRoutes.Admin.Usage },
  ```
- Remove disabled "Reports" entry (replaced by Usage).

### 5. Add account usage link to user menu
- File: `src/web-ui/app/components/layout/AppTopbar.vue` (or user menu component)
- Add "Usage" link → `/account/usage`.

## Verification
- `pnpm typecheck` — no TS errors.
- `pnpm lint` — clean.
- `pnpm build` — builds successfully.
- Manual: navigate to `/admin/providers`, create/edit/disable provider, probe models.