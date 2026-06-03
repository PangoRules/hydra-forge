# Task 7: Health endpoint and service probes
**Branch:** `task/phase-1-health`
**Parent branch:** `feat/phase-1-foundation`
**Parent spec:** `2026-06-02-phase-1-foundation-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:test-driven-development, then superpowers:executing-plans.

**Goal:** Expose `/health` reporting server, database connectivity/migration status, and LLM-provider placeholder/probe status.

**Architecture:** Application defines health contracts. Infrastructure implements DB and LLM provider probes. Server maps health DTO to HTTP response. No controller queries DbContext directly.

**Tech Stack:** ASP.NET Core minimal endpoint, EF Core, xUnit.

---

## Files

- Create: `src/HydraForge.Application/Health/*`
- Create: `src/HydraForge.Infrastructure/Health/*`
- Modify: `src/HydraForge.Infrastructure/Persistence/DependencyInjection.cs`
- Modify: `src/HydraForge.Server/Program.cs`
- Create/modify: `tests/HydraForge.Application.Tests/Health/*`
- Create/modify: `tests/HydraForge.Server.Tests/Health/*`
- Read-only context: Task 3 persistence, Task 5/6 Server test infrastructure.

## Steps

- [ ] **Step 1: Write failing Application health tests**

Use fake probes. Assert service returns:

- overall `Healthy` when server and DB healthy, no LLM providers configured
- overall `Unhealthy` when DB unhealthy
- LLM status `NotConfigured` does not fail whole service

- [ ] **Step 2: Add Application health contracts**

Create:

- `HealthStatus` enum: `Healthy`, `Degraded`, `Unhealthy`, `NotConfigured`
- `HealthComponent(string Name, HealthStatus Status, string Detail)`
- `HealthReportDto(HealthStatus Status, IReadOnlyList<HealthComponent> Components)`
- `IHealthProbe` with `Task<HealthComponent> CheckAsync(CancellationToken)`
- `GetHealthHandler`

- [ ] **Step 3: Implement DB probe in Infrastructure**

Use DbContext:

- `Database.CanConnectAsync()`
- `Database.GetPendingMigrationsAsync()`

Return Unhealthy if cannot connect. Return Degraded if pending migrations exist. Return Healthy when connected and no pending migrations.

- [ ] **Step 4: Implement LLM placeholder probe**

If no enabled `LlmProvider` rows exist, return `NotConfigured` with detail `No LLM providers configured.` Do not fail overall status.

- [ ] **Step 5: Map `/health` endpoint**

Return JSON with:

```json
{
  "status": "Healthy",
  "components": [
    { "name": "server", "status": "Healthy", "detail": "Server is running." }
  ]
}
```

HTTP status: 200 for Healthy/Degraded, 503 for Unhealthy.

- [ ] **Step 6: Write Server endpoint tests**

Assert `/health` returns component names `server`, `database`, `llmProviders` and correlation header still exists.

- [ ] **Step 7: Run verification**

```bash
dotnet test tests/HydraForge.Application.Tests/HydraForge.Application.Tests.csproj
dotnet test tests/HydraForge.Server.Tests/HydraForge.Server.Tests.csproj
dotnet build
```

Expected: pass.

- [ ] **Step 8: Commit task branch**

```bash
git add src/HydraForge.Application src/HydraForge.Infrastructure src/HydraForge.Server tests
git commit -m "feat: add health endpoint"
git push
```
