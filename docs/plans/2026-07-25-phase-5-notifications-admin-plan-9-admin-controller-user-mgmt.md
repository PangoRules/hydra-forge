# Plan 9: Admin Controller + User Management

**Branch:** `task/admin-controller-user-mgmt`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 9

## Task

Add `User.Disable()`/`Enable()`/`SetAdminRole()`/`SetPasswordHash()` instance methods. Create `AdminController` with user CRUD endpoints. Create `AdminService`. Create Web UI admin users page.

## Files to create

- `src/HydraForge.Application/Admin/IAdminService.cs`
- `src/HydraForge.Application/Admin/AdminService.cs`
- `src/HydraForge.Server/Controllers/Admin/AdminController.cs`
- `src/web-ui/app/pages/admin/index.vue`
- `src/web-ui/app/pages/admin/users.vue`
- `src/web-ui/app/composables/useAdmin.ts`

## Files to modify

- `src/HydraForge.Domain/Entities/Auth/User.cs` — add instance methods
- `src/HydraForge.Server/Program.cs` — register `AdminService`
- `src/web-ui/app/lib/routes.ts` — add `ApiRoutes.Admin.*` and `UiRoutes.Admin.*`
- `src/web-ui/app/layouts/default.vue` — add admin nav link

## Implementation steps

### Step 1: Add User instance methods

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
}
```

### Step 2: Write Domain tests

Create `tests/HydraForge.Domain.Tests/Entities/UserTests.cs`:

```csharp
using HydraForge.Domain.Entities.Auth;
using Xunit;

namespace HydraForge.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void Disable_SetsIsDisabledTrue()
    {
        var user = new User();
        user.Disable();
        Assert.True(user.IsDisabled);
    }

    [Fact]
    public void Enable_SetsIsDisabledFalse()
    {
        var user = new User();
        user.Disable();
        user.Enable();
        Assert.False(user.IsDisabled);
    }

    [Fact]
    public void SetAdminRole_TogglesIsAdmin()
    {
        var user = new User();
        user.SetAdminRole(true);
        Assert.True(user.IsAdmin);
        user.SetAdminRole(false);
        Assert.False(user.IsAdmin);
    }

    [Fact]
    public void SetPasswordHash_UpdatesHash()
    {
        var user = new User();
        user.SetPasswordHash("new-hash");
        Assert.Equal("new-hash", user.PasswordHash);
    }
}
```

Run: `dotnet test tests/HydraForge.Domain.Tests/ --filter "FullyQualifiedName~UserTests"` — 4 tests pass.

### Step 3: Create AdminService

Create `src/HydraForge.Application/Admin/IAdminService.cs`:

```csharp
using HydraForge.Domain.Common;

namespace HydraForge.Application.Admin;

public interface IAdminService
{
    Task<Result<IReadOnlyList<UserDto>>> ListUsersAsync(int skip, int take, string? search, CancellationToken ct = default);
    Task<Result<UserDto>> GetUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result> DisableUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result> EnableUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default);
    Task<Result> ToggleAdminRoleAsync(Guid actorId, Guid targetUserId, CancellationToken ct = default);
}

public record CreateUserRequest(string Username, string Password, string Name, string Email, bool IsAdmin);
public record UserDto(Guid Id, string Username, string Name, string Email, bool IsAdmin, bool IsDisabled, DateTime? LastLoginAt, DateTime CreatedAt);
```

Create `src/HydraForge.Application/Admin/AdminService.cs`:

```csharp
using HydraForge.Application.Auth;
using HydraForge.Domain.Common;

namespace HydraForge.Application.Admin;

public class AdminService(
    IUserRepository userRepo,
    IPasswordHasher passwordHasher
) : IAdminService
{
    public async Task<Result<IReadOnlyList<UserDto>>> ListUsersAsync(int skip, int take, string? search, CancellationToken ct = default)
    {
        var users = await userRepo.ListAsync(skip, take, search, ct);
        return Result<IReadOnlyList<UserDto>>.Success(
            users.Select(MapToDto).ToList());
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
        var existing = await userRepo.FindByUsernameAsync(request.Username, ct);
        if (existing != null)
            return Result<UserDto>.Failure(new Error("USERNAME_TAKEN", "Username already exists."));

        var user = new Domain.Entities.Auth.User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            UsernameNormalized = request.Username.ToUpperInvariant(),
            Name = request.Name,
            Email = request.Email,
            EmailNormalized = request.Email.ToUpperInvariant(),
            PasswordHash = passwordHasher.Hash(request.Password),
            IsAdmin = request.IsAdmin,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await userRepo.AddAsync(user, ct);
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
        user.SetPasswordHash(passwordHasher.Hash(newPassword));
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

    private static UserDto MapToDto(Domain.Entities.Auth.User u) => new(
        u.Id, u.Username, u.Name, u.Email, u.IsAdmin, u.IsDisabled, u.LastLoginAt, u.CreatedAt);
}
```

### Step 4: Create AdminController

Create `src/HydraForge.Server/Controllers/Admin/AdminController.cs`:

```csharp
using HydraForge.Application.Admin;
using HydraForge.Application.Auth;
using HydraForge.Server.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers.Admin;

[Authorize(Policy = AuthPolicies.AdminRequired)]
[ApiController]
[Route("api/admin")]
public class AdminController(IAdminService adminService) : ControllerBase
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
}

public record ResetPasswordRequest(string NewPassword);
```

### Step 5: Register AdminService in DI

In `src/HydraForge.Server/Program.cs`, add:

```csharp
builder.Services.AddScoped<IAdminService, AdminService>();
```

### Step 6: Add API routes

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
    projectsList: () => '/api/admin/projects',
    projectGet: (projectId: string) => `/api/admin/projects/${projectId}`,
    settingsGet: () => '/api/admin/settings',
    settingsUpdate: () => '/api/admin/settings',
    auditLog: () => '/api/admin/audit-log',
  },
}
```

### Step 7: Create admin users page

Create `src/web-ui/app/pages/admin/users.vue`:

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
const loading = ref(false)
const search = ref('')
const page = ref(0)
const pageSize = 20

async function loadUsers() {
  loading.value = true
  try {
    const data = await api.GET<any[]>(ApiRoutes.Admin.usersList(page.value * pageSize, pageSize, search.value || undefined))
    users.value = data
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
const newUser = reactive({ username: '', password: '', name: '', email: '', isAdmin: false })

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
  </div>

  <!-- Create User Modal -->
  <UModal v-model:open="showCreateModal">
    <template #body>
      <div class="p-4 space-y-3">
        <h2 class="text-lg font-semibold">Create User</h2>
        <UInput v-model="newUser.username" label="Username" />
        <UInput v-model="newUser.password" label="Password" type="password" />
        <UInput v-model="newUser.name" label="Name" />
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

### Step 8: Add admin nav link to layout

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

### Step 9: Verify

```bash
dotnet build
dotnet test tests/HydraForge.Domain.Tests/ --filter "FullyQualifiedName~UserTests"
dotnet test
```

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
```

## Verification

- `dotnet build` — no errors
- Domain tests: `Disable`, `Enable`, `SetAdminRole`, `SetPasswordHash` all work
- `dotnet test` — all tests pass
- `pnpm typecheck && pnpm lint && pnpm build` — no errors
- Manual: login as admin, navigate to `/admin/users`, see user list, create/disable/enable/toggle admin

## Dependencies

- Task 1 (JWT role claim fix — `[Authorize(Policy = AuthPolicies.AdminRequired)]` must work)
- Task 8 (Admin all-projects bypass — admin can see all projects)