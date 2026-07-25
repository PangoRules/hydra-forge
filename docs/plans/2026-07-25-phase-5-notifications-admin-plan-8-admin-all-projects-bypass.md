# Plan 8: Admin All-Projects Bypass

**Branch:** `task/admin-all-projects-bypass`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 8

## Task

Give admins a project-membership bypass everywhere membership is checked. `BoardHub`/`PresenceHub` already gate on `IsInRole` — no code change needed there beyond Task 1. `ProjectsController.List` needs the all-projects listing variant. Everything else lives in the Application layer.

**Revision note:** the original version of this plan estimated "Medium" effort against 4 services (`CardService`, `CommentService`, `CardRelationshipService`, `ProjectService`), and a controller-level `ClaimsPrincipal` extension method as the primary mechanism, with a "Files to modify" list of 9 controllers. A grep across the whole Application layer for the actual membership-check call (`GetByProjectAndUserAsync`) found it in **10 files, 57 call sites** — 6 more services than originally scoped, and none of the listed controllers (`CardsController`, `ColumnsController`, `CardCommentsController`, etc.) call it directly; they delegate to the services, which do their own check. Only `ProjectSnapshotController` calls it directly. This plan is rewritten around that reality: effort is Large, the bypass logic lives in the Application services via a shared guard helper (Application code can't depend on `ClaimsPrincipal` — that's an ASP.NET Core type), and the `ClaimsPrincipal` extension is kept only for the one real controller call site.

## Files to create

- `src/HydraForge.Application/Auth/MembershipGuard.cs` — shared `HasAccessAsync(IUserRepository, IProjectMemberRepository, Guid projectId, Guid userId, ct)` helper, used by all 10 services below instead of hand-duplicating `isAdmin || membership != null` at each of the 57 call sites
- `src/HydraForge.Server/Auth/ClaimsPrincipalExtensions.cs` — `IsProjectMemberOrAdmin` extension method, for the one controller call site (`ProjectSnapshotController`)
- `tests/HydraForge.Application.Tests/Auth/MembershipGuardTests.cs`

## Files to modify

Application services — each call site's `if (membership == null) return Result.Failure(...)` becomes `if (!await MembershipGuard.HasAccessAsync(...)) return Result.Failure(...)`:

| File | Call sites |
|---|---|
| `src/HydraForge.Application/Cards/CardService.cs` | 12 |
| `src/HydraForge.Application/Checklist/ChecklistService.cs` | 8 |
| `src/HydraForge.Application/Plans/PlanService.cs` | 8 |
| `src/HydraForge.Application/Specs/SpecService.cs` | 7 |
| `src/HydraForge.Application/Columns/ColumnService.cs` | 6 |
| `src/HydraForge.Application/Attachments/AttachmentService.cs` | 4 |
| `src/HydraForge.Application/Cards/CardRelationshipService.cs` | 4 |
| `src/HydraForge.Application/Projects/ProjectMemberService.cs` | 4 |
| `src/HydraForge.Application/Projects/ProjectService.cs` | 3 |
| `src/HydraForge.Application/Comments/CommentService.cs` | 1 |

Plus:

- `src/HydraForge.Application/Auth/LoginUserHandler.cs` — add `Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default)` to `IUserRepository` (this is where the interface actually lives — not its own file)
- `src/HydraForge.Infrastructure/Auth/EfUserRepository.cs` — implement `IsAdminAsync`
- `src/HydraForge.Server/Controllers/Projects/ProjectsController.cs` — `List` passes `isAdmin` through to `ProjectService.GetAllAsync`
- `src/HydraForge.Application/Projects/IProjectRepository.cs` — add `ListAllAsync`
- `src/HydraForge.Infrastructure/Projects/EfProjectRepository.cs` (or wherever the implementation lives) — implement `ListAllAsync`
- `src/HydraForge.Server/Controllers/Projects/ProjectSnapshotController.cs` — the one controller with a direct membership check; swap to `IsProjectMemberOrAdmin`

## Implementation steps

### Step 1: Add `IsAdminAsync` to `IUserRepository`

In `src/HydraForge.Application/Auth/LoginUserHandler.cs`, add to the interface:

```csharp
Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default);
```

In `src/HydraForge.Infrastructure/Auth/EfUserRepository.cs`, implement:

```csharp
public async Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default)
{
    return await context.Users.AnyAsync(u => u.Id == userId && u.IsAdmin, ct);
}
```

