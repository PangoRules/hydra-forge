# Plan 20: Web UI admin usage + account usage

**Branch:** `task/webui-admin-usage`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 20

## Steps

### 1. Create admin usage dashboard
- File: `src/web-ui/app/pages/admin/usage.vue`
- Two tabs: "Token Usage" and "Image Usage".
- Filters: user search (async select), feature (multi-select), model, date range (from/to).
- `DataTable` with `fillHeight` (full-height dashboard layout).
- Token columns: timestamp, user, feature, model, input tokens, output tokens, cached tokens, cost.
- Image columns: timestamp, user, feature, model, image count, resolution, cost.
- Aggregated cost row at bottom.
- Pagination via server-side `skip`/`take`.

### 2. Create account self-service usage page
- File: `src/web-ui/app/pages/account/usage.vue`
- Route: `/account/usage` (new directory `pages/account/`).
- Auth-gated (middleware: `['auth']`).
- Display: current period (start/end dates), token usage bar (used / budget), image usage bar.
- "Unlimited" display when budget is 0.
- Recent calls table (last 20): feature, model, tokens/images, cost, timestamp.
- Simple, no filters needed (single user).

### 3. Add account route
- File: `src/web-ui/app/lib/routes.ts`
- Add `UiRoutes.Account = { Usage: '/account/usage' }`.
- Add `ApiRoutes.Account = { usage: () => '/api/account/usage' }`.

### 4. Wire user menu link
- File: `src/web-ui/app/components/layout/AppTopbar.vue`
- Add "Usage" link in user dropdown → `UiRoutes.Account.Usage`.

## Verification
- `pnpm typecheck`
- `pnpm lint`
- `pnpm build`
- Manual: navigate to `/admin/usage`, apply filters, verify pagination.
- Manual: navigate to `/account/usage`, verify own usage displayed.