# Task 6: Structured logging and correlation ID pipeline
**Branch:** `task/phase-1-logging-correlation`
**Parent branch:** `feat/phase-1-foundation`
**Parent spec:** `2026-06-02-phase-1-foundation-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:test-driven-development, then superpowers:executing-plans.

**Goal:** Generate/accept correlation IDs per request, emit them in response headers, ProblemDetails, and structured Serilog logs.

**Architecture:** Server owns request pipeline/logging. Inner layers never depend on Serilog. Middleware stores correlation ID in `HttpContext.Items` for downstream adapters.

**Tech Stack:** ASP.NET Core middleware, Serilog.AspNetCore, xUnit server tests.

---

## Files

- Modify: `src/HydraForge.Server/HydraForge.Server.csproj`
- Create: `src/HydraForge.Server/Middleware/CorrelationIdMiddleware.cs`
- Modify: `src/HydraForge.Server/Middleware/GlobalExceptionMiddleware.cs`
- Modify: `src/HydraForge.Server/Program.cs`
- Modify: `src/HydraForge.Server/appsettings.json` if present; otherwise create it
- Create/modify: `tests/HydraForge.Server.Tests/*`
- Read-only context: Task 5 middleware and mapper, current Server csproj.

## Steps

- [ ] **Step 1: Write failing correlation tests**

Use server test host. Assert:

- Request without `X-Correlation-Id` returns header with generated non-empty value.
- Request with `X-Correlation-Id: test-corr` returns same header.
- Exception ProblemDetails includes same correlation ID.

- [ ] **Step 2: Add Serilog packages**

Server package refs:

```xml
<PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
<PackageReference Include="Serilog.Settings.Configuration" Version="10.0.0" />
<PackageReference Include="Serilog.Enrichers.CorrelationId" Version="3.0.1" />
```

Adjust minor versions only if restore requires.

- [ ] **Step 3: Implement `CorrelationIdMiddleware`**

Rules:

- Header name: `X-Correlation-Id`.
- Accept non-empty incoming value up to 128 chars.
- Generate `req_` + 16 random URL-safe chars when absent/blank/too long.
- Set `HttpContext.Items["CorrelationId"]`.
- Add response header.

- [ ] **Step 4: Configure Serilog in Program**

Use `builder.Host.UseSerilog(...)`. Enrich logs with correlation ID via middleware context and request logging.

Request log fields must include endpoint, duration, status code, userId when authenticated, and correlationId.

- [ ] **Step 5: Update exception middleware**

Read ID from correlation middleware item. Log with structured property `CorrelationId`.

- [ ] **Step 6: Add appsettings logging config**

Set production/default minimum to Warning through config, Development override to Information if existing conventions support it.

- [ ] **Step 7: Run tests/build**

```bash
dotnet test tests/HydraForge.Server.Tests/HydraForge.Server.Tests.csproj
dotnet build
```

Expected: pass.

- [ ] **Step 8: Commit task branch**

```bash
git add src/HydraForge.Server tests/HydraForge.Server.Tests
git commit -m "feat: add correlation logging"
git push
```
