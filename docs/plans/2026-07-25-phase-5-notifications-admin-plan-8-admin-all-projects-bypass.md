# Plan 8: Admin All-Projects Bypass

**Branch:** `task/admin-all-projects-bypass`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 8

## Task

Create `IsProjectMemberOrAdmin` extension method using `Roles.Admin` from Task 1. Replace all `Forbid()` membership-check patterns in controllers. Update `ProjectsController.List` to show all projects for admins. `BoardHub`/`PresenceHub` already gate on `IsInRole` — no code change needed beyond Task 1.

## Files to create

- `src/HydraForge.Server/Auth/ClaimsPrincipalExtensions.cs` — `IsProjectMemberOrAdmin` extension method

## Files to modify

- `src/HydraForge.Server/Controllers/Projects/ProjectsController.cs` — update `List` to show all projects for admin, add bypass to all endpoints
- `src/HydraForge.Server/Controllers/Projects/CardsController.cs` — replace membership checks
- `src/HydraForge.Server/Controllers/Projects/ColumnsController.cs` — replace membership checks
- `src/HydraForge.Server/Controllers/Projects/CardCommentsController.cs` — replace membership checks
- `src/HydraForge.Server/Controllers/Projects/CardAttachmentsController.cs` — replace membership checks
- `src/HydraForge.Server/Controllers/Projects/CardRelationshipsController.cs` — replace membership checks
- `src/HydraForge.Server/Controllers/Projects/SpecsController.cs` — replace membership checks
- `src/HydraForge.Server/Controllers/Projects/PlansController.cs` — replace membership checks
- `src/HydraForge.Server/Controllers/Projects/ProjectSnapshotController.cs` — replace membership checks
- `src/HydraForge.Server/Controllers/Projects/CardChecklistController.cs` — replace membership checks
- `src/HydraForge.Application/Projects/IProjectRepository.cs` — add `ListAllAsync` method
- `src/HydraForge.Application/Projects/ProjectService.cs` — add admin bypass to `GetAllAsync`

## Implementation steps

### Step 1: Create `IsProjectMemberOrAdmin` extension method

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

### Step 2: Update controllers — pattern

For each controller, the membership check pattern changes from:

```csharp
var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, userId);
if (membership == null)
    return Forbid();
```

To:

```csharp
if (!await User.IsProjectMemberOrAdmin(_memberRepo, projectId))
    return Forbid();
```

Controllers to update (each file needs `using HydraForge.Server.Auth;` added):

1. `CardsController.cs` — all endpoints call `_memberRepo.GetByProjectAndUserAsync` indirectly through `CardService`. The service layer does its own membership check. Controllers that call services don't need the bypass — the service handles it. **However**, `CardsController` doesn't inject `IProjectMemberRepository` directly. The membership check is in `CardService`. So `CardService` needs the bypass, not the controller.

**Correction**: The membership checks are in the Application services (`CardService`, `CommentService`, etc.), not in the controllers. The controllers just pass `userId` to the services. So the bypass must be added to the services, not the controllers.

### Step 2 (revised): Add bypass to Application services

Each service that does `_memberRepo.GetByProjectAndUserAsync(projectId, actorId)` needs to also check `IsInRole`. But Application layer shouldn't depend on `ClaimsPrincipal`. Instead, add an `IUserRoleService` or pass `isAdmin` flag.

**Simpler approach**: Add `bool isAdmin` parameter to service methods, or create an `IUserAccessor` that provides `IsAdmin`. 

**Even simpler**: The services already receive `actorId` (Guid). Add `IUserRepository` method `IsAdminAsync(Guid userId)` and check in each service.

Let's add to `IUserRepository`:

```csharp
Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default);
```

Then in each service, replace:

```csharp
var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
if (membership == null)
    return Result.Failure(...);
```

With:

```csharp
var isAdmin = await _userRepo.IsAdminAsync(actorId, ct);
if (!isAdmin)
{
    var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
    if (membership == null)
        return Result.Failure(...);
}
```

