# Pagination & Dependencies Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cap unbounded pagination on all list endpoints, add global request body size limit, update vulnerable pnpm transitive dependencies, and bump outdated NuGet packages.

**Architecture:** `Math.Min(take, MaxPageSize)` applied in every controller action before passing to service/repository. Global `MaxRequestBodySize` in Kestrel config. `pnpm update` for JS deps, manual version bumps for NuGet.

**Tech Stack:** ASP.NET Core 10, EF Core 10, pnpm, NuGet

---

### Task 1: Unbounded Pagination — Max Page Size Constants

**Files:**
- Create: `src/HydraForge.Domain/Constants/PaginationConstants.cs`
- Modify: `src/HydraForge.Server/Controllers/Projects/ProjectsController.cs`
- Modify: `src/HydraForge.Server/Controllers/Projects/CardsController.cs`
- Modify: `src/HydraForge.Server/Controllers/Admin/AdminController.cs`
- Modify: `src/HydraForge.Server/Controllers/NotificationsController.cs`
- Modify: `src/HydraForge.Server/Controllers/UsersController.cs`

- [ ] **Step 1: Create pagination constants**

Create `src/HydraForge.Domain/Constants/PaginationConstants.cs`:

```csharp
namespace HydraForge.Domain.Constants;

public static class PaginationConstants
{
    /// <summary>Max page size for user-facing list endpoints.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Max page size for admin list endpoints.</summary>
    public const int MaxAdminPageSize = 500;
}
```

- [ ] **Step 2: Cap ProjectsController.List**

In `ProjectsController.cs`, add after `take = 20` parameter:

```csharp
take = Math.Min(take, PaginationConstants.MaxPageSize);
```

Add `using HydraForge.Domain.Constants;`.

- [ ] **Step 3: Cap CardsController.List**

In `CardsController.cs`, the `archivedLimit` parameter is passed directly to the filter. Add cap in the controller before creating the filter:

```csharp
archivedLimit = archivedLimit.HasValue
    ? Math.Min(archivedLimit.Value, PaginationConstants.MaxPageSize)
    : null;
```

Add `using HydraForge.Domain.Constants;`.

- [ ] **Step 4: Cap AdminController.ListUsers**

In `AdminController.cs`, add after `take = 20`:

```csharp
take = Math.Min(take, PaginationConstants.MaxAdminPageSize);
```

Add `using HydraForge.Domain.Constants;`.

- [ ] **Step 5: Cap AdminController.ListProjects**

In `AdminController.cs`, add after `take = 20`:

```csharp
take = Math.Min(take, PaginationConstants.MaxAdminPageSize);
```

- [ ] **Step 6: Cap NotificationsController.List**

In `NotificationsController.cs`, add after `take = 20`:

```csharp
take = Math.Min(take, PaginationConstants.MaxPageSize);
```

Add `using HydraForge.Domain.Constants;`.

- [ ] **Step 7: Cap UsersController.Search**

In `UsersController.cs`, add after `limit = 10`:

```csharp
limit = Math.Min(limit, PaginationConstants.MaxPageSize);
```

Add `using HydraForge.Domain.Constants;`.

- [ ] **Step 8: Build and verify**

Run: `dotnet build`
Expected: PASS

- [ ] **Step 9: Commit**

```bash
git add src/HydraForge.Domain/Constants/PaginationConstants.cs src/HydraForge.Server/Controllers/Projects/ProjectsController.cs src/HydraForge.Server/Controllers/Projects/CardsController.cs src/HydraForge.Server/Controllers/Admin/AdminController.cs src/HydraForge.Server/Controllers/NotificationsController.cs src/HydraForge.Server/Controllers/UsersController.cs
git commit -m "fix: cap unbounded pagination (100 user-facing, 500 admin)"
```

---

### Task 2: Global Request Body Size Limit

**Files:**
- Modify: `src/HydraForge.Server/Program.cs`

- [ ] **Step 1: Add global MaxRequestBodySize**

