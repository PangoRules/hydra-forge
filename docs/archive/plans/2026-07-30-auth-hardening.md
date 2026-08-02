# Auth Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add rate limiting, fix PresenceHub auth bypass, add brute-force login protection, and harden AuthController against future regression.

**Architecture:** ASP.NET Core `AddRateLimiter()` with fixed-window policy. Login handler tracks failed attempts on User entity with lockout. PresenceHub gets membership check matching JoinProject pattern.

**Tech Stack:** ASP.NET Core 10, EF Core 10, SignalR, xUnit

---

### Task 1: Rate Limiting

**Files:**
- Modify: `src/HydraForge.Server/Program.cs`
- Modify: `src/HydraForge.Server/Controllers/Auth/AuthController.cs`
- Create: `src/HydraForge.Server/Conventions/RateLimitConvention.cs`
- Modify: `src/HydraForge.Server/Hubs/PresenceHub.cs`
- Modify: `src/HydraForge.Infrastructure/Realtime/BoardHub.cs`
- Modify: `src/HydraForge.Infrastructure/Realtime/NotificationHub.cs`

- [ ] **Step 1: Add rate limiter services in Program.cs**

After `builder.Services.AddCors(...)` (line 56), add:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Strict login limit: 5 attempts per minute per IP
    options.AddFixedWindowLimiter("Login", config =>
    {
        config.PermitLimit = 5;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });

    // Global limit: 300 requests per minute per IP
    options.AddFixedWindowLimiter("Global", config =>
    {
        config.PermitLimit = 300;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 10;
    });

    // SignalR hubs: 60 messages per minute per connection
    options.AddFixedWindowLimiter("SignalR", config =>
    {
        config.PermitLimit = 60;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 5;
    });
});
```

- [ ] **Step 2: Wire rate limiter middleware in Program.cs**

After `app.UseCors()` (line 218), add:

```csharp
app.UseRateLimiter();
```

- [ ] **Step 3: Apply login rate limit on AuthController.Login**

Add `[EnableRateLimiting("Login")]` and `[AllowAnonymous]` to the `Login` method:

```csharp
[HttpPost("login")]
[AllowAnonymous]
[EnableRateLimiting("Login")]
[ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
```

Add `using Microsoft.AspNetCore.RateLimiting;` at top.

- [ ] **Step 4: Apply global rate limit via controller convention**

In `Program.cs`, after `builder.Services.AddControllers()...` (line 62-69), add:

```csharp
builder.Services.AddControllers(options =>
{
    options.Conventions.Add(new RateLimitConvention("Global"));
})
```

Create `src/HydraForge.Server/Conventions/RateLimitConvention.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HydraForge.Server.Conventions;

public class RateLimitConvention(string policyName) : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        // Skip controllers that already have explicit rate limiting
        if (controller.Attributes.OfType<EnableRateLimitingAttribute>().Any())
            return;

        // Skip health endpoint
        if (controller.ControllerType == typeof(Controllers.Health.HealthController))
            return;

        controller.Filters.Add(new EnableRateLimitingAttribute(policyName));
    }
}
```

- [ ] **Step 5: Apply SignalR rate limit on hubs**

Add `[EnableRateLimiting("SignalR")]` to each hub class:

`PresenceHub.cs` — add after `[Authorize]`:
```csharp
[Authorize]
[EnableRateLimiting("SignalR")]
public class PresenceHub(IProjectMemberRepository memberRepo) : Hub
```

`BoardHub.cs` — find the class declaration and add the same attribute.

`NotificationHub.cs` — find the class declaration and add the same attribute.

- [ ] **Step 6: Build and verify**

Run: `dotnet build`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add src/HydraForge.Server/Program.cs src/HydraForge.Server/Controllers/Auth/AuthController.cs src/HydraForge.Server/Conventions/RateLimitConvention.cs src/HydraForge.Server/Hubs/PresenceHub.cs src/HydraForge.Infrastructure/Realtime/BoardHub.cs src/HydraForge.Infrastructure/Realtime/NotificationHub.cs
git commit -m "feat: add rate limiting (login 5/min, global 300/min, SignalR 60/min)"
```

---

### Task 2: PresenceHub FocusCard/UnfocusCard Membership Check

**Files:**
- Modify: `src/HydraForge.Server/Hubs/PresenceHub.cs`

- [ ] **Step 1: Add membership check to FocusCard**

Replace `FocusCard` method with:

```csharp
public async Task FocusCard(Guid projectId, Guid cardId)
{
    var userId = Context.User!.GetRequiredUserId();

    var isAdmin = Context.User!.IsInRole(Roles.Admin);
    if (!isAdmin)
    {
        var membership =
            await _memberRepo.GetByProjectAndUserAsync(projectId, userId)
            ?? throw new HubException("Access denied");
    }

    var groupName = BoardHub.ProjectGroup(projectId);
    await Clients
        .OthersInGroup(groupName)
        .SendAsync(
            "CardFocused",
            new
            {
                UserId = userId,
                CardId = cardId,
                Context.ConnectionId,
            }
        );
}
```

