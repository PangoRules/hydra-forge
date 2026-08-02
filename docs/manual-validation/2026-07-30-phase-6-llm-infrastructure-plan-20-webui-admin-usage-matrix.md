## Validate: Plan 20 — Web UI admin usage + account usage

### Setup
- [ ] API server running (`dotnet run --project src/HydraForge.Server`) with `ASPNETCORE_ENVIRONMENT=Development`
- [ ] Web UI running (`cd src/web-ui && pnpm dev`)
- [ ] At least one admin user and one non-admin user with recorded LLM usage in the database
- [ ] Logged in as admin in one browser session; non-admin in another (or incognito)

### Happy Path — Account Self-Service Usage
1. Log in as non-admin user → open user menu → click "Usage" → navigates to `/account/usage`
2. Page loads → header "My Usage", period dates render, token + image bars visible
3. Recent Calls table populates with up to 20 rows (timestamp, feature, model, tokens, images, cost)
4. Token/Image cells show comma-formatted numbers; cost column shows `$X.XXXX`
5. Token bar fills proportionally when `tokensUsed < tokensBudget`; shows "Unlimited" when budget == 0
6. Image bar same behavior as token bar

### Happy Path — Admin Usage Dashboard
1. Log in as admin → navigate to `/admin/usage` → page loads with "Token Usage" tab active
2. Token table populates: timestamp, user, feature, model, input/output/cached tokens, cost
3. Footer shows aggregated "Total Cost: $X.XXXX"
4. Pagination footer shows row count + page size selector
5. Click "Image Usage" tab → table switches to image columns (timestamp, user, feature, model, image count, resolution, cost); footer total cost refreshes
6. Switch back to "Token Usage" → previous token data reloads (or refreshes)

### Filters — Admin
1. Type partial username into user filter → debounced 300ms → user dropdown shows matches → click one → filter chip appears with username + clear (X) button
2. Click user chip X → filter clears, dropdown reopens
3. Type a valid GUID into user filter → filter applies without dropdown; clear via chip
4. Open feature multi-select → select 2 features → URL params include `feature=PersonalChat&feature=ProjectChat` (check via Network tab) → table refreshes
5. Type in Model filter → table refreshes (debounced)
6. Pick "From" date → table refreshes; pick "To" date → table refreshes
7. Click "Reset filters" → all filters clear, page returns to 1, table reloads

### Pagination — Admin
1. Reduce page size to 25 → table refreshes, page resets to 1
2. Click next page → `skip` increments correctly; table shows next page of records
3. With active filters, pagination preserves filter context

### Edge Cases
1. Non-admin user navigates directly to `/admin/usage` → redirected to `/projects` (or `/chats` depending on convention)
2. Account page with zero usage → bars show 0% or "Unlimited", Recent Calls table renders empty (not crashing)
3. Account page with `tokensBudget == 0` → "Unlimited" shown; bar hidden
4. Admin usage with all filters cleared → full unfiltered query, table shows all records up to page size
5. Backend down → `toast.showApiError` fires; table stays in last known state

### Regressions
1. Top nav links (Projects, Notifications, Admin sections) → still navigate correctly
2. Login flow → still redirects through auth middleware
3. Existing admin pages (users, providers, routing, audit-log) → still load
4. Existing account page (if any other than usage) → unaffected

### Cleanup
- [ ] No persistent test data; usage records accumulate naturally
