# Project List Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the unbounded project list (loads every project into memory, no search/sort/filter, infinite card grid) with a server-paginated list: table on desktop, existing card grid on mobile, search + sort + "my role" filter + archived toggle.

**Architecture:** Search/sort/role-filter/pagination move into the EF query itself (`EfProjectRepository.ListByUserIdAsync`), not in-memory — this is the actual fix for "unbounded." `ProjectService.GetAllAsync` threads the new params through and separately looks up each returned project's member count and the requester's own role (mirrors the existing `GetMemberCountsAsync` pattern). The frontend gets a new `ProjectFilterBar.vue` (search/role/sort/archived) and `ProjectListTable.vue` (desktop `UTable`), while the existing `ProjectList.vue`/`ProjectCard.vue` stay as the mobile view — same desktop-table/mobile-list split the board already uses (`BoardView.vue` vs `BoardMobileList.vue`).

**Tech Stack:** ASP.NET Core, EF Core (Npgsql `ILike`), Nuxt 4, Nuxt UI v4 `UTable`/`UPagination`, xUnit, Vitest.

## Global Constraints

- Business logic returns `Result<T, Error>` — never throw for expected failures.
- `xUnit` only, plain `Assert.*` — no FluentAssertions.
- Infrastructure tests follow the existing pattern in this codebase: skip (`return;`) when `HYDRAFORGE_TEST_CONNECTION_STRING` is unset, run for real against Postgres when it is set.
- Every `useApi()` call site wrapped in try/catch (D-40).
- No inline API path strings in Vue components/composables — use `ApiRoutes.*` from `app/lib/routes.ts`.
- No `console.log`/`console.error`/`console.warn` — use `useAppToast()`.
- `USelect` has no `clearable` prop — do not bind `null` as an item value; use an empty-string sentinel and translate to `null`/omitted at the call site (CLAUDE.md).
- Comments only when the WHY is non-obvious.

---

### Task 1: Backend — `ListByUserIdAsync` query rewrite (search/sort/role/pagination)

**Files:**
- Modify: `src/HydraForge.Application/Projects/IProjectRepository.cs`
- Modify: `src/HydraForge.Infrastructure/Projects/EfProjectRepository.cs`
- Test: `tests/HydraForge.Infrastructure.Tests/Projects/EfProjectRepositoryTests.cs`

**Interfaces:**
- Produces: `ProjectSortField` enum (`Name, CreatedAt, UpdatedAt`), `ProjectListPage(IReadOnlyList<Project> Items, int TotalCount)` record — both in `HydraForge.Application.Projects`. `IProjectRepository.ListByUserIdAsync(Guid userId, bool includeArchived, string? search, ProjectSortField sortBy, bool sortDescending, MemberRole? role, int skip, int take, CancellationToken ct = default) : Task<ProjectListPage>` — consumed by Task 2 (`ProjectService`).
- Produces: `IProjectMemberRepository.GetRolesByProjectAndUserAsync(IEnumerable<Guid> projectIds, Guid userId, CancellationToken ct = default) : Task<IReadOnlyDictionary<Guid, MemberRole>>` — consumed by Task 2.

- [ ] **Step 1: Write the failing tests**

Add to `tests/HydraForge.Infrastructure.Tests/Projects/EfProjectRepositoryTests.cs`, inside `EfProjectRepositoryTests`:

```csharp
    [Fact]
    public async Task ListByUserIdAsync_SearchFiltersByNameOrDescription()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfProjectRepository(context);
        var userId = Guid.NewGuid();

        var matchByName = new Project { Id = Guid.NewGuid(), Name = "Orders API", Description = "backend" };
        var matchByDesc = new Project { Id = Guid.NewGuid(), Name = "Marketing", Description = "orders dashboard" };
        var noMatch = new Project { Id = Guid.NewGuid(), Name = "Unrelated", Description = "nothing" };
        context.Projects.AddRange(matchByName, matchByDesc, noMatch);
        context.ProjectMembers.AddRange(
            new ProjectMember { Id = Guid.NewGuid(), ProjectId = matchByName.Id, UserId = userId, Role = MemberRole.Owner },
            new ProjectMember { Id = Guid.NewGuid(), ProjectId = matchByDesc.Id, UserId = userId, Role = MemberRole.Owner },
            new ProjectMember { Id = Guid.NewGuid(), ProjectId = noMatch.Id, UserId = userId, Role = MemberRole.Owner }
        );
        await context.SaveChangesAsync();

        var page = await repo.ListByUserIdAsync(userId, includeArchived: false, search: "orders", sortBy: ProjectSortField.Name, sortDescending: false, role: null, skip: 0, take: 20);

        Assert.Equal(2, page.TotalCount);
        Assert.DoesNotContain(page.Items, p => p.Id == noMatch.Id);
    }

    [Fact]
    public async Task ListByUserIdAsync_RoleFilterScopesToRequesterRole()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfProjectRepository(context);
        var userId = Guid.NewGuid();

        var owned = new Project { Id = Guid.NewGuid(), Name = "Owned Project", Description = "d" };
        var memberOf = new Project { Id = Guid.NewGuid(), Name = "Member Project", Description = "d" };
        context.Projects.AddRange(owned, memberOf);
        context.ProjectMembers.AddRange(
            new ProjectMember { Id = Guid.NewGuid(), ProjectId = owned.Id, UserId = userId, Role = MemberRole.Owner },
            new ProjectMember { Id = Guid.NewGuid(), ProjectId = memberOf.Id, UserId = userId, Role = MemberRole.Member }
        );
        await context.SaveChangesAsync();

        var page = await repo.ListByUserIdAsync(userId, includeArchived: false, search: null, sortBy: ProjectSortField.Name, sortDescending: false, role: MemberRole.Owner, skip: 0, take: 20);

        Assert.Single(page.Items);
        Assert.Equal(owned.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task ListByUserIdAsync_SkipTakePagesResultsAndReturnsTotalCount()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfProjectRepository(context);
        var userId = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
        {
            var project = new Project { Id = Guid.NewGuid(), Name = $"Project {i}", Description = "d" };
            context.Projects.Add(project);
            context.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = project.Id, UserId = userId, Role = MemberRole.Owner });
        }
        await context.SaveChangesAsync();

        var page = await repo.ListByUserIdAsync(userId, includeArchived: false, search: null, sortBy: ProjectSortField.Name, sortDescending: false, role: null, skip: 2, take: 2);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal("Project 2", page.Items[0].Name);
        Assert.Equal("Project 3", page.Items[1].Name);
    }
```

