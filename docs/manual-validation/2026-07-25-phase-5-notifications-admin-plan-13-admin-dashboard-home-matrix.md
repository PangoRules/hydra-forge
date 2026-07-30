## Validate: Admin Dashboard Home Page (Plan 13)

### Setup
- [ ] `docker compose up -d postgres` — DB running
- [ ] `dotnet run --project src/HydraForge.Server` — server up on `:5000`
- [ ] `cd src/web-ui && pnpm dev` — web UI up on `:3000`
- [ ] Admin seed ran — admin user exists (default: admin / Admin123!)
- [ ] At least one non-admin test user exists
- [ ] At least one project with one archived + one active project (so breakdown has both > 0)
- [ ] At least one disabled user (so Users breakdown has disabled > 0)
- [ ] At least 10 audit log entries exist (so "Recent Audit Log" fills the preview)

### Happy Path — Sidebar Nav
1. Log in as admin → sidebar shows "Admin" group with items in order: Dashboard, Users, System Settings, Audit Log
2. Hover "Dashboard" in sidebar → tooltip / hover state renders
3. Click "Dashboard" in sidebar → navigates to `/admin`
4. Click "Users" in sidebar → navigates to `/admin/users`
5. Log out, log in as non-admin → sidebar has NO "Admin" group at all

### Happy Path — Dashboard Page (admin lands here)
1. Navigate to `/admin` as admin → page renders (NOT redirected to `/admin/users`)
2. Page shows heading "Admin Dashboard"
3. Three summary cards visible: Users, Projects, Health
4. Users card shows total count matching `GET /api/admin/users` totalCount, with "X active · Y disabled" subtitle matching server-side breakdown
5. Projects card shows total count matching `GET /api/admin/projects` totalCount, with "X active · Y archived" subtitle matching server-side breakdown
6. Health card shows green dot + "Online" label (static, no fetch)
7. Four quick-action buttons render: Manage Users, All Projects, System Settings, Audit Log
8. Click "Manage Users" → navigates to `/admin/users`
9. Click "All Projects" → navigates to `/projects`
10. Click "System Settings" → navigates to `/admin/settings`
11. Click "Audit Log" → navigates to `/admin/audit-log`
12. "Recent Audit Log" section heading renders
13. DataTable shows up to 10 audit entries with columns: Timestamp, Actor, Entity Type, Action
14. Timestamp column renders formatted local date string (not raw ISO)
15. Footer shows "1-10 of N" where N is the full audit log count from the API
16. "View Full Audit Log" button renders (because recentAudit.length > 0) and navigates to `/admin/audit-log`

### Edge Cases
1. Navigate to `/admin` while logged out → middleware redirects to `/login`
2. Navigate to `/admin` as non-admin → redirects to `/chats` (does NOT render dashboard)
3. Disable API server, then load `/admin` as admin → error toast with correlation ID shown, summary cards stay at `0`
4. Empty install (no audit entries) → "Recent Audit Log" DataTable shows "No results found." empty state, "View Full Audit Log" button is HIDDEN
5. Install with exactly 10 audit entries → footer shows "1-10 of 10", pagination footer renders single page (no nav buttons)
6. Install with 500+ users → Users card totalCount shows real total, but active/disabled breakdown only counts the first 500 fetched (DASHBOARD_BREAKDOWN_PAGE_SIZE constant) — verify this matches plan's "revisit with a dedicated counts endpoint" caveat
7. Install with 500+ projects → same caveat as #6 for the Projects card breakdown

### Regressions
1. Existing admin pages still work: `/admin/users`, `/admin/settings`, `/admin/audit-log` render unchanged
2. Existing non-admin pages still work: `/projects`, `/chats`, login still functional
3. Sidebar "Workspace" group (Chats, Projects) and disabled "AI Tools"/"Creative"/"Personal" groups still render
4. Login / logout / JWT refresh still work
5. SignalR board presence unaffected
6. Admin nav group item order matches plan: Dashboard → Users → System Settings → Audit Log (Reports still disabled placeholder)
7. `/api/admin/audit-log?take=10` query works (server accepts the `take` parameter, returns up to 10 items + totalCount)

### Cleanup
- [ ] No test data needs cleanup (dashboard is read-only)