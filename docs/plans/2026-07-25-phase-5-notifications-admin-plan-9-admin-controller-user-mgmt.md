# Plan 9: Admin Controller + User Management

**Branch:** `task/admin-controller-user-mgmt`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 9

## Task

Add `User.Create()` factory + `Disable()`/`Enable()`/`SetAdminRole()`/`SetPasswordHash()`/`RecordLogin()` instance methods. Extend `IUserRepository` with the paging/write methods `AdminService` needs. Create `AdminController` with user CRUD endpoints + admin project listing (reusing the existing `IProjectService`). Create `AdminService`. Create Web UI admin users + projects pages.

**Revision note:** this plan originally made `User` private-ctor/private-setter but left `AdminService.CreateUserAsync` constructing it via object initializer — that doesn't compile. Investigation also found `EfUserRepository.cs`, `AdminSeeder.cs`, and `TestUserSeeder.cs` all construct/mutate `User` directly today — all three break under the same refactor and are now in scope. `IUserRepository` (defined in `LoginUserHandler.cs`, not its own file) also has none of `ListAsync`/`CountAsync`/`UpdateAsync` that `AdminService` needs — added below. The original spec's `/api/admin/projects` endpoint + `/admin/projects` page were never assigned to any task — folded into this one since it's the lightest lift (reuses `IProjectService.GetAllAsync` from Task 8, which already returns a paged, admin-aware result).

## Files to create

- `src/HydraForge.Application/Admin/IAdminService.cs`
- `src/HydraForge.Application/Admin/AdminService.cs`
- `src/HydraForge.Server/Controllers/Admin/AdminController.cs`
- `src/web-ui/app/pages/admin/index.vue`
- `src/web-ui/app/pages/admin/users.vue`
- `src/web-ui/app/pages/admin/projects.vue`
- `src/web-ui/app/composables/useAdmin.ts`
- `tests/HydraForge.Domain.Tests/Entities/UserTests.cs`
- `tests/HydraForge.Infrastructure.Tests/Auth/EfUserRepositoryTests.cs`

## Files to modify

- `src/HydraForge.Domain/Entities/Auth/User.cs` — add `Create()` factory + instance methods
- `src/HydraForge.Application/Auth/LoginUserHandler.cs` — extend `IUserRepository` (`ListAsync`, `CountAsync`, `UpdateAsync`; `CreateAsync` gains an optional `CancellationToken`); `IsAdminAsync` already added here by Task 8 — do not re-add
- `src/HydraForge.Infrastructure/Auth/EfUserRepository.cs` — implement new methods; stop mutating `User` properties directly (`CreateAsync`/`UpdateLastLoginAsync`)
- `src/HydraForge.Infrastructure/Auth/AdminSeeder.cs` — use `User.Create()` instead of object initializer
- `src/HydraForge.Infrastructure/Auth/TestUserSeeder.cs` — use `User.Create()` instead of object initializer
- `src/HydraForge.Server/Program.cs` — register `AdminService`
- `src/web-ui/app/lib/routes.ts` — add `ApiRoutes.Admin.*` and `UiRoutes.Admin.*`
- `src/web-ui/app/layouts/default.vue` — add admin nav link

## Implementation steps

### Step 1: Add User factory + instance methods

In `src/HydraForge.Domain/Entities/Auth/User.cs`, replace the property-bag class:

