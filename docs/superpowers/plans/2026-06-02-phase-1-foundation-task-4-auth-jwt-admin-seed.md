# Task 4: Auth, password hashing, JWT, and admin seed
**Branch:** `task/phase-1-auth`
**Parent branch:** `feat/phase-1-foundation`
**Parent spec:** `2026-06-02-phase-1-foundation-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:test-driven-development, then superpowers:executing-plans.

**Goal:** Add local username/password authentication, Argon2id password hashing, JWT issuance, disabled-user rejection, and first-run admin seed.

**Architecture:** Application defines auth use case and abstractions. Infrastructure implements password hashing, user persistence, and admin seed persistence. Server exposes HTTP endpoint and JWT validation.

**Tech Stack:** ASP.NET Core auth/JWT, Konscious.Security.Cryptography Argon2id or bcrypt fallback if restore fails, EF Core user store, xUnit.

---

## Files

- Modify: `src/HydraForge.Domain/Common/DomainErrorCodes.cs`
- Modify: `src/HydraForge.Application/HydraForge.Application.csproj`
- Create: `src/HydraForge.Application/Auth/*`
- Modify: `src/HydraForge.Infrastructure/HydraForge.Infrastructure.csproj`
- Create: `src/HydraForge.Infrastructure/Auth/*`
- Modify: `src/HydraForge.Server/HydraForge.Server.csproj`
- Modify: `src/HydraForge.Server/Program.cs`
- Create: `src/HydraForge.Server/Auth/*`
- Create: `tests/HydraForge.Application.Tests/Auth/*`
- Read-only context: Task 2 and Task 3 merged code, current csproj files.

## Steps

- [ ] **Step 1: Write failing Application auth tests**

Cover valid login, wrong password, disabled user. Use in-memory fake `IUserRepository`, fake `IPasswordHasher`, fake `IAccessTokenIssuer`.

Expected assertions:

```csharp
Assert.True(result.IsSuccess);
Assert.Equal("jwt-token", result.Value.AccessToken);
Assert.Equal(DomainErrorCodes.Auth.InvalidCredentials, result.Error.Code);
Assert.Equal(DomainErrorCodes.Auth.UserDisabled, disabledResult.Error.Code);
```

- [ ] **Step 2: Add Application auth contracts/use case**

Create:

- `LoginRequest(string Username, string Password)`
- `LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, Guid UserId, string Username, bool IsAdmin)`
- `IUserRepository` with `FindByUsernameAsync`, `UpdateLastLoginAsync`, `AnyAdminExistsAsync`, `CreateAsync`
- `IPasswordHasher` with `HashPassword`, `VerifyPassword`
- `IAccessTokenIssuer` with `IssueToken(User user)`
- `LoginUserHandler` returning `Result<LoginResponse>`

- [ ] **Step 3: Run Application tests**

```bash
dotnet test tests/HydraForge.Application.Tests/HydraForge.Application.Tests.csproj
```

Expected: auth tests pass after implementation.

- [ ] **Step 4: Implement Infrastructure password hasher**

Prefer Argon2id using per-password random salt and encoded hash string containing params. If Argon2 dependency cannot restore, use BCrypt.Net-Next with cost 12 and document package choice in commit body.

- [ ] **Step 5: Implement EF user repository and admin seeder**

Admin seed behavior:

- If any admin exists, do nothing.
- If no admin exists and username/password missing, return `AUTH_ADMIN_SEED_NOT_CONFIGURED`.
- If no admin exists and env values exist, create exactly one enabled admin with normalized username.

- [ ] **Step 6: Add JWT config and issuer in Server or Infrastructure adapter**

Config keys:

- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:SigningKey`
- `Jwt:AccessTokenMinutes`

Claims: `sub`, `name`, `is_admin`.

- [ ] **Step 7: Expose auth endpoint**

Map `POST /api/auth/login` with body `{ "username": "admin", "password": "..." }` and response `LoginResponse`.

Failure mapping can use basic ProblemDetails until Task 5 replaces mapper.

- [ ] **Step 8: Enable JWT bearer validation**

Register authentication/authorization in `Program.cs`. Add `app.UseAuthentication()` before `app.UseAuthorization()`.

- [ ] **Step 9: Add admin seed startup hook**

After migrations, resolve seeder and run once. Log result without printing password.

- [ ] **Step 10: Verify**

```bash
dotnet test
dotnet build
```

Expected: all tests/build pass.

- [ ] **Step 11: Commit task branch**

```bash
git add src tests
git commit -m "feat: add local jwt auth"
git push
```