Add to `tests/HydraForge.Infrastructure.Tests/Projects/EfProjectMemberRepositoryTests`:

```csharp
    [Fact]
    public async Task GetRolesByProjectAndUserAsync_ReturnsRequesterRolePerProject()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfProjectMemberRepository(context);

        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = new Project { Id = projectId, Name = "Roles Test", Description = "d" };
        context.Projects.Add(project);
        context.ProjectMembers.AddRange(
            new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId, Role = MemberRole.Owner },
            new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = otherUserId, Role = MemberRole.Member }
        );
        await context.SaveChangesAsync();

        var roles = await repo.GetRolesByProjectAndUserAsync([projectId], userId);

        Assert.Equal(MemberRole.Owner, roles[projectId]);
    }
```

- [ ] **Step 2: Run tests to verify they compile and (if `HYDRAFORGE_TEST_CONNECTION_STRING` is set) fail**

```bash
dotnet test tests/HydraForge.Infrastructure.Tests --filter "FullyQualifiedName~EfProjectRepositoryTests|FullyQualifiedName~EfProjectMemberRepositoryTests"
```
Expected: compile error (`ProjectSortField`/`ProjectListPage`/`GetRolesByProjectAndUserAsync` don't exist yet).

- [ ] **Step 3: Add `ProjectSortField` and `ProjectListPage`, update `IProjectRepository`**

In `src/HydraForge.Application/Projects/IProjectRepository.cs`, find:

```csharp
using HydraForge.Domain.Entities.ProjectSpace;

namespace HydraForge.Application.Projects;

public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken ct = default);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListByUserIdAsync(Guid userId, bool includeArchived = false, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
}
```

Replace with:

```csharp
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Projects;

public enum ProjectSortField
{
    Name,
    CreatedAt,
    UpdatedAt,
}

public record ProjectListPage(IReadOnlyList<Project> Items, int TotalCount);

public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken ct = default);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectListPage> ListByUserIdAsync(
        Guid userId,
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        MemberRole? role,
        int skip,
        int take,
        CancellationToken ct = default
    );
    Task UpdateAsync(Project project, CancellationToken ct = default);
}
```

- [ ] **Step 4: Add `GetRolesByProjectAndUserAsync` to `IProjectMemberRepository`**

In the same file, find:

```csharp
public interface IProjectMemberRepository
{
    Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default);
    Task AddMemberAsync(ProjectMember member, CancellationToken ct = default);
    Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid id, CancellationToken ct = default);
}
```

Replace with:

```csharp
public interface IProjectMemberRepository
{
    Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(IEnumerable<Guid> projectIds, Guid userId, CancellationToken ct = default);
    Task AddMemberAsync(ProjectMember member, CancellationToken ct = default);
    Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 5: Implement `EfProjectRepository.ListByUserIdAsync`**

In `src/HydraForge.Infrastructure/Projects/EfProjectRepository.cs`, find:

```csharp
    public async Task<IReadOnlyList<Project>> ListByUserIdAsync(
        Guid userId,
        bool includeArchived = false,
        CancellationToken ct = default
    )
    {
        var projectIds = await context
            .ProjectMembers.Where(m => m.UserId == userId)
            .Select(m => m.ProjectId)
            .ToListAsync(ct);

        var query = context.Projects.Where(p => projectIds.Contains(p.Id));
        if (!includeArchived)
            query = query.Where(p => p.ArchivedAt == null);

        return await query.ToListAsync(ct);
    }
```

Replace with:

```csharp
    public async Task<ProjectListPage> ListByUserIdAsync(
        Guid userId,
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        MemberRole? role,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        var query =
            from p in context.Projects
            join m in context.ProjectMembers on p.Id equals m.ProjectId
            where m.UserId == userId
            select new { Project = p, MemberRole = m.Role };

        if (!includeArchived)
            query = query.Where(x => x.Project.ArchivedAt == null);

        if (role.HasValue)
            query = query.Where(x => x.MemberRole == role.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.Project.Name, $"%{search}%")
                || (x.Project.Description != null && EF.Functions.ILike(x.Project.Description, $"%{search}%"))
            );
        }

        var totalCount = await query.CountAsync(ct);

        query = sortBy switch
        {
            ProjectSortField.Name => sortDescending
                ? query.OrderByDescending(x => x.Project.Name)
                : query.OrderBy(x => x.Project.Name),
            ProjectSortField.UpdatedAt => sortDescending
                ? query.OrderByDescending(x => x.Project.UpdatedAt)
                : query.OrderBy(x => x.Project.UpdatedAt),
            _ => sortDescending
                ? query.OrderByDescending(x => x.Project.CreatedAt)
                : query.OrderBy(x => x.Project.CreatedAt),
        };

        var items = await query.Skip(skip).Take(take).Select(x => x.Project).ToListAsync(ct);

        return new ProjectListPage(items, totalCount);
    }
```

- [ ] **Step 6: Implement `EfProjectMemberRepository.GetRolesByProjectAndUserAsync`**

In the same file, find the `EfProjectMemberRepository` class's `GetMemberCountsAsync` method and add this method right after it:

```csharp
    public async Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(
        IEnumerable<Guid> projectIds,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var idList = projectIds.ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, MemberRole>();

        var roles = await context
            .ProjectMembers.Where(m => idList.Contains(m.ProjectId) && m.UserId == userId)
            .Select(m => new { m.ProjectId, m.Role })
            .ToListAsync(ct);

        return roles.ToDictionary(x => x.ProjectId, x => x.Role);
    }