```csharp
namespace HydraForge.Domain.Entities.Auth;

public class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string UsernameNormalized { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string EmailNormalized { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsAdmin { get; private set; }
    public bool IsDisabled { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private User() { }

    public static User Create(
        string username,
        string name,
        string lastName,
        string email,
        string passwordHash,
        bool isAdmin = false)
    {
        var now = DateTime.UtcNow;
        return new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            LastName = lastName,
            Username = username,
            UsernameNormalized = username.ToLowerInvariant(),
            Email = email,
            EmailNormalized = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            IsAdmin = isAdmin,
            IsDisabled = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Disable()
    {
        IsDisabled = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Enable()
    {
        IsDisabled = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAdminRole(bool isAdmin)
    {
        IsAdmin = isAdmin;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPasswordHash(string hash)
    {
        PasswordHash = hash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLogin(DateTime loginAt)
    {
        LastLoginAt = loginAt;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

`RecordLogin` replaces the direct `user.LastLoginAt = loginAt; user.UpdatedAt = ...` currently done in `EfUserRepository.UpdateLastLoginAsync` (Step 3) — that code stops compiling the moment setters go private, so it moves here.

### Step 2: Write Domain tests

Create `tests/HydraForge.Domain.Tests/Entities/UserTests.cs`:

```csharp
using HydraForge.Domain.Entities.Auth;
using Xunit;

namespace HydraForge.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void Create_SetsNormalizedFieldsAndDefaults()
    {
        var user = User.Create("Alice", "Alice", "Smith", "Alice@Example.com", "hash", isAdmin: true);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("alice", user.UsernameNormalized);
        Assert.Equal("alice@example.com", user.EmailNormalized);
        Assert.True(user.IsAdmin);
        Assert.False(user.IsDisabled);
        Assert.Null(user.LastLoginAt);
    }

    [Fact]
    public void Disable_SetsIsDisabledTrue()
    {
        var user = User.Create("bob", "Bob", "Jones", "bob@example.com", "hash");
        user.Disable();
        Assert.True(user.IsDisabled);
    }

    [Fact]
    public void Enable_SetsIsDisabledFalse()
    {
        var user = User.Create("bob", "Bob", "Jones", "bob@example.com", "hash");
        user.Disable();
        user.Enable();
        Assert.False(user.IsDisabled);
    }

    [Fact]
    public void SetAdminRole_TogglesIsAdmin()
    {
        var user = User.Create("bob", "Bob", "Jones", "bob@example.com", "hash");
        user.SetAdminRole(true);
        Assert.True(user.IsAdmin);
        user.SetAdminRole(false);
        Assert.False(user.IsAdmin);
    }

    [Fact]
    public void SetPasswordHash_UpdatesHash()
    {
        var user = User.Create("bob", "Bob", "Jones", "bob@example.com", "hash");
        user.SetPasswordHash("new-hash");
        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Fact]
    public void RecordLogin_SetsLastLoginAt()
    {
        var user = User.Create("bob", "Bob", "Jones", "bob@example.com", "hash");
        var loginAt = DateTime.UtcNow;
        user.RecordLogin(loginAt);
        Assert.Equal(loginAt, user.LastLoginAt);
    }
}
```

Run: `dotnet test tests/HydraForge.Domain.Tests/ --filter "FullyQualifiedName~UserTests"` — 6 tests pass.

### Step 3: Extend `IUserRepository` and fix existing callers

In `src/HydraForge.Application/Auth/LoginUserHandler.cs`, extend the interface (`IsAdminAsync` was already added here by Task 8 — don't duplicate it):

```csharp
public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
    Task<User?> FindByUsernameAsync(string username);
    Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(IReadOnlyList<string> usernames, string? searchTerm = null, int maxResults = 10, CancellationToken ct = default);
    Task UpdateLastLoginAsync(Guid userId, DateTime loginAt);
    Task<bool> AnyAdminExistsAsync();
    Task CreateAsync(User user, CancellationToken ct = default);
    Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default); // added by Task 8
    Task<IReadOnlyList<User>> ListAsync(int skip, int take, string? search, CancellationToken ct = default);
    Task<int> CountAsync(string? search, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}
```

`CreateAsync` gained an optional `CancellationToken` — existing call sites (`AdminSeeder`, `TestUserSeeder`) that call `CreateAsync(user)` with no token still compile unchanged.

In `src/HydraForge.Infrastructure/Auth/EfUserRepository.cs`, replace the whole file — `CreateAsync` no longer sets normalized fields itself (the `User.Create()` factory already guarantees them), and `UpdateLastLoginAsync` calls the new `RecordLogin()` instance method instead of assigning properties:

```csharp
using HydraForge.Application.Auth;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Auth;

