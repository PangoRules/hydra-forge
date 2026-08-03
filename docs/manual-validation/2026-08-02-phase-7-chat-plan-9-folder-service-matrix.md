## Validate: ChatFolderService (Plan 9)

### Setup
- [ ] Postgres + MinIO up (`docker compose up -d postgres minio`)
- [ ] API server running (`dotnet run --project src/HydraForge.Server`) with `ASPNETCORE_ENVIRONMENT=Development`
- [ ] Authenticate as a user; capture JWT (`/api/auth/login` or `.http` test file)
- [ ] **No controller wired yet for this plan** — endpoint-level matrix is the controller plan's job. This matrix covers service-level behavior via the test harness if no controller is available; otherwise via the controller plan's endpoints.

### Happy Path
1. **Create root folder (no parent, no project)** → folder persists with `OwnerId = actor`, `ParentFolderId = null`, `ArchivedAt = null`
2. **Create child under root (depth=1)** → success; `ParentFolderId = root.Id`
3. **Create grandchild under child (depth=2)** → success; `ParentFolderId = child.Id`
4. **List folders** → returns 3 folders, no `ArchivedAt`
5. **Update folder name** → `Name` changes
6. **Archive empty folder** → returns DTO with `ArchivedAt != null`; folder no longer in subsequent `ListAsync`
7. **Archive folder with 2 child sessions** → folder `ArchivedAt` set; both sessions `ArchivedAt` set

### Edge Cases
1. **Create with empty name `""`** → `Validation.Required` error
2. **Create with whitespace name `"   "`** → `Validation.Required` error
3. **Create with non-existent `ParentFolderId`** → `Chat.FolderNotFound` (NOT `FolderMaxDepth`)
4. **Create with projectId, no membership** → `Projects.MembershipDenied`
5. **Create grandchild under grandchild (depth=3)** → `Chat.FolderMaxDepth`
6. **Update to set `ParentFolderId = folder.Id` (self)** → `Chat.FolderSelfParent`
7. **Update with non-existent `ParentFolderId`** → `Chat.FolderNotFound`
8. **Update with empty name `""`** → currently NO error returned; folder stored with empty name. **Expected**: `Validation.Required`. Document gap if not fixed in this plan.
9. **Archive non-existent folder** → `Chat.FolderNotFound`
10. **Archive folder owned by different user** → `Chat.SessionNotOwner`
11. **List folders after archiving one** → archived folder excluded; only non-archived returned

### Regressions
1. **Existing project archive cascade** (`IChatArchiveService` in `Application.Projects`) still cascades to `ChatFolders` + `ChatSessions` for project-scoped archives — verify `ArchiveProjectAsync` still wipes the `ArchivedAt` on the project's folders (now including user-created personal folders if any share the project)
2. **ChatSession CRUD** still works (CreateSession, UpdateSession, ArchiveSession via separate flow)
3. **13 ChatFolder unit tests pass** — confirmed pre-merge

### Cleanup
- [ ] Delete test folders/sessions via DB or test data teardown