Task 9 also touches this file (adds `ListAsync`/`CountAsync`/`UpdateAsync`) — this task lands first, Task 9 builds on top, per the declared dependency order.

### Step 2: Create `MembershipGuard`

Create `src/HydraForge.Application/Auth/MembershipGuard.cs` — one place that encodes "admin or member", called identically from all 10 services instead of repeating the `isAdmin` check inline 57 times:

```csharp
using HydraForge.Application.Projects;

namespace HydraForge.Application.Auth;

public static class MembershipGuard
{
    public static async Task<bool> HasAccessAsync(
        IUserRepository userRepo,
        IProjectMemberRepository memberRepo,
        Guid projectId,
        Guid userId,
        CancellationToken ct = default)
    {
        if (await userRepo.IsAdminAsync(userId, ct))
            return true;

        var membership = await memberRepo.GetByProjectAndUserAsync(projectId, userId, ct);
        return membership != null;
    }
}
```

### Step 3: Write `MembershipGuard` tests

Create `tests/HydraForge.Application.Tests/Auth/MembershipGuardTests.cs`:

```csharp
using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Domain.Entities.ProjectSpace;
using Xunit;

namespace HydraForge.Application.Tests.Auth;

public class MembershipGuardTests
{
    private class FakeUserRepo(bool isAdmin) : IUserRepository
    {
        public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(isAdmin);
        // ... stub remaining IUserRepository members to throw NotImplementedException
    }

    private class FakeMemberRepo(bool hasMembership) : IProjectMemberRepository
    {
        public Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(hasMembership ? new ProjectMember() : null);
        // ... stub remaining IProjectMemberRepository members
    }

    [Fact]
    public async Task HasAccessAsync_Admin_ReturnsTrueRegardlessOfMembership()
    {
        var result = await MembershipGuard.HasAccessAsync(
            new FakeUserRepo(isAdmin: true), new FakeMemberRepo(hasMembership: false), Guid.NewGuid(), Guid.NewGuid());
        Assert.True(result);
    }

    [Fact]
    public async Task HasAccessAsync_NonAdminMember_ReturnsTrue()
    {
        var result = await MembershipGuard.HasAccessAsync(
            new FakeUserRepo(isAdmin: false), new FakeMemberRepo(hasMembership: true), Guid.NewGuid(), Guid.NewGuid());
        Assert.True(result);
    }

    [Fact]
    public async Task HasAccessAsync_NonAdminNonMember_ReturnsFalse()
    {
        var result = await MembershipGuard.HasAccessAsync(
            new FakeUserRepo(isAdmin: false), new FakeMemberRepo(hasMembership: false), Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result);
    }
}
```

### Step 4: Update the 10 services — pattern

For each of the 57 call sites, the change is the same shape. Before (example from `CardService.cs`):

```csharp
var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
if (membership == null)
    return Result<CardDto>.Failure(new Error(DomainErrorCodes.Projects.NotAMember, "Not a project member."));
```

After:

```csharp
if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, cmd.ProjectId, cmd.ActorId, ct))
    return Result<CardDto>.Failure(new Error(DomainErrorCodes.Projects.NotAMember, "Not a project member."));
```

Every touched service needs `IUserRepository` added to its constructor if it doesn't already have one (check each file — `ProjectService` already gets it from Task 7's trigger-point work; `CardService`, `CommentService` likely already have it for actor-name lookups added in Task 7; `ChecklistService`, `PlanService`, `SpecService`, `ColumnService`, `AttachmentService`, `CardRelationshipService`, `ProjectMemberService` almost certainly don't yet — add it).

Work through the 10 files in this order (smallest first, to validate the pattern before the two big ones):

1. `CommentService.cs` (1 site)
2. `ProjectService.cs` (3 sites)
3. `AttachmentService.cs` (4 sites)
4. `CardRelationshipService.cs` (4 sites)
5. `ProjectMemberService.cs` (4 sites)
6. `ColumnService.cs` (6 sites)
7. `SpecService.cs` (7 sites)
8. `ChecklistService.cs` (8 sites)
9. `PlanService.cs` (8 sites)
10. `CardService.cs` (12 sites, largest — do last once the pattern is proven)

For each file, add `using HydraForge.Application.Auth;` and replace every `GetByProjectAndUserAsync` membership-null-check with the `MembershipGuard.HasAccessAsync` call above. Do not change the failure `Error` code/message at each site — only the condition that guards it.

### Step 5: Write per-service bypass tests

For each of the 10 services, add (or extend an existing test file with) one test confirming an admin actor bypasses a membership check that would otherwise fail — e.g. for `CardService`:

```csharp
[Fact]
public async Task MoveAsync_AdminNonMember_Succeeds()
{
    // Arrange: fake IUserRepository.IsAdminAsync returns true for the actor,
    // fake IProjectMemberRepository.GetByProjectAndUserAsync returns null for the same actor/project
    // Act: call CardService.MoveAsync with that actor as cmd.ActorId
    // Assert: Result.IsSuccess — the admin bypass let the call through despite no membership
}
```

Repeat this shape once per service (10 tests total) rather than only testing the shared `MembershipGuard` in isolation — the guard being correct doesn't prove every one of the 57 call sites was actually wired to use it instead of the old inline check.

### Step 6: Add `ListAllAsync` to `IProjectRepository`

In `src/HydraForge.Application/Projects/IProjectRepository.cs`, add:

```csharp
Task<PagedResult<Project>> ListAllAsync(
    bool includeArchived,
    string? search,
    ProjectSortField sortBy,
    bool sortDescending,
    int skip,
    int take,
    CancellationToken ct = default);
```

Implement in the EF repository (mirrors the existing `ListByUserIdAsync`/`ListForUserAsync`, minus the membership join/filter).

### Step 7: Update `ProjectService.GetAllAsync` for admin

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
    bool isAdmin = false,
    CancellationToken ct = default)
{
    var page = isAdmin
        ? await projectRepo.ListAllAsync(includeArchived, search, sortBy, sortDescending, skip, take, ct)
        : await projectRepo.ListByUserIdAsync(requestUserId, includeArchived, search, sortBy, sortDescending, role, skip, take, ct);
    // ... rest same as today
}
```

### Step 8: Update `ProjectsController.List`

```csharp
[HttpGet]
public async Task<IActionResult> List(...)
{
    var userId = User.GetRequiredUserId();
    var isAdmin = User.IsInRole(Roles.Admin);

    var result = await projectService.GetAllAsync(
        userId, includeArchived, search, sortBy, sortDescending, role, skip, take, isAdmin);
    // ...
}
```

### Step 9: `IsProjectMemberOrAdmin` for the one real controller call site

Create `src/HydraForge.Server/Auth/ClaimsPrincipalExtensions.cs`:

```csharp
using System.Security.Claims;
using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Domain.Constants;

namespace HydraForge.Server.Auth;

public static class ClaimsPrincipalExtensions
{
    public static async Task<bool> IsProjectMemberOrAdmin(
        this ClaimsPrincipal user,
        IProjectMemberRepository memberRepo,
        Guid projectId)
    {
        if (user.IsInRole(Roles.Admin))
            return true;

        var userId = user.GetRequiredUserId();
        var membership = await memberRepo.GetByProjectAndUserAsync(projectId, userId);
        return membership != null;
    }
}
```

Update `src/HydraForge.Server/Controllers/Projects/ProjectSnapshotController.cs` — its existing:

```csharp
var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, userId);
if (membership == null)
    return Forbid();
```

becomes:

```csharp
if (!await User.IsProjectMemberOrAdmin(_memberRepo, projectId))
    return Forbid();
```

Write `tests/HydraForge.Server.Tests/Auth/ClaimsPrincipalExtensionsTests.cs` (admin-true / member-true / neither-false — 3 tests), same shape as before.

### Step 10: Verify

```bash
dotnet build
dotnet test tests/HydraForge.Application.Tests/ --filter "FullyQualifiedName~MembershipGuardTests"
dotnet test tests/HydraForge.Server.Tests/ --filter "FullyQualifiedName~ClaimsPrincipalExtensionsTests"
dotnet test
```

## Verification

- `dotnet build` — no errors
- `dotnet test` — all tests pass, including the 10 new per-service admin-bypass tests and 3 `MembershipGuard`/`ClaimsPrincipalExtensions` tests
- Manual: login as admin, access a card/column/spec/plan/checklist/attachment/comment/relationship/project-member action on a project you're not a member of → succeeds for all 10 resource types, not just cards
- Manual: login as non-admin non-member, same actions → 403/`Result.Failure` on every one
- Manual: `BoardHub`/`PresenceHub` — join a project as admin non-member → succeeds (no code change needed there, just confirms Task 1's fix is sufficient)

## Dependencies

- Task 1 (JWT role claim fix — `IsInRole(Roles.Admin)` must work, and the `IsAdminAsync` DB check needs `User.IsAdmin` to be set correctly, which it already is independent of the JWT claim)