public class EfUserRepository(HydraForgeDbContext context) : IUserRepository
{
    public async Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        return await context.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
    }

    public async Task<User?> FindByUsernameAsync(string username)
    {
        var normalized = username.ToLowerInvariant();
        return await context.Users.FirstOrDefaultAsync(u => u.UsernameNormalized == normalized);
    }

    public async Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(IReadOnlyList<string> usernames, string? searchTerm = null, int maxResults = 10, CancellationToken ct = default)
    {
        IQueryable<User> query = context.Users;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalized = searchTerm.ToLowerInvariant();
            query = query.Where(u => EF.Functions.ILike(u.Username, $"%{normalized}%"));
        }
        else if (usernames.Count > 0)
        {
            var normalized = usernames.Select(u => u.ToLowerInvariant()).ToList();
            query = query.Where(u => normalized.Contains(u.UsernameNormalized));
        }

        var users = await query.Take(maxResults).ToListAsync(ct);
        return users.ToDictionary(u => u.Username, u => u, StringComparer.OrdinalIgnoreCase);
    }

    public async Task UpdateLastLoginAsync(Guid userId, DateTime loginAt)
    {
        var user = await context.Users.FindAsync(userId);
        if (user == null)
            return;
        user.RecordLogin(loginAt);
        await context.SaveChangesAsync();
    }

    public async Task<bool> AnyAdminExistsAsync()
    {
        return await context.Users.AnyAsync(u => u.IsAdmin);
    }

    public async Task CreateAsync(User user, CancellationToken ct = default)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default)
    {
        // implemented by Task 8 — keep as-is if already present, shown here for completeness
        return await context.Users.AnyAsync(u => u.Id == userId && u.IsAdmin, ct);
    }

    public async Task<IReadOnlyList<User>> ListAsync(int skip, int take, string? search, CancellationToken ct = default)
    {
        IQueryable<User> query = context.Users;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.ToLowerInvariant();
            query = query.Where(u =>
                EF.Functions.ILike(u.Username, $"%{normalized}%") ||
                EF.Functions.ILike(u.Email, $"%{normalized}%"));
        }

        return await query
            .OrderBy(u => u.Username)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(string? search, CancellationToken ct = default)
    {
        IQueryable<User> query = context.Users;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.ToLowerInvariant();
            query = query.Where(u =>
                EF.Functions.ILike(u.Username, $"%{normalized}%") ||
                EF.Functions.ILike(u.Email, $"%{normalized}%"));
        }

        return await query.CountAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync(ct);
    }
}
```

In `src/HydraForge.Infrastructure/Auth/AdminSeeder.cs`, replace the object initializer:

```csharp
var user = User.Create(
    username: username,
    name: _options.Name ?? "Admin",
    lastName: _options.LastName ?? "Admin",
    email: _options.Email ?? "admin@localhost",
    passwordHash: passwordHasher.HashPassword(password),
    isAdmin: true
);

await userRepository.CreateAsync(user);
```

In `src/HydraForge.Infrastructure/Auth/TestUserSeeder.cs`, replace the object initializer:

```csharp
var user = User.Create(
    username: username,
    name: $"Test{username}",
    lastName: "User",
    email: $"{username}@localhost",
    passwordHash: passwordHasher.HashPassword(password),
    isAdmin: isAdmin
);

await userRepository.CreateAsync(user);
```

### Step 4: Write EfUserRepository tests

Create `tests/HydraForge.Infrastructure.Tests/Auth/EfUserRepositoryTests.cs` (uses EF in-memory provider, same pattern as `EfAuditLogReaderTests` in Task 11):

```csharp
using HydraForge.Domain.Entities.Auth;
using HydraForge.Infrastructure.Auth;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HydraForge.Infrastructure.Tests.Auth;