- [ ] **Step 2: Add membership check to UnfocusCard**

Replace `UnfocusCard` method with:

```csharp
public async Task UnfocusCard(Guid projectId)
{
    var userId = Context.User!.GetRequiredUserId();

    var isAdmin = Context.User!.IsInRole(Roles.Admin);
    if (!isAdmin)
    {
        var membership =
            await _memberRepo.GetByProjectAndUserAsync(projectId, userId)
            ?? throw new HubException("Access denied");
    }

    var groupName = BoardHub.ProjectGroup(projectId);
    await Clients
        .OthersInGroup(groupName)
        .SendAsync("CardUnfocused", new { UserId = userId, Context.ConnectionId });
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add src/HydraForge.Server/Hubs/PresenceHub.cs
git commit -m "fix: add membership check to PresenceHub FocusCard/UnfocusCard"
```

---

### Task 3: Login Brute-Force Protection

**Files:**
- Modify: `src/HydraForge.Domain/Entities/Auth/User.cs`
- Modify: `src/HydraForge.Application/Auth/LoginUserHandler.cs`
- Modify: `src/HydraForge.Domain/Common/DomainErrorCodes.cs`

- [ ] **Step 1: Add lockout fields to User entity**

In `User.cs`, add properties after `LastLoginAt`:

```csharp
public int FailedLoginAttempts { get; private set; }
public DateTime? LockedOutUntil { get; private set; }
```

Add methods:

```csharp
public void RecordFailedLogin()
{
    FailedLoginAttempts++;
    UpdatedAt = DateTime.UtcNow;
}

public void Lockout(TimeSpan duration)
{
    LockedOutUntil = DateTime.UtcNow.Add(duration);
    UpdatedAt = DateTime.UtcNow;
}

public void ResetFailedAttempts()
{
    FailedLoginAttempts = 0;
    LockedOutUntil = null;
    UpdatedAt = DateTime.UtcNow;
}
```

- [ ] **Step 2: Add lockout error code**

In `DomainErrorCodes.cs`, inside `Auth` class, add:

```csharp
public const string AccountLocked = "AUTH_ACCOUNT_LOCKED";
```

- [ ] **Step 3: Add lockout logic to LoginUserHandler**

Inject `IAuditLogWriter`:

```csharp
public class LoginUserHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer,
    IAuditLogWriter auditLogWriter
)
```

Add lockout check and tracking in `HandleAsync`:

```csharp
// Check lockout
if (user.LockedOutUntil.HasValue && user.LockedOutUntil.Value > DateTime.UtcNow)
{
    var remaining = user.LockedOutUntil.Value - DateTime.UtcNow;
    return Result<LoginResponse>.Failure(
        new Error(DomainErrorCodes.Auth.AccountLocked,
            $"Account locked. Try again in {remaining.Minutes + 1} minute(s).")
    );
}

if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
{
    user.RecordFailedLogin();

    // Lockout after 5 consecutive failures for 15 minutes
    if (user.FailedLoginAttempts >= 5)
    {
        user.Lockout(TimeSpan.FromMinutes(15));
    }

    await userRepository.UpdateAsync(user);

    // Audit log the failed attempt
    await auditLogWriter.WriteAsync(new AuditLogRequest(
        ActorId: user.Id,
        Scope: AuditLogScope.System,
        EntityType: "User",
        EntityId: user.Id,
        Action: "LoginFailed",
        NewValueJson: $"{{\"failedAttempts\": {user.FailedLoginAttempts}}}"
    ));

    return Result<LoginResponse>.Failure(
        new Error(DomainErrorCodes.Auth.InvalidCredentials, "Invalid credentials.")
    );
}

// Successful login — reset failed attempts
user.ResetFailedAttempts();
await userRepository.UpdateAsync(user);
```

- [ ] **Step 4: Add EF migration for new User fields**

Run:
```bash
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations add AddUserLockoutFields --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```

- [ ] **Step 5: Build and run tests**

Run: `dotnet build && dotnet test`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/HydraForge.Domain/Entities/Auth/User.cs src/HydraForge.Domain/Common/DomainErrorCodes.cs src/HydraForge.Application/Auth/LoginUserHandler.cs src/HydraForge.Infrastructure/Migrations/
git commit -m "feat: add brute-force login protection with account lockout after 5 failures"
```

---

### Task 4: AuthController [AllowAnonymous]

**Files:**
- Modify: `src/HydraForge.Server/Controllers/Auth/AuthController.cs`

- [ ] **Step 1: Add [AllowAnonymous] to Login method**

The attribute is already added in Task 1, Step 3. Verify it's there:

```csharp
[HttpPost("login")]
[AllowAnonymous]
[EnableRateLimiting("Login")]
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add src/HydraForge.Server/Controllers/Auth/AuthController.cs
git commit -m "fix: add explicit [AllowAnonymous] on AuthController.Login"
```

---

### Verification

Run full verification per `dotnet-verification` skill:
1. `dotnet build` — must pass
2. `dotnet test` — must pass
3. `PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server` — must report no pending changes
