# Foundation Schema Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Align the Phase 1 foundation entity schema with the currently intended requirements without implementing behavior or future-only services.

**Architecture:** Domain entities remain persistence-agnostic POCOs under `src/HydraForge.Domain`. EF configuration stays centralized in `HydraForgeDbContext`; the initial migration is regenerated after entity changes because this branch is still pre-merge. Tests assert EF model shape for each aligned domain area.

**Tech Stack:** .NET 10, EF Core 10, Npgsql, pgvector, xUnit.

---

### Task 1: Add Schema Contract Tests

**Files:**
- Modify: `tests/HydraForge.Infrastructure.Tests/Persistence/HydraForgeDbContextModelTests.cs`

- [ ] Add failing EF model tests for project board, chat, project context, personal memory/notes, document/gallery, and notification schema properties.
- [ ] Run `dotnet test tests/HydraForge.Infrastructure.Tests/HydraForge.Infrastructure.Tests.csproj --filter FullyQualifiedName~HydraForgeDbContextModelTests`.
- [ ] Confirm failures are missing properties or wrong enum/property types.

### Task 2: Align Domain Entities And Enums

**Files:**
- Modify: `src/HydraForge.Domain/Entities/ProjectSpace/*.cs`
- Modify: `src/HydraForge.Domain/Entities/Chat/*.cs`
- Modify: `src/HydraForge.Domain/Entities/PersonalSpace/*.cs`
- Modify: `src/HydraForge.Domain/Enums/*.cs`
- Add: `src/HydraForge.Domain/Entities/ProjectSpace/CardWatcher.cs`

- [ ] Add required foundation fields from `docs/requirements-and-architecture.md`.
- [ ] Prefer documented names unless the repository has already standardized on `CreatedAt` over `Timestamp`.
- [ ] Keep existing useful fields when they do not conflict with docs.
- [ ] Do not add behavior, navigation collections, validation services, or future-only routing entities.

### Task 3: Update EF Configuration And Regenerate Initial Migration

**Files:**
- Modify: `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`
- Regenerate: `src/HydraForge.Infrastructure/Migrations/20260603054055_InitialCreate.cs`
- Regenerate: `src/HydraForge.Infrastructure/Migrations/20260603054055_InitialCreate.Designer.cs`
- Regenerate: `src/HydraForge.Infrastructure/Migrations/HydraForgeDbContextModelSnapshot.cs`

- [ ] Add `DbSet<CardWatcher>`.
- [ ] Update indexes for renamed or newly added ordering, source, card/project, and reporting fields.
- [ ] Run `PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations remove --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server --force`.
- [ ] Run `PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations add InitialCreate --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server`.
- [ ] Run `PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server` and expect no pending changes.

### Task 4: Update Docs For Intentional Deviations

**Files:**
- Modify: `docs/requirements-and-architecture.md`
- Modify: `docs/DECISIONS.md` if a real decision changes

- [ ] Document `CreatedAt` as the stored timestamp name where needed.
- [ ] Document any intentionally retained extra accounting/status fields.
- [ ] Do not rewrite historical `docs/superpowers/plans/*` files.

### Task 5: Verify And Commit

**Files:**
- All changed files from tasks above

- [ ] Run `dotnet test tests/HydraForge.Infrastructure.Tests/HydraForge.Infrastructure.Tests.csproj`.
- [ ] Run `dotnet build`.
- [ ] Run `git status --short`, `git diff`, and `git log --oneline -10`.
- [ ] Commit with a conventional message such as `fix(domain): align foundation schema`.
- [ ] Push the task branch.