public class EfUserRepositoryTests
{
    private static HydraForgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HydraForgeDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_PersistsUserAsIs_NoAdditionalMutation()
    {
        using var db = CreateContext();
        var repo = new EfUserRepository(db);
        var user = User.Create("alice", "Alice", "Smith", "alice@example.com", "hash");

        await repo.CreateAsync(user);

        var found = await repo.FindByUsernameAsync("alice");
        Assert.NotNull(found);
        Assert.Equal("alice", found!.UsernameNormalized);
    }

    [Fact]
    public async Task ListAsync_FiltersBySearch()
    {
        using var db = CreateContext();
        var repo = new EfUserRepository(db);
        await repo.CreateAsync(User.Create("alice", "Alice", "Smith", "alice@example.com", "hash"));
        await repo.CreateAsync(User.Create("bob", "Bob", "Jones", "bob@example.com", "hash"));

        var result = await repo.ListAsync(0, 20, "ali");

        Assert.Single(result);
        Assert.Equal("alice", result[0].Username);
    }

    [Fact]
    public async Task CountAsync_MatchesListAsyncFilter()
    {
        using var db = CreateContext();
        var repo = new EfUserRepository(db);
        await repo.CreateAsync(User.Create("alice", "Alice", "Smith", "alice@example.com", "hash"));
        await repo.CreateAsync(User.Create("bob", "Bob", "Jones", "bob@example.com", "hash"));

        Assert.Equal(2, await repo.CountAsync(null));
        Assert.Equal(1, await repo.CountAsync("bob"));
    }

    [Fact]
    public async Task UpdateAsync_PersistsInstanceMethodChanges()
    {
        using var db = CreateContext();
        var repo = new EfUserRepository(db);
        var user = User.Create("alice", "Alice", "Smith", "alice@example.com", "hash");
        await repo.CreateAsync(user);

        user.Disable();
        await repo.UpdateAsync(user);

        var found = await repo.FindByIdAsync(user.Id);
        Assert.True(found!.IsDisabled);
    }
}
```

Run: `dotnet test tests/HydraForge.Infrastructure.Tests/ --filter "FullyQualifiedName~EfUserRepositoryTests"` — 4 tests pass.

### Step 5: Create AdminService

Create `src/HydraForge.Application/Admin/IAdminService.cs`:

```csharp
using HydraForge.Domain.Common;

namespace HydraForge.Application.Admin;

public interface IAdminService
{
    Task<Result<UserListPageDto>> ListUsersAsync(int skip, int take, string? search, CancellationToken ct = default);
    Task<Result<UserDto>> GetUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result> DisableUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result> EnableUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default);
    Task<Result> ToggleAdminRoleAsync(Guid actorId, Guid targetUserId, CancellationToken ct = default);
}

public record CreateUserRequest(string Username, string Password, string Name, string LastName, string Email, bool IsAdmin);
public record UserDto(Guid Id, string Username, string Name, string Email, bool IsAdmin, bool IsDisabled, DateTime? LastLoginAt, DateTime CreatedAt);
public record UserListPageDto(IReadOnlyList<UserDto> Items, int TotalCount);
```

Create `src/HydraForge.Application/Admin/AdminService.cs`:

```csharp
using HydraForge.Application.Auth;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;

namespace HydraForge.Application.Admin;