Before `var app = builder.Build()`, add:

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    // Global request body size limit: 1 MB for most endpoints
    // Spec/plan content endpoints have their own validation (MarkdownPayloadTooLarge)
    options.Limits.MaxRequestBodySize = 1_048_576; // 1 MB
});
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add src/HydraForge.Server/Program.cs
git commit -m "fix: set global MaxRequestBodySize to 1 MB"
```

---

### Task 3: pnpm Vulnerability Fixes

**Files:**
- Modify: `src/web-ui/pnpm-lock.yaml` (via `pnpm update`)

- [ ] **Step 1: Update transitive dependencies**

Run:
```bash
cd src/web-ui && pnpm update
```

This bumps all dependencies to their latest compatible versions, pulling in fixed versions of:
- `tar` → >=7.5.21 (fixes GHSA-23hp-3jrh-7fpw, GHSA-r292-9mhp-454m, GHSA-w8wr-v893-vjvp, GHSA-8x88-c5mf-7j5w, GHSA-gvwx-54wh-qm9j)
- `brace-expansion` → >=5.0.8 (fixes GHSA-3jxr-9vmj-r5cp, GHSA-mh99-v99m-4gvg)
- `js-yaml` → >=4.3.0 (fixes GHSA-52cp-r559-cp3m)
- `shell-quote` → >=1.8.5 (fixes GHSA-395f-4hp3-45gv)
- `svgo` → >=4.0.2 (fixes GHSA-2p49-hgcm-8545)
- `fast-uri` → >=3.1.4 (fixes GHSA-v2hh-gcrm-f6hx)
- `postcss` → >=8.5.18 (fixes GHSA-r28c-9q8g-f849)
- `@babel/core` → >=7.29.1 (fixes GHSA-4x5r-pxfx-6jf8)

- [ ] **Step 2: Verify web UI still builds**

Run:
```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
```
Expected: All three pass.

- [ ] **Step 3: Commit**

```bash
git add src/web-ui/package.json src/web-ui/pnpm-lock.yaml
git commit -m "chore(deps): bump pnpm transitive deps to fix 8 HIGH/CRITICAL vulns"
```

---

### Task 4: NuGet Package Updates

**Files:**
- Modify: `src/HydraForge.Application/HydraForge.Application.csproj`
- Modify: `src/HydraForge.Infrastructure/HydraForge.Infrastructure.csproj`
- Modify: `src/HydraForge.Server/HydraForge.Server.csproj`
- Modify: `tests/HydraForge.Server.Tests/HydraForge.Server.Tests.csproj`
- Modify: `tests/HydraForge.Infrastructure.Tests/HydraForge.Infrastructure.Tests.csproj`
- Modify: `tests/HydraForge.Application.Tests/HydraForge.Application.Tests.csproj`
- Modify: `tests/HydraForge.Domain.Tests/HydraForge.Domain.Tests.csproj`

- [ ] **Step 1: Update System.IdentityModel.Tokens.Jwt**

In `HydraForge.Application.csproj` and `HydraForge.Infrastructure.csproj`, change:
```xml
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.3.1" />
```
to:
```xml
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.*" />
```

- [ ] **Step 2: Update AWSSDK.S3**

In `HydraForge.Infrastructure.csproj`, change:
```xml
<PackageReference Include="AWSSDK.S3" Version="3.7.402.0" />
```
to:
```xml
<PackageReference Include="AWSSDK.S3" Version="3.7.*" />
```

- [ ] **Step 3: Update EF Core packages**

In `HydraForge.Infrastructure.csproj`, change:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.8" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.8" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
```
to:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.*" />
```

In `tests/HydraForge.Infrastructure.Tests.csproj`, change:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.8" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.8" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
```
to:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.*" />
```

- [ ] **Step 4: Update test packages**

In all test `.csproj` files, change:
```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
```
to:
```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.*" />
```

In `tests/HydraForge.Server.Tests.csproj`, also change:
```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.6" />
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.6" />
```
to:
```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.*" />
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.*" />
```

- [ ] **Step 5: Restore and build**

Run:
```bash
dotnet restore && dotnet build
```
Expected: PASS

- [ ] **Step 6: Run all tests**

Run:
```bash
dotnet test
```
Expected: All tests pass.

- [ ] **Step 7: Verify no pending EF model changes**

Run:
```bash
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```
Expected: No pending changes.

- [ ] **Step 8: Commit**

```bash
git add src/HydraForge.Application/HydraForge.Application.csproj src/HydraForge.Infrastructure/HydraForge.Infrastructure.csproj src/HydraForge.Server/HydraForge.Server.csproj tests/HydraForge.Server.Tests/HydraForge.Server.Tests.csproj tests/HydraForge.Infrastructure.Tests/HydraForge.Infrastructure.Tests.csproj tests/HydraForge.Application.Tests/HydraForge.Application.Tests.csproj tests/HydraForge.Domain.Tests/HydraForge.Domain.Tests.csproj
git commit -m "chore(deps): bump NuGet packages to latest minor/patch versions"
```

---

### Verification

Run full verification per `dotnet-verification` skill:
1. `dotnet build` — must pass
2. `dotnet test` — must pass
3. `PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server` — must report no pending changes

Run web UI verification per `nuxt-verification` skill:
1. `cd src/web-ui && pnpm typecheck` — must pass
2. `cd src/web-ui && pnpm lint` — must pass
3. `cd src/web-ui && pnpm build` — must pass