### Step 2a: Add `IsAdminAsync` to IUserRepository

In `src/HydraForge.Application/Auth/IUserRepository.cs` (find actual path), add:

```csharp
Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default);
```

### Step 2b: Implement in EfUserRepository

Find `EfUserRepository` and add:

```csharp
public async Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default)
{
    return await db.Users.AnyAsync(u => u.Id == userId && u.IsAdmin, ct);
}
```

### Step 2c: Update services

Update these service files to add admin bypass before membership check:

- `src/HydraForge.Application/Cards/CardService.cs` — all methods that check membership
- `src/HydraForge.Application/Comments/CommentService.cs` — `ValidateMembershipAndCardAsync`
- `src/HydraForge.Application/Cards/CardRelationshipService.cs` — all methods
- `src/HydraForge.Application/Projects/ProjectService.cs` — `GetByIdAsync`, `UpdateAsync`, `ToggleArchiveAsync`

### Step 3: Update ProjectsController.List for admin

In `src/HydraForge.Server/Controllers/Projects/ProjectsController.cs`, update `List` to check admin:

```csharp
[HttpGet]
public async Task<IActionResult> List(...)
{
    var userId = User.GetRequiredUserId();
    var isAdmin = User.IsInRole(Roles.Admin);

    var result = await projectService.GetAllAsync(
        userId,
        includeArchived,
        search,
        sortBy,
        sortDescending,
        role,
        skip,
        take,
        isAdmin
    );
    // ...
}
```

Update `ProjectService.GetAllAsync` to accept `bool isAdmin` parameter and call `ListAllAsync` when admin:

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
    // ... rest same
}
```

### Step 4: Add `ListAllAsync` to IProjectRepository

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

Implement in `EfProjectRepository`.

### Step 5: Write tests

Create `tests/HydraForge.Server.Tests/Auth/ClaimsPrincipalExtensionsTests.cs`:

```csharp
using System.Security.Claims;
using HydraForge.Application.Projects;
using HydraForge.Domain.Constants;
using HydraForge.Server.Auth;
using Xunit;

namespace HydraForge.Server.Tests.Auth;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public async Task IsProjectMemberOrAdmin_AdminRole_ReturnsTrue()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, Roles.Admin),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        ]));
        var memberRepo = new FakeMemberRepo(hasMembership: false);

        var result = await user.IsProjectMemberOrAdmin(memberRepo, Guid.NewGuid());

        Assert.True(result);
    }

    [Fact]
    public async Task IsProjectMemberOrAdmin_NonAdminWithMembership_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        ]));
        var memberRepo = new FakeMemberRepo(hasMembership: true);

        var result = await user.IsProjectMemberOrAdmin(memberRepo, Guid.NewGuid());

        Assert.True(result);
    }

    [Fact]
    public async Task IsProjectMemberOrAdmin_NonAdminNoMembership_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        ]));
        var memberRepo = new FakeMemberRepo(hasMembership: false);

        var result = await user.IsProjectMemberOrAdmin(memberRepo, Guid.NewGuid());

        Assert.False(result);
    }

    private class FakeMemberRepo(bool hasMembership) : IProjectMemberRepository
    {
        public Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        {
            return Task.FromResult(hasMembership ? new ProjectMember() : null);
        }
        // ... stub remaining methods
    }
}
```

### Step 6: Verify

```bash
dotnet build
dotnet test tests/HydraForge.Server.Tests/ --filter "FullyQualifiedName~ClaimsPrincipalExtensionsTests"
dotnet test
```

## Verification

- `dotnet build` — no errors
- `dotnet test` — all tests pass
- Manual: login as admin, access a project you're not a member of → should succeed
- Manual: login as non-admin non-member, access a project → should get 403

## Dependencies

- Task 1 (JWT role claim fix — `IsInRole(Roles.Admin)` must work)