public class AdminService(
    IUserRepository userRepo,
    IPasswordHasher passwordHasher
) : IAdminService
{
    public async Task<Result<UserListPageDto>> ListUsersAsync(int skip, int take, string? search, CancellationToken ct = default)
    {
        var users = await userRepo.ListAsync(skip, take, search, ct);
        var totalCount = await userRepo.CountAsync(search, ct);
        return Result<UserListPageDto>.Success(
            new UserListPageDto(users.Select(MapToDto).ToList(), totalCount));
    }

    public async Task<Result<UserDto>> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepo.FindByIdAsync(userId, ct);
        if (user == null)
            return Result<UserDto>.Failure(new Error("USER_NOT_FOUND", "User not found."));
        return Result<UserDto>.Success(MapToDto(user));
    }

    public async Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var existing = await userRepo.FindByUsernameAsync(request.Username);
        if (existing != null)
            return Result<UserDto>.Failure(new Error("USERNAME_TAKEN", "Username already exists."));

        var user = User.Create(
            request.Username, request.Name, request.LastName, request.Email,
            passwordHasher.HashPassword(request.Password), request.IsAdmin);

        await userRepo.CreateAsync(user, ct);
        return Result<UserDto>.Success(MapToDto(user));
    }

    public async Task<Result> DisableUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepo.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure(new Error("USER_NOT_FOUND", "User not found."));
        user.Disable();
        await userRepo.UpdateAsync(user, ct);
        return Result.Success();
    }

    public async Task<Result> EnableUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepo.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure(new Error("USER_NOT_FOUND", "User not found."));
        user.Enable();
        await userRepo.UpdateAsync(user, ct);
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default)
    {
        var user = await userRepo.FindByIdAsync(userId, ct);
        if (user == null)
            return Result.Failure(new Error("USER_NOT_FOUND", "User not found."));
        user.SetPasswordHash(passwordHasher.HashPassword(newPassword));
        await userRepo.UpdateAsync(user, ct);
        return Result.Success();
    }

    public async Task<Result> ToggleAdminRoleAsync(Guid actorId, Guid targetUserId, CancellationToken ct = default)
    {
        if (actorId == targetUserId)
            return Result.Failure(new Error("ADMIN_SELF_DEMOTION", "Cannot remove your own admin role."));

        var user = await userRepo.FindByIdAsync(targetUserId, ct);
        if (user == null)
            return Result.Failure(new Error("USER_NOT_FOUND", "User not found."));
        user.SetAdminRole(!user.IsAdmin);
        await userRepo.UpdateAsync(user, ct);
        return Result.Success();
    }

    private static UserDto MapToDto(User u) => new(
        u.Id, u.Username, u.Name, u.Email, u.IsAdmin, u.IsDisabled, u.LastLoginAt, u.CreatedAt);
}
```

Confirmed against the actual interface in `LoginUserHandler.cs`: it's `IPasswordHasher.HashPassword(string)`, matching `AdminSeeder`'s existing usage — used consistently above.

### Step 6: Create AdminController (users + projects)

Create `src/HydraForge.Server/Controllers/Admin/AdminController.cs`. This also adds the `/api/admin/projects` endpoints the original spec documented (§7.1) but that no task ever implemented — it reuses `IProjectService.GetAllAsync`/`GetByIdAsync` from Task 8, which are already admin-aware (`isAdmin: true` bypasses membership filtering and returns the existing paged `ProjectListPageDto`/`ProjectListPageResponse`, which already carries `TotalCount` — no new project DTOs needed):

```csharp
using HydraForge.Application.Admin;
using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Server.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Admin;

