# Task 8: Audit log infrastructure
**Branch:** `task/phase-1-audit`
**Parent branch:** `feat/phase-1-foundation`
**Parent spec:** `2026-06-02-phase-1-foundation-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:test-driven-development, then superpowers:executing-plans.

**Goal:** Add audit service abstraction and EF-backed writer capable of persisting `AuditLogEntry` for future mutation use cases.

**Architecture:** Application defines audit contract. Infrastructure persists through DbContext. No broad EF interception in Phase 1; future Application services call audit explicitly.

**Tech Stack:** C#, EF Core, xUnit.

---

## Files

- Create: `src/HydraForge.Application/Audit/*`
- Create: `src/HydraForge.Infrastructure/Audit/*`
- Modify: `src/HydraForge.Infrastructure/Persistence/DependencyInjection.cs`
- Create/modify: `tests/HydraForge.Application.Tests/Audit/*`
- Create/modify: `tests/HydraForge.Infrastructure.Tests/Audit/*`
- Read-only context: Task 3 `AuditLogEntry` entity/DbContext, Task 2 `Result`.

## Steps

- [ ] **Step 1: Write failing Application contract tests**

Create `AuditLogRequest` with actor, project, entity type/id, action, old/new values. Validate required fields before persistence and return `AUDIT_WRITE_FAILED` only for writer failure, not missing required input.

- [ ] **Step 2: Add Application audit contract**

Create:

- `AuditLogRequest(Guid ActorId, Guid? ProjectId, string EntityType, Guid EntityId, string Action, string? OldValueJson, string? NewValueJson)`
- `IAuditLogWriter` with `Task<Result> WriteAsync(AuditLogRequest request, CancellationToken cancellationToken)`
- `AuditService` wrapping writer and validation.

- [ ] **Step 3: Implement EF audit writer**

Map request to `AuditLogEntry`, set timestamp using injected clock or `DateTimeOffset.UtcNow` if no clock abstraction exists.

On `DbUpdateException`, log error and return `Result.Failure(new Error(DomainErrorCodes.Infrastructure.AuditWriteFailed, "Audit log write failed."))`.

- [ ] **Step 4: Add Infrastructure tests**

Use real PostgreSQL when available. If repo has no PostgreSQL test harness yet, add a skipped-by-default integration test controlled by env `HYDRAFORGE_TEST_CONNECTION_STRING`:

```csharp
string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString)) return;
```

Test inserts one audit entry and reads it back from DbContext.

- [ ] **Step 5: Register services in DI**

Register `IAuditLogWriter` and `AuditService` in Application/Infrastructure DI extension used by Server.

- [ ] **Step 6: Run verification**

```bash
dotnet test tests/HydraForge.Application.Tests/HydraForge.Application.Tests.csproj
dotnet test tests/HydraForge.Infrastructure.Tests/HydraForge.Infrastructure.Tests.csproj
dotnet build
```

Expected: pass; DB integration test runs only when connection string env var exists.

- [ ] **Step 7: Commit task branch**

```bash
git add src/HydraForge.Application src/HydraForge.Infrastructure tests
git commit -m "feat: add audit log writer"
git push
```
