# Task 5: Global exception middleware and ProblemDetails mapping
**Branch:** `task/phase-1-problemdetails`
**Parent branch:** `feat/phase-1-foundation`
**Parent spec:** `2026-06-02-phase-1-foundation-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:test-driven-development, then superpowers:executing-plans.

**Goal:** Catch unhandled server exceptions and map expected Application `Result` failures to RFC 7807 `ProblemDetails` with `correlationId` and named error code.

**Architecture:** Server owns HTTP concerns. Domain/Application keep pure `Result<T, Error>`. Middleware is humble: catch/log/map only.

**Tech Stack:** ASP.NET Core middleware, `ProblemDetails`, xUnit server tests.

---

## Files

- Create: `src/HydraForge.Server/Errors/ProblemDetailsMapper.cs`
- Create: `src/HydraForge.Server/Middleware/GlobalExceptionMiddleware.cs`
- Modify: `src/HydraForge.Server/Program.cs`
- Create: `tests/HydraForge.Server.Tests/HydraForge.Server.Tests.csproj`
- Create: `tests/HydraForge.Server.Tests/Errors/*`
- Modify: `HydraForge.slnx`
- Read-only context: Task 2 error primitives, Task 4 auth endpoint if merged, current Server Program.

## Steps

- [ ] **Step 1: Add Server test project**

Reference `HydraForge.Server` and use ASP.NET Core test host packages compatible with net10.0.

- [ ] **Step 2: Write failing mapper tests**

Test that known codes map to status/title/type:

```csharp
Error error = new(DomainErrorCodes.Auth.InvalidCredentials, "Invalid username or password.");
ProblemDetails details = ProblemDetailsMapper.FromError(error, "corr-1");

Assert.Equal(401, details.Status);
Assert.Equal("Invalid credentials", details.Title);
Assert.Equal("corr-1", details.Extensions["correlationId"]);
Assert.Equal(DomainErrorCodes.Auth.InvalidCredentials, details.Extensions["code"]);
```

- [ ] **Step 3: Write failing middleware test**

Create test endpoint that throws `InvalidOperationException("boom")`. Assert response status 500, JSON has `correlationId`, JSON does not contain `boom` or stack trace.

- [ ] **Step 4: Implement mapper**

Map codes:

- `AUTH_INVALID_CREDENTIALS` → 401
- `AUTH_USER_DISABLED` → 403
- `AUTH_ADMIN_SEED_NOT_CONFIGURED` → 500
- `DATABASE_UNAVAILABLE` → 503
- `AUDIT_WRITE_FAILED` → 500
- unknown Domain error → 400

Set `type` to `https://hydraforge.local/errors/<lower-kebab-code>`.

- [ ] **Step 5: Implement middleware**

Behavior:

- Read correlation ID from `HttpContext.Items["CorrelationId"]` if present, else `TraceIdentifier`.
- Log exception at Error.
- Return `application/problem+json`.
- Include `correlationId` extension.
- Never include stack trace or exception message in response body for 5xx.

- [ ] **Step 6: Wire middleware early**

In `Program.cs`, call global exception middleware before auth/endpoints.

- [ ] **Step 7: Run verification**

```bash
dotnet test tests/HydraForge.Server.Tests/HydraForge.Server.Tests.csproj
dotnet test
```

Expected: all tests pass.

- [ ] **Step 8: Commit task branch**

```bash
git add HydraForge.slnx src/HydraForge.Server tests/HydraForge.Server.Tests
git commit -m "feat: add problem details middleware"
git push
```