[Authorize(Policy = AuthPolicies.AdminRequired)]
[ApiController]
[Route("api/admin")]
public class AdminController(
    IAdminService adminService,
    IProjectService projectService
) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await adminService.ListUsersAsync(skip, take, search, ct);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<IActionResult> GetUser(Guid userId, CancellationToken ct)
    {
        var result = await adminService.GetUserAsync(userId, ct);
        return result.IsFailure ? NotFound(result.Error) : Ok(result.Value);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await adminService.CreateUserAsync(request, ct);
        return result.IsFailure ? BadRequest(result.Error) : CreatedAtAction(nameof(GetUser), new { userId = result.Value.Id }, result.Value);
    }

    [HttpPatch("users/{userId:guid}/disable")]
    public async Task<IActionResult> DisableUser(Guid userId, CancellationToken ct)
    {
        var result = await adminService.DisableUserAsync(userId, ct);
        return result.IsFailure ? NotFound(result.Error) : NoContent();
    }

    [HttpPatch("users/{userId:guid}/enable")]
    public async Task<IActionResult> EnableUser(Guid userId, CancellationToken ct)
    {
        var result = await adminService.EnableUserAsync(userId, ct);
        return result.IsFailure ? NotFound(result.Error) : NoContent();
    }

    [HttpPost("users/{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid userId, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await adminService.ResetPasswordAsync(userId, request.NewPassword, ct);
        return result.IsFailure ? NotFound(result.Error) : NoContent();
    }

    [HttpPatch("users/{userId:guid}/role")]
    public async Task<IActionResult> ToggleRole(Guid userId, CancellationToken ct)
    {
        var actorId = User.GetRequiredUserId();
        var result = await adminService.ToggleAdminRoleAsync(actorId, userId, ct);
        return result.IsFailure ? BadRequest(result.Error) : NoContent();
    }

    [HttpGet("projects")]
    public async Task<IActionResult> ListProjects(
        [FromQuery] bool includeArchived = true,
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var actorId = User.GetRequiredUserId();
        var result = await projectService.GetAllAsync(
            actorId, includeArchived, search, ProjectSortField.Name, false, null, skip, take, isAdmin: true, ct);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpGet("projects/{projectId:guid}")]
    public async Task<IActionResult> GetProject(Guid projectId, CancellationToken ct)
    {
        var actorId = User.GetRequiredUserId();
        var result = await projectService.GetByIdAsync(projectId, actorId, ct);
        return result.IsFailure ? NotFound(result.Error) : Ok(result.Value);
    }
}

public record ResetPasswordRequest(string NewPassword);
```

Verify the exact signature of `IProjectService.GetAllAsync`/`GetByIdAsync` once Task 8 lands (parameter names/order for sort field, role filter, etc.) — adjust the call above to match rather than guessing further.

### Step 7: Register AdminService in DI

In `src/HydraForge.Server/Program.cs`, add:

```csharp
builder.Services.AddScoped<IAdminService, AdminService>();
```

### Step 8: Add API/UI routes

In `src/web-ui/app/lib/routes.ts`, add:

```typescript
UiRoutes: {
  // ... existing
  Admin: {
    Home: '/admin',
    Users: '/admin/users',
    Projects: '/admin/projects',
    Settings: '/admin/settings',
    AuditLog: '/admin/audit-log',
  }
},

ApiRoutes: {
  // ... existing
  Admin: {
    usersList: (skip = 0, take = 20, search?: string) =>
      `/api/admin/users?skip=${skip}&take=${take}${search ? `&search=${encodeURIComponent(search)}` : ''}`,
    userGet: (userId: string) => `/api/admin/users/${userId}`,
    userCreate: () => '/api/admin/users',
    userDisable: (userId: string) => `/api/admin/users/${userId}/disable`,
    userEnable: (userId: string) => `/api/admin/users/${userId}/enable`,
    userResetPassword: (userId: string) => `/api/admin/users/${userId}/reset-password`,
    userRole: (userId: string) => `/api/admin/users/${userId}/role`,
    projectsList: (skip = 0, take = 20, search?: string) =>
      `/api/admin/projects?skip=${skip}&take=${take}${search ? `&search=${encodeURIComponent(search)}` : ''}`,
    projectGet: (projectId: string) => `/api/admin/projects/${projectId}`,
    settingsGet: () => '/api/admin/settings',
    settingsUpdate: () => '/api/admin/settings',
    auditLog: () => '/api/admin/audit-log',
  },
}
```

### Step 9: Create admin users page

Create `src/web-ui/app/pages/admin/users.vue`. Note this consumes the paged shape `{ items, totalCount }` from Step 5/6, not a bare array:

```vue
<script setup lang="ts">
definePageMeta({ middleware: ['auth'] })

const { user } = useAuth()
if (!user.value?.isAdmin) {
  navigateTo('/projects')
}

const api = useApi()
const toast = useToast()

const users = ref<any[]>([])
const totalCount = ref(0)
const loading = ref(false)
const search = ref('')
const page = ref(0)
const pageSize = 20

