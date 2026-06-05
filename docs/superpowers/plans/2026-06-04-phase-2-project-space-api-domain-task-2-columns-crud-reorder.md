# Task 2: Column CRUD, default columns, column reorder, ordering invariants
**Branch:** `task/columns-crud-reorder`
**Parent branch:** `feat/phase-2-project-space-api-domain`
**Parent spec:** `2026-06-04-phase-2-project-space-api-domain-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use `subagent-driven-development` or `executing-plans`.

**Goal:** Ship column list/create/update/delete/reorder APIs with dense positions and non-empty delete protection.

**Files:**
- Modify existing/read: `Column.cs`, `Card.cs`, `DomainErrorCodes.cs`, `HydraForgeDbContext.cs`, `ProblemDetailsMapper.cs`, `Program.cs`.
- Create: `src/HydraForge.Application/Columns/ColumnModels.cs`, `ColumnRepository.cs`, `ColumnService.cs`.
- Create: `src/HydraForge.Infrastructure/Columns/EfColumnRepository.cs`, `ColumnServiceCollectionExtensions.cs`.
- Create: `src/HydraForge.Server/Controllers/Projects/ColumnsController.cs`.
- Tests: `tests/HydraForge.Application.Tests/Columns/ColumnServiceTests.cs`, `tests/HydraForge.Server.Tests/Projects/ColumnEndpointTests.cs`.
- Create smoke tests: `http/phase-2/columns.http`.

## Steps

- [ ] Checkout `task/columns-crud-reorder`; merge latest parent before starting.
- [ ] Write failing Application tests: create column appends at max+1, reorder accepts exact column id list and rewrites dense `0..n-1`, update changes name/color/wip, delete empty column compacts positions, delete non-empty column returns `COLUMN_DELETE_NON_EMPTY`, invalid reorder ids return `COLUMN_INVALID_POSITION`, non-member gets project-not-found-style error.
- [ ] Add error codes: `COLUMN_NOT_FOUND`, `COLUMN_INVALID_POSITION`, `COLUMN_DELETE_NON_EMPTY`, `COLUMN_ARCHIVED_PROJECT_DENIED`.
- [ ] Implement `ColumnService` with `IProjectAuthorizationReader`/membership port from Task 1 or local shared port if Task 1 not merged. No controller direct DB.
- [ ] Implement EF repository using transaction for reorder/delete compaction. Query cards by `ColumnId` and `ArchivedAt == null` for non-empty check.
- [ ] Wire DI.
- [ ] Add/extend EF model tests for index `(ProjectId, Position)` and no global query filter.
- [ ] Write Server tests for `GET/POST /api/projects/{projectId}/columns`, `PUT/DELETE /api/projects/{projectId}/columns/{columnId}`, `PUT /api/projects/{projectId}/columns/reorder`, auth required, ProblemDetails with correlation.
- [ ] Implement `ColumnsController`. Request records: `CreateColumnRequest(Name, Color, WipLimit)`, `UpdateColumnRequest(Name, Color, WipLimit)`, `ReorderColumnsRequest(IReadOnlyList<Guid> ColumnIds)`.
- [ ] Update `ProblemDetailsMapper` for column codes.
- [ ] Create `http/phase-2/columns.http` covering list, create, update, reorder, delete, non-empty delete failure.
- [ ] Run targeted tests then `dotnet test`.
- [ ] Commit: `git add src tests http && git commit -m "feat: add column API"`.

**Acceptance:** positions remain dense after create/reorder/delete; deleting non-empty active column fails; `.http` covers every column endpoint.