```

Add `using HydraForge.Domain.Enums;` to the top of `EfProjectRepository.cs` if not already present (check first — `MemberRole` is referenced by the new signature).

- [ ] **Step 7: Run `dotnet build` — this will show every other call site that needs updating**

```bash
dotnet build
```
Expected: errors in `ProjectService.cs` (still calls the old `ListByUserIdAsync` signature) and the test fakes (`InMemoryProjectRepository`, `TestProjectRepository`, both `IProjectMemberRepository` fakes) — these are fixed in Tasks 2 and 3. Confirm the errors are ONLY in those files, not in `EfProjectRepository.cs`/`EfProjectMemberRepository.cs`/`IProjectRepository.cs` themselves.

- [ ] **Step 8: Run the new infrastructure tests**

```bash
dotnet test tests/HydraForge.Infrastructure.Tests --filter "FullyQualifiedName~EfProjectRepositoryTests|FullyQualifiedName~EfProjectMemberRepositoryTests"
```
Expected: if `HYDRAFORGE_TEST_CONNECTION_STRING` is unset, all pass trivially (early return). If set, all PASS against real Postgres.

- [ ] **Step 9: Commit**

```bash
git add src/HydraForge.Application/Projects/IProjectRepository.cs \
        src/HydraForge.Infrastructure/Projects/EfProjectRepository.cs \
        tests/HydraForge.Infrastructure.Tests/Projects/EfProjectRepositoryTests.cs