async function loadUsers() {
  loading.value = true
  try {
    const data = await api.GET<{ items: any[], totalCount: number }>(
      ApiRoutes.Admin.usersList(page.value * pageSize, pageSize, search.value || undefined))
    users.value = data.items
    totalCount.value = data.totalCount
  } catch (e: any) {
    toast.add({ title: e.message || 'Failed to load users', color: 'error' })
  } finally {
    loading.value = false
  }
}

async function toggleDisable(userId: string, currentlyDisabled: boolean) {
  try {
    if (currentlyDisabled) {
      await api.PATCH(ApiRoutes.Admin.userEnable(userId))
    } else {
      await api.PATCH(ApiRoutes.Admin.userDisable(userId))
    }
    await loadUsers()
    toast.add({ title: `User ${currentlyDisabled ? 'enabled' : 'disabled'}`, color: 'success' })
  } catch (e: any) {
    toast.add({ title: e.message || 'Action failed', color: 'error' })
  }
}

async function toggleAdmin(userId: string) {
  try {
    await api.PATCH(ApiRoutes.Admin.userRole(userId))
    await loadUsers()
    toast.add({ title: 'Admin role toggled', color: 'success' })
  } catch (e: any) {
    toast.add({ title: e.message || 'Action failed', color: 'error' })
  }
}

const showCreateModal = ref(false)
const newUser = reactive({ username: '', password: '', name: '', lastName: '', email: '', isAdmin: false })

async function createUser() {
  try {
    await api.POST(ApiRoutes.Admin.userCreate(), { body: { ...newUser } })
    showCreateModal.value = false
    await loadUsers()
    toast.add({ title: 'User created', color: 'success' })
  } catch (e: any) {
    toast.add({ title: e.message || 'Create failed', color: 'error' })
  }
}

onMounted(() => loadUsers())
</script>

<template>
  <div class="p-6">
    <div class="flex items-center justify-between mb-4">
      <h1 class="text-2xl font-bold">Users</h1>
      <UButton label="Create User" @click="showCreateModal = true" />
    </div>

    <UInput v-model="search" placeholder="Search users..." @update:model-value="loadUsers" class="mb-4" />

    <UTable :rows="users" :loading="loading">
      <template #disabled-data="{ row }">
        <UBadge :color="row.isDisabled ? 'red' : 'green'">
          {{ row.isDisabled ? 'Disabled' : 'Active' }}
        </UBadge>
      </template>
      <template #isAdmin-data="{ row }">
        <UBadge v-if="row.isAdmin" color="blue">Admin</UBadge>
        <span v-else class="text-gray-400">—</span>
      </template>
      <template #actions-data="{ row }">
        <div class="flex gap-1">
          <UButton size="xs" color="neutral" @click="toggleDisable(row.id, row.isDisabled)">
            {{ row.isDisabled ? 'Enable' : 'Disable' }}
          </UButton>
          <UButton size="xs" color="neutral" @click="toggleAdmin(row.id)">
            {{ row.isAdmin ? 'Remove Admin' : 'Make Admin' }}
          </UButton>
        </div>
      </template>
    </UTable>

    <p class="text-sm text-gray-500 mt-3">{{ totalCount }} total users</p>
  </div>

  <!-- Create User Modal -->
  <UModal v-model:open="showCreateModal">
    <template #body>
      <div class="p-4 space-y-3">
        <h2 class="text-lg font-semibold">Create User</h2>
        <UInput v-model="newUser.username" label="Username" />
        <UInput v-model="newUser.password" label="Password" type="password" />
        <UInput v-model="newUser.name" label="First Name" />
        <UInput v-model="newUser.lastName" label="Last Name" />
        <UInput v-model="newUser.email" label="Email" />
        <UCheckbox v-model="newUser.isAdmin" label="Admin" />
      </div>
    </template>
    <template #footer>
      <div class="flex justify-end gap-2 p-4">
        <UButton label="Cancel" color="neutral" @click="showCreateModal = false" />
        <UButton label="Create" @click="createUser" />
      </div>
    </template>
  </UModal>
