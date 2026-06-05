# Task 1: Project CRUD, membership, archive, visibility, default project creation workflow
**Branch:** `task/project-crud-membership-archive`
**Parent branch:** `feat/phase-2-project-space-api-domain`
**Parent spec:** `2026-06-04-phase-2-project-space-api-domain-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use `subagent-driven-development` or `executing-plans`. Track every checkbox.

**Goal:** Ship project APIs, member visibility, Owner/Member rules, project archive cascade to project chat folder/session, default columns, initial snapshot, audit, and `.http` smoke tests.

**Read before editing:** `CLAUDE.md`, `docs/DECISIONS.md`, `docs/architecture.md`, `docs/data-model.md`, spec above, `Project.cs`, `ProjectMember.cs`, `Column.cs`, `ProjectContextSnapshot.cs`, `ChatFolder.cs`, `ChatSession.cs`, `DomainErrorCodes.cs`, `Result.cs`, `Error.cs`, `HydraForgeDbContext.cs`, `Program.cs`, `AuthController.cs`, `ProblemDetailsMapper.cs`, `LoginUserHandler.cs`, `AuditService.cs`, `EfAuditLogWriter.cs`, existing test files read by architect.

**Files:**
- Modify: `src/HydraForge.Domain/Entities/ProjectSpace/Project.cs` add `ArchivedAt`.
- Modify: `src/HydraForge.Domain/Common/DomainErrorCodes.cs` add `Projects`/`Membership` codes.
- Modify: `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs` add project archive index and any default constraints.
- Create: `src/HydraForge.Application/Projects/ProjectModels.cs`, `ProjectRepository.cs`, `ProjectService.cs`, `ChatArchiveService.cs`.
- Create: `src/HydraForge.Infrastructure/Projects/EfProjectRepository.cs`, `EfChatArchiveService.cs`, `ProjectServiceCollectionExtensions.cs`.
- Create: `src/HydraForge.Server/Controllers/Projects/ProjectsController.cs`.
- Modify: `src/HydraForge.Server/Errors/ProblemDetailsMapper.cs`, `src/HydraForge.Server/Program.cs`.
- Create: `src/HydraForge.Infrastructure/Migrations/<timestamp>_AddProjectArchivedAt.cs` via EF.
- Tests: `tests/HydraForge.Domain.Tests/Entities/ProjectTests.cs`, `tests/HydraForge.Application.Tests/Projects/ProjectServiceTests.cs`, `tests/HydraForge.Infrastructure.Tests/Persistence/HydraForgeDbContextModelTests.cs`, `tests/HydraForge.Server.Tests/Projects/ProjectEndpointTests.cs`.
- Create smoke tests: `http/phase-2/projects.http`.

## Steps

- [ ] Write failing domain/model tests for `Project.ArchivedAt`, owner retention, and error-code constants: `PROJECT_NOT_FOUND`, `PROJECT_ARCHIVED`, `PROJECT_OWNER_REQUIRED`, `PROJECT_LAST_OWNER_REMOVAL_DENIED`, `PROJECT_MEMBERSHIP_DENIED`, `PROJECT_MEMBER_DUPLICATE`.
- [ ] Run: `dotnet test tests/HydraForge.Domain.Tests/HydraForge.Domain.Tests.csproj --filter ProjectTests`. Expect fail: missing `ArchivedAt`/codes.
- [ ] Add `ArchivedAt` to `Project`; add nested static classes to `DomainErrorCodes`.
- [ ] Write failing Application tests using in-memory fakes: create project inserts owner member, creates six default columns (`Backlog`, `Spec-ing`, `Planned`, `In Dev`, `In Review`, `Done`), creates snapshot, creates project chat folder, non-member gets not-found-style error, owner-only add/update/remove members, cannot remove last Owner, archive sets `Project.ArchivedAt` and calls `IChatArchiveService.ArchiveProjectAsync(projectId, archivedAt)`.
- [ ] Implement Application ports and `ProjectService`. Keep use case in Application; repository/file/EF stays outward. All expected failures return `Result<T>`.
- [ ] Write failing Infrastructure model test for `Project.ArchivedAt` and migration pending state.
- [ ] Implement EF repository + chat archive adapter. `EfChatArchiveService` must set `ChatFolder.ArchivedAt` for project folders and `ChatSession.ArchivedAt` for sessions under matching folder ids. Do not implement full chat CRUD.
- [ ] Add migration: `PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations add AddProjectArchivedAt --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server`.
- [ ] Wire DI in `ProjectServiceCollectionExtensions` and call from `Program.cs` after persistence.
- [ ] Write failing Server endpoint tests for auth required, create/list/get/update/archive/delete, member add/update/remove, non-member invisible, Owner-only member mutation, ProblemDetails correlation.
- [ ] Implement `ProjectsController` routes: `/api/projects`, `/api/projects/{projectId}`, `/api/projects/{projectId}/members`. Use current auth claims; if missing helper, create private `GetUserId()` and `IsAdmin()` in controller for this task.
- [ ] Update `ProblemDetailsMapper` mappings for new Project/Membership codes.
- [ ] Create `http/phase-2/projects.http` with login, create, list, get, update, member add/update/remove, archive, delete, non-member/invalid-id examples. Use variables: `@baseUrl`, `@token`, `@projectId`, `@userId`.
- [ ] Run: `dotnet test tests/HydraForge.Application.Tests/HydraForge.Application.Tests.csproj --filter ProjectServiceTests`; expect pass.
- [ ] Run: `dotnet test tests/HydraForge.Server.Tests/HydraForge.Server.Tests.csproj --filter ProjectEndpointTests`; expect pass.
- [ ] Run: `dotnet test`; expect pass.
- [ ] Commit on task branch: `git add src tests http && git commit -m "feat: add project API foundation"`.

**Acceptance:** every endpoint has `.http` smoke request; non-members cannot infer project existence; project archive uses `ArchivedAt`, cascades minimal chat archive, writes audit rows.
