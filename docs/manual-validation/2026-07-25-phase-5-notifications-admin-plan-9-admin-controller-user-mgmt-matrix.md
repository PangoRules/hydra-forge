## Validate: Admin Controller + User Management (Plan 9)

### Setup
- [ ] `docker compose up -d postgres` — DB running
- [ ] `dotnet run --project src/HydraForge.Server` — server up on `:5000`
- [ ] Admin seed ran — admin user exists (default: admin / Admin123!)
- [ ] A non-admin test user exists (create via UI or seed)

### Happy Path — API
1. `GET /api/admin/users` with admin bearer token → `200` + paginated `{ items, totalCount }`
2. `POST /api/admin/users` with valid body → `201` + created user DTO
3. `PATCH /api/admin/users/{id}/disable` → `204`, user becomes disabled
4. `PATCH /api/admin/users/{id}/enable` → `204`, user re-enabled
5. `POST /api/admin/users/{id}/reset-password` with `{ "newPassword": "..." }` → `204`
6. `PATCH /api/admin/users/{id}/role` → `204`, admin flag toggles
7. `GET /api/admin/projects` → `200` + paginated projects list (includes projects the admin is NOT a member of)
8. `GET /api/admin/projects/{id}` → `200` + project detail for any project

### Happy Path — Web UI
1. Navigate to `/admin` as admin → redirects to `/admin/users`
2. `/admin/users` shows paginated user table with username, email, status badges
3. Click "Disable" on an active user → badge changes to "Disabled", toast shows
4. Click "Enable" on a disabled user → badge changes to "Active"
5. Click "Make Admin" → admin badge appears
6. Click "Remove Admin" → admin badge disappears
7. Click "Create User" → modal opens with all fields
8. Fill valid data, click "Create" → user appears in table, toast shows
9. Search by username → table filters, total count updates
10. Click reset password on a user → modal appears, enter new password → `204`
11. Navigate to `/admin/projects` → table shows all projects (including ones admin isn't member of)
12. Click "Open Board" → navigates to project board

### Edge Cases
1. `POST /api/admin/users` with duplicate username → `400` + `USERNAME_TAKEN`
2. `POST /api/admin/users` with blank username → `400` + validation error
3. `POST /api/admin/users` with short password (< 6 chars) → `400` + validation error
4. `GET /api/admin/users/{id}` with nonexistent GUID → `404`
5. `PATCH /api/admin/users/{id}/disable` with self-ID → `400` + `SELF_DISABLE`
6. `PATCH /api/admin/users/{id}/disable` on nonexistent user → `404`
7. `PATCH /api/admin/users/{id}/role` with self-ID → `400` + `ADMIN_SELF_DEMOTION`
8. `GET /api/admin/users` without auth token → `401`
9. `GET /api/admin/users` with non-admin token → `403`
10. Navigate to `/admin` as non-admin → redirects to `/projects`
11. Navigate to `/admin/users` as non-admin → redirects to `/projects`
12. Search with no matches → empty table, `0 total users`

### Regressions
1. Login as admin with valid credentials → `200`, JWT returned
2. Login as disabled user → `401` / error
3. Login as non-admin user → `200`, normal user session
4. Non-admin user loads `/projects` → sees own projects (not affected by admin bypass)
5. Project CRUD for non-admin user → works as before (membership enforced)
6. SignalR board presence → unaffected by admin changes

### Cleanup
- [ ] No test data needs cleanup (API endpoints operate on real DB)