</template>
```

### Step 10: Create admin projects page

Create `src/web-ui/app/pages/admin/projects.vue` — read-only table, no admin-specific mutation actions (project archive/edit already happens through the existing project pages, now reachable for any project because of the Task 8 bypass):

```vue
<script setup lang="ts">
definePageMeta({ middleware: ['auth'] })

const { user } = useAuth()
if (!user.value?.isAdmin) {
  navigateTo('/projects')
}

const api = useApi()
const toast = useToast()

const projects = ref<any[]>([])
const totalCount = ref(0)
const loading = ref(false)
const search = ref('')
const page = ref(0)
const pageSize = 20

async function loadProjects() {
  loading.value = true
  try {
    const data = await api.GET<{ items: any[], totalCount: number }>(
      ApiRoutes.Admin.projectsList(page.value * pageSize, pageSize, search.value || undefined))
    projects.value = data.items
    totalCount.value = data.totalCount
  } catch (e: any) {
    toast.add({ title: e.message || 'Failed to load projects', color: 'error' })
  } finally {
    loading.value = false
  }
}

onMounted(() => loadProjects())
</script>

<template>
  <div class="p-6">
    <h1 class="text-2xl font-bold mb-4">All Projects</h1>

    <UInput v-model="search" placeholder="Search projects..." @update:model-value="loadProjects" class="mb-4" />

    <UTable :rows="projects" :loading="loading">
      <template #archived-data="{ row }">
        <UBadge :color="row.archivedAt ? 'red' : 'green'">
          {{ row.archivedAt ? 'Archived' : 'Active' }}
        </UBadge>
      </template>
      <template #actions-data="{ row }">
        <UButton size="xs" color="neutral" :to="`/projects/${row.id}/board`">Open Board</UButton>
      </template>
    </UTable>

    <p class="text-sm text-gray-500 mt-3">{{ totalCount }} total projects</p>
  </div>
</template>
```

### Step 11: Add admin nav link to layout

In `src/web-ui/app/layouts/default.vue`, add admin nav in `#left` slot:

```vue
<template #left>
  <NuxtLink to="/projects" class="flex items-center gap-2">
    <span class="text-lg font-bold">HydraForge</span>
  </NuxtLink>
  <ClientOnly>
    <UButton
      v-if="user?.isAdmin"
      label="Admin"
      color="neutral"
      variant="ghost"
      to="/admin"
    />
  </ClientOnly>
</template>
```

### Step 12: Verify

```bash
dotnet build
dotnet test tests/HydraForge.Domain.Tests/ --filter "FullyQualifiedName~UserTests"
dotnet test tests/HydraForge.Infrastructure.Tests/ --filter "FullyQualifiedName~EfUserRepositoryTests"
dotnet test
```

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
```

## Verification

- `dotnet build` — no errors (including `AdminSeeder`, `TestUserSeeder`, `EfUserRepository` — all updated for the private-setter `User`)
- Domain tests: `Create`, `Disable`, `Enable`, `SetAdminRole`, `SetPasswordHash`, `RecordLogin` all work
- Infrastructure tests: `ListAsync` search filter, `CountAsync` matches, `UpdateAsync` persists instance-method changes
- `dotnet test` — all tests pass, including existing auth/login tests (unaffected by the `IUserRepository` additions since they're purely additive)
- `pnpm typecheck && pnpm lint && pnpm build` — no errors
- Manual: login as admin, navigate to `/admin/users`, see paginated user list with total count, create/disable/enable/toggle admin
- Manual: navigate to `/admin/projects`, see all projects including ones you're not a member of

## Dependencies

- Task 1 (JWT role claim fix — `[Authorize(Policy = AuthPolicies.AdminRequired)]` must work)
- Task 8 (Admin all-projects bypass — `IsAdminAsync` already added to `IUserRepository`; `IProjectService.GetAllAsync`/`GetByIdAsync` already admin-aware)