git commit -m "feat(api): move project list search/sort/role-filter/pagination into the EF query"
```

---

### Task 2: Backend — wire `ProjectService.GetAllAsync` through the new query

**Files:**
- Modify: `src/HydraForge.Application/Projects/ProjectContracts.cs`
- Modify: `src/HydraForge.Application/Projects/ProjectService.cs`
- Modify: `tests/HydraForge.Application.Tests/Projects/ProjectServiceTests.cs`

**Interfaces:**
- Consumes: `IProjectRepository.ListByUserIdAsync` (Task 1), `IProjectMemberRepository.GetRolesByProjectAndUserAsync` (Task 1).
- Produces: `ProjectListDto` gains `MemberRole MyRole` (last field). New `ProjectListPageDto(IReadOnlyList<ProjectListDto> Items, int TotalCount)`. `ProjectService.GetAllAsync(Guid requestUserId, bool includeArchived, string? search, ProjectSortField sortBy, bool sortDescending, MemberRole? role, int skip, int take, CancellationToken ct = default) : Task<Result<ProjectListPageDto>>` — consumed by Task 3 (`ProjectsController`).

- [ ] **Step 1: Write the failing tests**

Replace the existing `GetAllAsync` section (there is none dedicated yet) — add to `tests/HydraForge.Application.Tests/Projects/ProjectServiceTests.cs`, after `CreateAsync_CreatesProjectChatFolder`:

```csharp
    [Fact]
    public async Task GetAllAsync_ReturnsPagedProjectsWithMyRole()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        repo.Projects.Add(new Project { Id = projectId, Name = "Test Project", Description = "d" });
        memberRepo.Members.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId, Role = MemberRole.Owner });

        var result = await handler.GetAllAsync(userId, includeArchived: false, search: null, sortBy: ProjectSortField.Name, sortDescending: false, role: null, skip: 0, take: 20);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(MemberRole.Owner, result.Value.Items[0].MyRole);
        Assert.Equal(1, result.Value.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_ClampsTakeToMaxOneHundred()
    {
        var (repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var handler = new ProjectService(repo, columnRepo, memberRepo, snapshotRepo, chatService, snapshotRefresher, publisher, auditWriter);
        var userId = Guid.NewGuid();

        var result = await handler.GetAllAsync(userId, includeArchived: false, search: null, sortBy: ProjectSortField.Name, sortDescending: false, role: null, skip: 0, take: 9999);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, repo.LastTake);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~ProjectServiceTests"
```
Expected: compile errors — `GetAllAsync` still has the old signature, `repo.LastTake` doesn't exist, `MyRole` doesn't exist on `ProjectListDto`.

- [ ] **Step 3: Add `MyRole` to `ProjectListDto`, add `ProjectListPageDto`**

In `src/HydraForge.Application/Projects/ProjectContracts.cs`, find:

```csharp
public record ProjectListDto(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt,
    DateTime? ArchivedAt,
    int MemberCount
);
```

Replace with:

```csharp
public record ProjectListDto(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt,
    DateTime? ArchivedAt,
    int MemberCount,
    MemberRole MyRole
);

public record ProjectListPageDto(IReadOnlyList<ProjectListDto> Items, int TotalCount);
```

- [ ] **Step 4: Rewrite `ProjectService.GetAllAsync`**

In `src/HydraForge.Application/Projects/ProjectService.cs`, find:

```csharp
    public async Task<Result<IReadOnlyList<ProjectListDto>>> GetAllAsync(
        Guid requestUserId,
        bool includeArchived = false,
        CancellationToken ct = default
    )
    {
        var projects = await projectRepo.ListByUserIdAsync(requestUserId, includeArchived, ct);

        var memberCounts = await memberRepo.GetMemberCountsAsync(
            projects.Select(p => p.Id),
            ct
        );

        var result = projects
            .Select(project => new ProjectListDto(
                project.Id,
                project.Name,
                project.Description,
                project.CreatedAt,
                project.ArchivedAt,
                memberCounts.GetValueOrDefault(project.Id, 0)
            ))
            .ToList();

        return Result<IReadOnlyList<ProjectListDto>>.Success(result);
    }
```

Replace with:

```csharp
    public async Task<Result<ProjectListPageDto>> GetAllAsync(
        Guid requestUserId,
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        MemberRole? role,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        var clampedSkip = Math.Max(skip, 0);
        var clampedTake = Math.Clamp(take, 1, 100);

        var page = await projectRepo.ListByUserIdAsync(
            requestUserId,
            includeArchived,
            search,
            sortBy,
            sortDescending,
            role,
            clampedSkip,
            clampedTake,
            ct
        );

        var projectIds = page.Items.Select(p => p.Id).ToList();
        var memberCounts = await memberRepo.GetMemberCountsAsync(projectIds, ct);
        var myRoles = await memberRepo.GetRolesByProjectAndUserAsync(projectIds, requestUserId, ct);

        var result = page
            .Items.Select(project => new ProjectListDto(
                project.Id,
                project.Name,
                project.Description,
                project.CreatedAt,
                project.ArchivedAt,
                memberCounts.GetValueOrDefault(project.Id, 0),
                myRoles.GetValueOrDefault(project.Id, MemberRole.Member)
            ))
            .ToList();

        return Result<ProjectListPageDto>.Success(new ProjectListPageDto(result, page.TotalCount));
    }
```

- [ ] **Step 5: Update `InMemoryProjectRepository` and `InMemoryProjectMemberRepository` fakes**

In `tests/HydraForge.Application.Tests/Projects/ProjectServiceTests.cs`, find:

```csharp
    public Task<IReadOnlyList<Project>> ListByUserIdAsync(Guid userId, bool includeArchived = false, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Project>>(Projects);
```

Replace with (this fake ignores real membership scoping, matching its existing simplified behavior, but now implements search/sort/skip/take meaningfully and records `LastTake` so `GetAllAsync_ClampsTakeToMaxOneHundred` can assert on it):

```csharp
    public int LastTake { get; private set; }

    public Task<ProjectListPage> ListByUserIdAsync(
        Guid userId,
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        MemberRole? role,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        LastTake = take;

        var filtered = Projects.AsEnumerable();
        if (!includeArchived)
            filtered = filtered.Where(p => p.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(p =>
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (p.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            );

        IEnumerable<Project> sorted = sortBy switch
        {
            ProjectSortField.Name => sortDescending ? filtered.OrderByDescending(p => p.Name) : filtered.OrderBy(p => p.Name),
            ProjectSortField.UpdatedAt => sortDescending ? filtered.OrderByDescending(p => p.UpdatedAt) : filtered.OrderBy(p => p.UpdatedAt),
            _ => sortDescending ? filtered.OrderByDescending(p => p.CreatedAt) : filtered.OrderBy(p => p.CreatedAt),
        };

        var all = sorted.ToList();
        var page = all.Skip(skip).Take(take).ToList();
        return Task.FromResult(new ProjectListPage(page, all.Count));
    }
```

Find `internal class InMemoryProjectMemberRepository` and its `GetMemberCountsAsync` method; add this method right after it:

```csharp
    public Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(
        IEnumerable<Guid> projectIds,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var idList = projectIds.ToList();
        var roles = Members
            .Where(m => idList.Contains(m.ProjectId) && m.UserId == userId)
            .ToDictionary(m => m.ProjectId, m => m.Role);
        return Task.FromResult<IReadOnlyDictionary<Guid, MemberRole>>(roles);
    }
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~ProjectServiceTests"
```
Expected: PASS (all `ProjectServiceTests`, including the two new ones).

```bash
dotnet build
```
Expected: only `ProjectsController.cs` and `tests/HydraForge.Server.Tests/Projects/ProjectsControllerTests.cs` should still have errors (fixed in Task 3).

- [ ] **Step 7: Commit**

```bash
git add src/HydraForge.Application/Projects/ProjectContracts.cs \
        src/HydraForge.Application/Projects/ProjectService.cs \
        tests/HydraForge.Application.Tests/Projects/ProjectServiceTests.cs
git commit -m "feat(api): thread search/sort/role/pagination through ProjectService.GetAllAsync"
```

---

### Task 3: Backend — `ProjectsController.List` query params + paged response

**Files:**
- Modify: `src/HydraForge.Application/Projects/ProjectModels.cs`
- Modify: `src/HydraForge.Server/Controllers/Projects/ProjectsController.cs`
- Modify: `tests/HydraForge.Server.Tests/Projects/ProjectsControllerTests.cs`

**Interfaces:**
- Consumes: `ProjectService.GetAllAsync` (Task 2).
- Produces: `ProjectListResponse` gains `MemberRole MyRole` (last field). New `ProjectListPageResponse(IReadOnlyList<ProjectListResponse> Items, int TotalCount)`. `GET /api/Projects` now accepts `includeArchived`, `search`, `sortBy`, `sortDescending`, `role`, `skip`, `take` query params and returns `ProjectListPageResponse`.

- [ ] **Step 1: Write the failing test**

Add to `tests/HydraForge.Server.Tests/Projects/ProjectsControllerTests.cs` (find the existing test class with `[Fact]` methods for this controller and add alongside them):

```csharp
    [Fact]
    public async Task List_ReturnsPagedResponseWithMyRole()
    {
        using var factory = new ProjectsTestWebApplicationFactory();
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        factory.AddProject(new Project { Id = projectId, Name = "Listed Project", Description = "d" });
        factory.AddMember(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId, Role = MemberRole.Owner });
        var token = factory.IssueToken(userId, "testuser", isAdmin: false);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/Projects?search=Listed&sortBy=Name&take=10");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ProjectListPageResponse>();
        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.Equal("Listed Project", body.Items[0].Name);
        Assert.Equal(MemberRole.Owner, body.Items[0].MyRole);
        Assert.Equal(1, body.TotalCount);
    }
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/HydraForge.Server.Tests --filter "FullyQualifiedName~List_ReturnsPagedResponseWithMyRole"
```
Expected: compile error — `ProjectListPageResponse` doesn't exist, `ListByUserIdAsync`/fakes still on old signature.

- [ ] **Step 3: Add `MyRole` to `ProjectListResponse`, add `ProjectListPageResponse`**

In `src/HydraForge.Application/Projects/ProjectModels.cs`, find:

```csharp
public record ProjectListResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime? ArchivedAt,
    int MemberCount
);
```

Replace with:

```csharp
public record ProjectListResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime? ArchivedAt,
    int MemberCount,
    MemberRole MyRole
);

public record ProjectListPageResponse(IReadOnlyList<ProjectListResponse> Items, int TotalCount);
```

- [ ] **Step 4: Update `ProjectsController.List`**

In `src/HydraForge.Server/Controllers/Projects/ProjectsController.cs`, find:

```csharp
    [HttpGet]
    [ProducesResponseType(typeof(List<ProjectListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool includeArchived = false)
    {
        var userId = User.GetRequiredUserId();

        var result = await projectService.GetAllAsync(userId, includeArchived);

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = result
            .Value.Select(p => new ProjectListResponse(
                p.Id,
                p.Name,
                p.Description,
                p.CreatedAt,
                p.ArchivedAt,
                p.MemberCount
            ))
            .ToList();
        return Ok(response);
    }
```

Replace with:

```csharp
    [HttpGet]
    [ProducesResponseType(typeof(ProjectListPageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeArchived = false,
        [FromQuery] string? search = null,
        [FromQuery] ProjectSortField sortBy = ProjectSortField.CreatedAt,
        [FromQuery] bool sortDescending = true,
        [FromQuery] MemberRole? role = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20
    )
    {
        var userId = User.GetRequiredUserId();

        var result = await projectService.GetAllAsync(
            userId,
            includeArchived,
            search,
            sortBy,
            sortDescending,
            role,
            skip,
            take
        );

        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error);
        }

        var response = new ProjectListPageResponse(
            [
                .. result.Value.Items.Select(p => new ProjectListResponse(
                    p.Id,
                    p.Name,
                    p.Description,
                    p.CreatedAt,
                    p.ArchivedAt,
                    p.MemberCount,
                    p.MyRole
                )),
            ],
            result.Value.TotalCount
        );
        return Ok(response);
    }
```

- [ ] **Step 5: Update `TestProjectRepository` and `TestProjectMemberRepository` fakes**

In `tests/HydraForge.Server.Tests/Projects/ProjectsControllerTests.cs`, find:

```csharp
    public Task<IReadOnlyList<Project>> ListByUserIdAsync(Guid userId, bool includeArchived = false, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Project>>(_projects);
```

Replace with:

```csharp
    public Task<ProjectListPage> ListByUserIdAsync(
        Guid userId,
        bool includeArchived,
        string? search,
        ProjectSortField sortBy,
        bool sortDescending,
        MemberRole? role,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        var filtered = _projects.AsEnumerable();
        if (!includeArchived)
            filtered = filtered.Where(p => p.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(p =>
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (p.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            );

        var all = filtered.ToList();
        var page = all.Skip(skip).Take(take).ToList();
        return Task.FromResult(new ProjectListPage(page, all.Count));
    }
```

Find `internal class TestProjectMemberRepository`'s `GetMemberCountsAsync` method and add this method right after it:

```csharp
    public Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(
        IEnumerable<Guid> projectIds,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var idList = projectIds.ToList();
        var roles = _members
            .Where(m => idList.Contains(m.ProjectId) && m.UserId == userId)
            .ToDictionary(m => m.ProjectId, m => m.Role);
        return Task.FromResult<IReadOnlyDictionary<Guid, MemberRole>>(roles);
    }
```

- [ ] **Step 6: Run test to verify it passes**

```bash
dotnet test tests/HydraForge.Server.Tests --filter "FullyQualifiedName~List_ReturnsPagedResponseWithMyRole"
```
Expected: PASS.

```bash
dotnet build && dotnet test
```
Expected: full solution builds, all tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/HydraForge.Application/Projects/ProjectModels.cs \
        src/HydraForge.Server/Controllers/Projects/ProjectsController.cs \
        tests/HydraForge.Server.Tests/Projects/ProjectsControllerTests.cs
git commit -m "feat(api): GET /api/Projects accepts search/sort/role/pagination, returns ProjectListPageResponse"
```

---

### Task 4: Regenerate frontend API types

**Files:**
- Modify: `src/web-ui/app/types/api.d.ts` (generated — regenerate, do not hand-edit)

- [ ] **Step 1: Start Postgres + MinIO**

```bash
docker compose up -d postgres minio
```

- [ ] **Step 2: Start the server in Development**

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/HydraForge.Server &
```
Wait for `Now listening on: http://localhost:5000`.

- [ ] **Step 3: Regenerate the types**

```bash
cd src/web-ui && pnpm run generate:api-types && cd -
```

- [ ] **Step 4: Verify the new fields are present**

```bash
grep -A 8 'ProjectListResponse:' src/web-ui/app/types/api.d.ts
grep -B2 -A 4 'ProjectListPageResponse:' src/web-ui/app/types/api.d.ts
```
Expected: `myRole` field on `ProjectListResponse`; `ProjectListPageResponse` schema with `items`/`totalCount`.

- [ ] **Step 5: Stop the server**

```bash
kill %1
```

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/types/api.d.ts
git commit -m "chore: regenerate API types for paged project list"
```

---

### Task 5: Frontend — `ProjectFilterBar.vue`

**Files:**
- Create: `src/web-ui/app/components/project/ProjectFilterBar.vue`
- Test: `src/web-ui/app/components/project/__tests__/ProjectFilterBar.test.ts`

**Interfaces:**
- Produces: `ProjectFilterBar` component, props `{ search: string, role: string, sortBy: string, sortDescending: boolean, showArchived: boolean }` (role uses `''` for "all roles", never `null`, per CLAUDE.md's `USelect` note), emits `'update:search'`, `'update:role'`, `'update:sortBy'`, `'update:sortDescending'`, `'update:showArchived'`. Consumed by Task 7 (`projects/index.vue`).

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/components/project/__tests__/ProjectFilterBar.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ProjectFilterBar from '~/components/project/ProjectFilterBar.vue'

const defaultProps = {
  search: '',
  role: '',
  sortBy: 'CreatedAt',
  sortDescending: true,
  showArchived: false
}

describe('ProjectFilterBar', () => {
  it('emits update:search when the search input changes', async () => {
    const wrapper = await mountSuspended(ProjectFilterBar, { props: defaultProps })
    await wrapper.find('[data-testid="project-search-input"] input').setValue('orders')
    expect(wrapper.emitted('update:search')?.[0]).toEqual(['orders'])
  })

  it('emits update:showArchived when the archived switch toggles', async () => {
    const wrapper = await mountSuspended(ProjectFilterBar, { props: defaultProps })
    await wrapper.find('input[type="checkbox"]').setValue(true)
    expect(wrapper.emitted('update:showArchived')?.[0]).toEqual([true])
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd src/web-ui && pnpm vitest run components/project/__tests__/ProjectFilterBar.test.ts
```
Expected: FAIL — cannot find module `~/components/project/ProjectFilterBar.vue`.

- [ ] **Step 3: Implement `ProjectFilterBar.vue`**

Create `src/web-ui/app/components/project/ProjectFilterBar.vue`:

```vue
<script setup lang="ts">
const props = defineProps<{
  search: string
  role: string
  sortBy: string
  sortDescending: boolean
  showArchived: boolean
}>()

const emit = defineEmits<{
  'update:search': [string]
  'update:role': [string]
  'update:sortBy': [string]
  'update:sortDescending': [boolean]
  'update:showArchived': [boolean]
}>()

const roleOptions = [
  { label: 'All roles', value: '' },
  { label: 'Owner', value: 'Owner' },
  { label: 'Member', value: 'Member' }
]

const sortOptions = [
  { label: 'Newest first', value: 'CreatedAt:true' },
  { label: 'Oldest first', value: 'CreatedAt:false' },
  { label: 'Name (A-Z)', value: 'Name:false' },
  { label: 'Name (Z-A)', value: 'Name:true' },
  { label: 'Recently updated', value: 'UpdatedAt:true' }
]

const sortValue = computed({
  get: () => `${props.sortBy}:${props.sortDescending}`,
  set: (val: string) => {
    const [field, desc] = val.split(':')
    emit('update:sortBy', field!)
    emit('update:sortDescending', desc === 'true')
  }
})
</script>

<template>
  <div class="flex flex-wrap items-center gap-3">
    <UInput
      :model-value="search"
      placeholder="Search projects..."
      icon="i-lucide-search"
      class="flex-1 min-w-[200px]"
      data-testid="project-search-input"
      @update:model-value="emit('update:search', String($event))"
    />
    <USelect
      :model-value="role"
      :items="roleOptions"
      class="w-36"
      data-testid="project-role-select"
      @update:model-value="emit('update:role', String($event))"
    />
    <USelect
      v-model="sortValue"
      :items="sortOptions"
      class="w-44"
      data-testid="project-sort-select"
    />
    <div class="flex items-center gap-2">
      <USwitch
        :model-value="showArchived"
        @update:model-value="emit('update:showArchived', Boolean($event))"
      />
      <span class="text-sm text-muted">Show archived</span>
    </div>
  </div>
</template>
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
cd src/web-ui && pnpm vitest run components/project/__tests__/ProjectFilterBar.test.ts
```
Expected: PASS (both tests).

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/components/project/ProjectFilterBar.vue src/web-ui/app/components/project/__tests__/ProjectFilterBar.test.ts
git commit -m "feat(web): add ProjectFilterBar (search/role/sort/archived)"
```

---

### Task 6: Frontend — `ProjectListTable.vue` (desktop)

**Files:**
- Create: `src/web-ui/app/components/project/ProjectListTable.vue`
- Test: `src/web-ui/app/components/project/__tests__/ProjectListTable.test.ts`

**Interfaces:**
- Produces: `ProjectListTable` component, props `{ projects: ProjectListResponse[], loading: boolean }`, emits `'select': [projectId: string]`, `'edit': [projectId: string]`, `'toggle-archive': [project: { id: string, name: string, archivedAt: string | null }]`. Consumed by Task 7.

- [ ] **Step 1: Write the failing test**

Create `src/web-ui/app/components/project/__tests__/ProjectListTable.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ProjectListTable from '~/components/project/ProjectListTable.vue'

const makeProject = (overrides = {}) => ({
  id: 'p1',
  name: 'Orders API',
  description: 'desc',
  createdAt: new Date().toISOString(),
  archivedAt: null,
  memberCount: 3,
  myRole: 'Owner',
  ...overrides
})

describe('ProjectListTable', () => {
  it('renders project rows', async () => {
    const wrapper = await mountSuspended(ProjectListTable, {
      props: { projects: [makeProject()], loading: false }
    })
    expect(wrapper.text()).toContain('Orders API')
    expect(wrapper.text()).toContain('Owner')
  })

  it('shows an Archived badge for archived projects', async () => {
    const wrapper = await mountSuspended(ProjectListTable, {
      props: { projects: [makeProject({ archivedAt: new Date().toISOString() })], loading: false }
    })
    expect(wrapper.text()).toContain('Archived')
  })

  it('emits edit when the edit button is clicked', async () => {
    const wrapper = await mountSuspended(ProjectListTable, {
      props: { projects: [makeProject()], loading: false }
    })
    await wrapper.find('[data-testid="edit-p1"]').trigger('click')
    expect(wrapper.emitted('edit')?.[0]).toEqual(['p1'])
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd src/web-ui && pnpm vitest run components/project/__tests__/ProjectListTable.test.ts
```
Expected: FAIL — cannot find module.

- [ ] **Step 3: Implement `ProjectListTable.vue`**

Create `src/web-ui/app/components/project/ProjectListTable.vue`:

```vue
<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import type { components } from '~/types/api'

type ProjectListResponse = components['schemas']['ProjectListResponse']

defineProps<{
  projects: ProjectListResponse[]
  loading: boolean
}>()

const emit = defineEmits<{
  select: [projectId: string]
  edit: [projectId: string]
  'toggle-archive': [project: { id: string, name: string, archivedAt: string | null }]
}>()

const columns: TableColumn<ProjectListResponse>[] = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'memberCount', header: 'Members' },
  { accessorKey: 'myRole', header: 'My Role' },
  { accessorKey: 'createdAt', header: 'Created' },
  { id: 'actions', header: '' }
]

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString()
}
</script>

<template>
  <UTable
    :data="projects"
    :columns="columns"
    :loading="loading"
    class="w-full"
    @select="(_e, row) => emit('select', row.original.id)"
  >
    <template #name-cell="{ row }">
      <div class="flex items-center gap-2">
        <span class="font-medium">{{ row.original.name }}</span>
        <UBadge
          v-if="row.original.archivedAt"
          variant="subtle"
          size="xs"
          color="neutral"
        >
          Archived
        </UBadge>
      </div>
    </template>
    <template #createdAt-cell="{ row }">
      {{ formatDate(row.original.createdAt) }}
    </template>
    <template #actions-cell="{ row }">
      <div
        class="flex justify-end gap-1"
        @click.stop
      >
        <UButton
          icon="i-lucide-pencil"
          variant="ghost"
          size="xs"
          :data-testid="`edit-${row.original.id}`"
          @click="emit('edit', row.original.id)"
        />
        <UButton
          :icon="row.original.archivedAt ? 'i-lucide-archive-restore' : 'i-lucide-archive'"
          variant="ghost"
          size="xs"
          :data-testid="`toggle-archive-${row.original.id}`"
          @click="emit('toggle-archive', { id: row.original.id, name: row.original.name, archivedAt: row.original.archivedAt })"
        />
      </div>
    </template>
  </UTable>
</template>
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
cd src/web-ui && pnpm vitest run components/project/__tests__/ProjectListTable.test.ts
```
Expected: PASS (all 3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/web-ui/app/components/project/ProjectListTable.vue src/web-ui/app/components/project/__tests__/ProjectListTable.test.ts
git commit -m "feat(web): add ProjectListTable desktop view"
```

---

### Task 7: Frontend — wire `projects/index.vue`

**Files:**
- Modify: `src/web-ui/app/pages/projects/index.vue`

**Interfaces:**
- Consumes: `ProjectFilterBar` (Task 5), `ProjectListTable` (Task 6), existing `ProjectList` (mobile, unchanged).

- [ ] **Step 1: Rewrite `projects/index.vue`**

Replace the full contents of `src/web-ui/app/pages/projects/index.vue`:

```vue
<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes, UiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import ProjectFilterBar from '~/components/project/ProjectFilterBar.vue'
import ProjectListTable from '~/components/project/ProjectListTable.vue'

definePageMeta({ middleware: ['auth'] })

type ProjectListResponse = components['schemas']['ProjectListResponse']
type ProjectListPageResponse = components['schemas']['ProjectListPageResponse']

const projects = ref<ProjectListResponse[]>([])
const totalCount = ref(0)
const loading = ref(true)
const showCreateModal = ref(false)
const showArchived = ref(false)

const search = ref('')
const role = ref('')
const sortBy = ref('CreatedAt')
const sortDescending = ref(true)
const page = ref(1)
const pageSize = 20

const api = useApi()
const toast = useAppToast()

const archiveTarget = ref<{ id: string, name: string } | null>(null)
const showArchiveConfirm = computed({
  get: () => archiveTarget.value !== null,
  set: (v: boolean) => { if (!v) archiveTarget.value = null }
})

const editingProject = ref<{ id: string, name: string, description: string | null } | null>(null)

function handleEditProject(projectId: string) {
  const project = projects.value.find(p => p.id === projectId)
  if (project) {
    editingProject.value = { id: project.id, name: project.name, description: project.description ?? null }
  }
}

async function fetchProjects() {
  loading.value = true
  try {
    const params = new URLSearchParams()
    if (showArchived.value) params.set('includeArchived', 'true')
    if (search.value) params.set('search', search.value)
    if (role.value) params.set('role', role.value)
    params.set('sortBy', sortBy.value)
    params.set('sortDescending', String(sortDescending.value))
    params.set('skip', String((page.value - 1) * pageSize))
    params.set('take', String(pageSize))

    const { data } = await api.GET<ProjectListPageResponse>(`${ApiRoutes.Projects.list()}?${params}`)
    projects.value = data?.items ?? []
    totalCount.value = data?.totalCount ?? 0
  } catch (e: unknown) {
    const message = e instanceof Error ? e.message : 'Failed to load projects'
    toast.error(message)
  } finally {
    loading.value = false
  }
}

async function handleToggleArchive(project: { id: string, name: string, archivedAt: string | null }) {
  const isArchiving = !project.archivedAt
  if (isArchiving) {
    archiveTarget.value = { id: project.id, name: project.name }
    return
  }
  try {
    await api.POST(ApiRoutes.Projects.toggleArchive(project.id))
    toast.success('Project restored')
    fetchProjects()
  } catch {
    toast.error('Failed to restore project')
  }
}

async function confirmArchive() {
  if (!archiveTarget.value) return
  const id = archiveTarget.value.id
  archiveTarget.value = null
  try {
    await api.POST(ApiRoutes.Projects.toggleArchive(id))
    toast.success('Project archived')
    fetchProjects()
  } catch {
    toast.error('Failed to archive project')
  }
}

watch(showArchived, () => { page.value = 1; fetchProjects() })
watch(role, () => { page.value = 1; fetchProjects() })
watch(sortBy, () => { page.value = 1; fetchProjects() })
watch(sortDescending, () => { page.value = 1; fetchProjects() })
watch(page, () => fetchProjects())

let searchTimer: ReturnType<typeof setTimeout> | null = null
watch(search, () => {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    page.value = 1
    fetchProjects()
    searchTimer = null
  }, 300)
})

function onProjectSelect(projectId: string) {
  navigateTo(UiRoutes.Projects.Board(projectId))
}

function onProjectCreated() {
  showCreateModal.value = false
  fetchProjects()
  toast.success('Project created')
}

onMounted(() => fetchProjects())
</script>

<template>
  <div class="min-h-screen flex flex-col">
    <div class="p-4 sm:p-6 lg:p-8 pb-0 w-full flex-1 flex flex-col">
      <div class="flex items-center justify-between pb-4 mb-4 border-b border-gray-200 dark:border-gray-700">
        <h1 class="text-2xl font-bold">
          Projects
        </h1>
        <UButton @click="showCreateModal = true">
          New Project
        </UButton>
      </div>

      <ProjectFilterBar
        v-model:search="search"
        v-model:role="role"
        v-model:sort-by="sortBy"
        v-model:sort-descending="sortDescending"
        v-model:show-archived="showArchived"
        class="mb-6"
      />

      <div class="flex-1">
        <ProjectListTable
          class="hidden md:block"
          :projects="projects"
          :loading="loading"
          @select="onProjectSelect"
          @toggle-archive="handleToggleArchive"
          @edit="handleEditProject"
        />
        <ProjectList
          class="md:hidden"
          :projects="projects"
          :loading="loading"
          @select="onProjectSelect"
          @toggle-archive="handleToggleArchive"
          @edit="handleEditProject"
        />

        <div
          v-if="totalCount > pageSize"
          class="flex justify-center py-6"
        >
          <UPagination
            v-model:page="page"
            :total="totalCount"
            :items-per-page="pageSize"
          />
        </div>
      </div>
    </div>

    <ProjectCreateModal
      v-model:open="showCreateModal"
      @created="onProjectCreated"
    />

    <ConfirmDialog
      v-model:open="showArchiveConfirm"
      title="Archive project"
      :message="archiveTarget ? `Archive ${archiveTarget.name}? This will hide it from the default project list.` : ''"
      confirm-text="Archive"
      @confirm="confirmArchive"
    />

    <ProjectEditModal
      v-if="editingProject"
      :project-id="editingProject.id"
      :initial-name="editingProject.name"
      :initial-description="editingProject.description"
      @close="editingProject = null"
      @updated="editingProject = null; fetchProjects()"
    />
  </div>
</template>
```

- [ ] **Step 2: Write the failing test for debounced search + filter refetch**

Create `src/web-ui/app/pages/projects/__tests__/index.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mountSuspended, mockNuxtImport } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import ProjectsPage from '~/pages/projects/index.vue'

const mockGET = vi.fn()

mockNuxtImport('useApi', () => () => ({
  GET: mockGET,
  POST: vi.fn(),
  PUT: vi.fn(),
  DELETE: vi.fn()
}))

mockNuxtImport('useToast', () => () => ({ add: vi.fn() }))

describe('projects/index.vue', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    mockGET.mockReset()
    mockGET.mockResolvedValue({ data: { items: [], totalCount: 0 }, error: undefined })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('fetches on mount with default pagination params', async () => {
    await mountSuspended(ProjectsPage, {
      global: {
        stubs: {
          ProjectCreateModal: true,
          ProjectEditModal: true,
          ProjectListTable: true,
          ProjectList: true,
          ProjectFilterBar: true
        }
      }
    })
    await flushPromises()

    expect(mockGET).toHaveBeenCalledTimes(1)
    const [url] = mockGET.mock.calls[0]!
    expect(url).toContain('skip=0')
    expect(url).toContain('take=20')
    expect(url).toContain('sortBy=CreatedAt')
  })

  it('debounces search input by 300ms before refetching', async () => {
    const wrapper = await mountSuspended(ProjectsPage, {
      global: {
        stubs: {
          ProjectCreateModal: true,
          ProjectEditModal: true,
          ProjectListTable: true,
          ProjectList: true,
          ProjectFilterBar: true
        }
      }
    })
    await flushPromises()
    mockGET.mockClear()

    ;(wrapper.vm as any).search = 'orders'
    await flushPromises()
    expect(mockGET).not.toHaveBeenCalled()

    vi.advanceTimersByTime(300)
    await flushPromises()

    expect(mockGET).toHaveBeenCalledTimes(1)
    const [url] = mockGET.mock.calls[0]!
    expect(url).toContain('search=orders')
  })
})
```

- [ ] **Step 3: Run the test to verify it fails or passes against the rewrite**

```bash
cd src/web-ui && pnpm vitest run pages/projects/__tests__/index.test.ts
```
Expected: PASS — Step 1 already implemented the debounce/fetch logic this test exercises. If it fails, the mismatch is between this test's expectations and the Step 1 implementation; fix `projects/index.vue` to match (do not weaken the test).

- [ ] **Step 4: Typecheck + lint**

```bash
cd src/web-ui && pnpm run typecheck && pnpm run lint
```
Expected: zero errors.

- [ ] **Step 5: Manual verification**

Run the full stack (`docker compose up -d postgres minio`, `dotnet run --project src/HydraForge.Server` in Development, `cd src/web-ui && pnpm dev`). In a browser: open `/projects` — desktop shows the table, mobile viewport shows the existing card grid. Type in search — list filters after ~300ms. Switch role filter to "Owner" — only owned projects show. Change sort — order changes. Create 25+ projects (or lower `pageSize` temporarily to test with fewer) and confirm pagination controls appear and page through correctly. Toggle "Show archived" — archived projects appear with the badge. Edit and archive/restore still work from both the table and mobile card menu.

- [ ] **Step 6: Commit**

```bash
git add src/web-ui/app/pages/projects/index.vue src/web-ui/app/pages/projects/__tests__/index.test.ts
git commit -m "feat(web): wire paginated/filterable project list into projects page"
```

---

### Task 8: Full verification pass

- [ ] **Step 1: Backend**

```bash
dotnet build
dotnet test
```
Expected: 0 errors, all tests pass.

- [ ] **Step 2: Frontend**

```bash
cd src/web-ui && pnpm run typecheck && pnpm run lint && pnpm test && pnpm run build
```
Expected: 0 errors, all tests pass, production build succeeds.

- [ ] **Step 3: Manual regression check**

Confirm project creation, edit, archive/restore, and navigating into a project's board still all work end to end — this task changed the list endpoint's response shape (`List<ProjectListResponse>` → `ProjectListPageResponse`), so any other consumer of `GET /api/Projects` (there should be none besides `projects/index.vue`) would break silently otherwise.

```bash
grep -rn "ApiRoutes.Projects.list()" src/web-ui/app
```
Expected: only `projects/index.vue` calls it.
