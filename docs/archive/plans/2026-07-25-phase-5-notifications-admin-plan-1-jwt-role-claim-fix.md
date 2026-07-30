# Plan 1: JWT Role Claim Fix

**Branch:** `task/jwt-role-claim-fix`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 1

## Task

Fix JWT token issuance to emit `ClaimTypes.Role` with value `"Admin"` when `user.IsAdmin` is true. Define shared `Roles.Admin` constant. This unblocks all `IsInRole("Admin")` checks in `PresenceHub`, `BoardHub`, and new admin controllers.

## Files to create

- `src/HydraForge.Domain/Constants/Roles.cs` — shared `Roles.Admin` constant

## Files to modify

- `src/HydraForge.Infrastructure/Auth/JwtTokenIssuer.cs` — add `ClaimTypes.Role` emission
- `src/HydraForge.Server/Auth/AuthPolicies.cs` — add `AdminRequired` policy constant
- `src/HydraForge.Server/Program.cs` — register `AdminRequired` authorization policy
- `src/HydraForge.Server/Hubs/PresenceHub.cs:31` — replace `"Admin"` string with `Roles.Admin`
- `src/HydraForge.Infrastructure/Realtime/BoardHub.cs:30` — replace `"Admin"` string with `Roles.Admin`

## Implementation steps

### Step 1: Create `Roles` constants class

Create `src/HydraForge.Domain/Constants/Roles.cs`:

```csharp
namespace HydraForge.Domain.Constants;

public static class Roles
{
    public const string Admin = "Admin";
}
```

### Step 2: Add `ClaimTypes.Role` emission in `JwtTokenIssuer`

In `src/HydraForge.Infrastructure/Auth/JwtTokenIssuer.cs`, add `using System.Security.Claims;` (already present) and `using HydraForge.Domain.Constants;`. Modify `IssueToken`:

```csharp
public AccessToken IssueToken(User user)
{
    var expiresAt = DateTimeOffset.UtcNow.AddMinutes(accessTokenMinutes);
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new(JwtRegisteredClaimNames.Name, user.Username),
        new("is_admin", user.IsAdmin.ToString().ToLower()),
    };

    if (user.IsAdmin)
    {
        claims.Add(new Claim(ClaimTypes.Role, Roles.Admin));
    }

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: expiresAt.UtcDateTime,
        signingCredentials: credentials
    );

    return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
}
```

### Step 3: Add `AdminRequired` policy constant

In `src/HydraForge.Server/Auth/AuthPolicies.cs`:

```csharp
namespace HydraForge.Server.Auth;

public static class AuthPolicies
{
    public const string UserIdRequired = nameof(UserIdRequired);
    public const string AdminRequired = nameof(AdminRequired);
}
```

### Step 4: Register `AdminRequired` policy in `Program.cs`

In `src/HydraForge.Server/Program.cs`, add `using HydraForge.Domain.Constants;` and add the policy inside the existing `AddAuthorization` block:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AuthPolicies.UserIdRequired,
        policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => context.User.TryGetUserId(out _));
        }
    );
    options.AddPolicy(
        AuthPolicies.AdminRequired,
        policy => policy.RequireRole(Roles.Admin)
    );
});
```

### Step 5: Replace magic strings in hubs

In `src/HydraForge.Server/Hubs/PresenceHub.cs:31`, add `using HydraForge.Domain.Constants;` and change:

```csharp
var isAdmin = Context.User!.IsInRole(Roles.Admin);
```

In `src/HydraForge.Infrastructure/Realtime/BoardHub.cs:30`, add `using HydraForge.Domain.Constants;` and change:

```csharp
var isAdmin = Context.User.IsInRole(Roles.Admin);
```

### Step 6: Write tests

Create `tests/HydraForge.Infrastructure.Tests/Auth/JwtTokenIssuerTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HydraForge.Domain.Constants;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Infrastructure.Auth;
using Xunit;

namespace HydraForge.Infrastructure.Tests.Auth;

public class JwtTokenIssuerTests
{
    private static readonly JwtTokenIssuer Issuer = new(
        "HydraForge", "HydraForge", "super-secret-key-that-is-at-least-32-bytes-long!!", 60);

    [Fact]
    public void IssueToken_AdminUser_IncludesRoleClaim()
    {
        var user = new User { Id = Guid.NewGuid(), Username = "admin", IsAdmin = true };

        var token = Issuer.IssueToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.Value);
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal(Roles.Admin, roleClaim.Value);
    }

    [Fact]
    public void IssueToken_NonAdminUser_NoRoleClaim()
    {
        var user = new User { Id = Guid.NewGuid(), Username = "user", IsAdmin = false };

        var token = Issuer.IssueToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.Value);
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.Null(roleClaim);
    }

    [Fact]
    public void IssueToken_AdminUser_StillEmitsIsAdminClaim()
    {
        var user = new User { Id = Guid.NewGuid(), Username = "admin", IsAdmin = true };

        var token = Issuer.IssueToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.Value);
        var isAdminClaim = jwt.Claims.FirstOrDefault(c => c.Type == "is_admin");
        Assert.NotNull(isAdminClaim);
        Assert.Equal("true", isAdminClaim.Value);
    }
}
```

### Step 7: Run tests

```bash
dotnet test tests/HydraForge.Infrastructure.Tests/ --filter "FullyQualifiedName~JwtTokenIssuerTests"
```

Expected: all 3 tests pass.

### Step 8: Verify full build

```bash
dotnet build
```

Expected: no errors.

## Verification

- `dotnet build` — no errors
- `dotnet test tests/HydraForge.Infrastructure.Tests/ --filter "FullyQualifiedName~JwtTokenIssuerTests"` — 3 tests pass
- `dotnet test` — all existing tests still pass
- Manual: login as admin, inspect JWT payload — `role` claim present with value `"Admin"`

## Dependencies

None — this is the first task and unblocks all others.