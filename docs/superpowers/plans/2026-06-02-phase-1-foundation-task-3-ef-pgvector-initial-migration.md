# Task 3: EF Core, Npgsql, entities, pgvector, and initial migration
**Branch:** `task/phase-1-ef-pgvector`
**Parent branch:** `feat/phase-1-foundation`
**Parent spec:** `2026-06-02-phase-1-foundation-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:test-driven-development, then superpowers:executing-plans.

**Goal:** Add Phase 1 schema foundation with EF Core/Npgsql, pgvector extension, vector columns, DbContext, mappings, migration, and startup migration hook.

**Architecture:** Domain entities stay persistence-ignorant. Infrastructure maps Domain entities through EF Core. Server composes DbContext and optional migration application.

**Tech Stack:** EF Core 10, Npgsql, pgvector, PostgreSQL 16, xUnit, real PostgreSQL for migration verification.

---

## Files

- Modify: `src/HydraForge.Infrastructure/HydraForge.Infrastructure.csproj`
- Modify: `src/HydraForge.Server/HydraForge.Server.csproj`
- Modify: `src/HydraForge.Server/Program.cs`
- Create: `src/HydraForge.Domain/<domain folders>/*.cs`
- Create: `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`
- Create: `src/HydraForge.Infrastructure/Persistence/DependencyInjection.cs`
- Create: `src/HydraForge.Infrastructure/Persistence/DesignTimeHydraForgeDbContextFactory.cs`
- Create: `src/HydraForge.Infrastructure/Migrations/*`
- Create: `tests/HydraForge.Infrastructure.Tests/HydraForge.Infrastructure.Tests.csproj`
- Create: `tests/HydraForge.Infrastructure.Tests/Persistence/HydraForgeDbContextModelTests.cs`
- Modify: `HydraForge.slnx`
- Read-only context: Domain/Application/Infrastructure/Server csproj files, existing placeholder tests, docs data model.

## Domain entity set

Implement entities/enums from spec data model needed for schema only:

- Project space: `Project`, `Column`, `Card`, `CardAssignee`, `CardRelationship`, `Comment`, `ChecklistItem`, `Attachment`, `Spec`, `SpecVersion`, `Plan`, `PlanVersion`, `AuditLogEntry`, `ProjectMember`, `ProjectContextSnapshot`
- Auth/user: `User`
- Chat skeleton: `ChatFolder`, `ChatSession`, `ChatMessage`, `CardChatLink`
- Admin/LLM/usage: `LlmProvider`, `FeatureModelConfig`, `UserTokenBudget`, `TokenUsageRecord`, `ImageUsageRecord`
- Personal space schema: `AgentPersonality`, `MemoryEntry`, `Note`, `NoteReminder`, `NoteImageAttachment`, `PersonalTask`, `CalendarSource`, `CalendarEvent`, `Document`, `DocumentVersion`, `DocumentChunk`, `GalleryImage`, `Album`, `AlbumImage`, `ImageTag`, `Notification`
- Enums: `CardType`, `RelationshipType`, `MemberRole`, `MessageRole`, `ProviderType`, `ModelTier`, `MemoryCategory`, `TagSource`

## Steps

- [ ] **Step 1: Add Infrastructure test project before implementation**

Create test project referencing Infrastructure and Domain. Add to `HydraForge.slnx` under `/tests/`.

Use package refs matching existing xUnit style plus EF relational assertions if needed.

- [ ] **Step 2: Write failing EF model tests**

Tests verify:

```csharp
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

HydraForgeDbContext db = CreateContext();
IModel model = db.Model;

Assert.NotNull(model.FindEntityType(typeof(MemoryEntry)));
Assert.NotNull(model.FindEntityType(typeof(DocumentChunk)));
Assert.Contains(model.GetEntityTypes(), entity => entity.GetTableName() == "users");
Assert.Contains(model.GetEntityTypes(), entity => entity.GetTableName() == "cards");
```

Also assert indexes:

- `User.UsernameNormalized` unique
- `(Card.ProjectId, Card.CardNumber)` unique
- `ProjectContextSnapshot.ProjectId` unique

- [ ] **Step 3: Run failing tests**

```bash
dotnet test tests/HydraForge.Infrastructure.Tests/HydraForge.Infrastructure.Tests.csproj
```

Expected: compile fails because Infrastructure test project/DbContext/entities do not exist.

- [ ] **Step 4: Add EF/Npgsql packages**

Infrastructure packages:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.8" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.8" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
<PackageReference Include="Pgvector.EntityFrameworkCore" Version="0.2.2" />
```

Adjust exact package versions only if restore proves incompatibility with .NET 10/Npgsql 10; keep same major line.

- [ ] **Step 5: Create Domain entities without EF attributes**

Use public classes with get/set properties for EF materialization. Keep behavior minimal: defaults for `Id = Guid.NewGuid()` and timestamps where safe. No EF imports.

- [ ] **Step 6: Create `HydraForgeDbContext`**

Expose `DbSet<T>` for all entities. In `OnModelCreating`, map snake_case table names, required fields, max lengths, delete behaviors, indexes, owned/simple arrays where necessary.

Use `modelBuilder.HasPostgresExtension("vector")` and pgvector mapping for:

- `MemoryEntry.Embedding` → `vector(1536)`
- `DocumentChunk.Embedding` → `vector(1536)`

- [ ] **Step 7: Add Infrastructure DI extension**

Create `AddPersistence(this IServiceCollection services, IConfiguration configuration)` in Infrastructure (`PersistenceServiceCollectionExtensions`). It reads `ConnectionStrings:Default` and registers DbContext with Npgsql.

- [ ] **Step 8: Wire Server composition root**

In `Program.cs`, remove weather endpoint. Add Infrastructure DI. Add startup migration only when config `Database:ApplyMigrationsOnStartup` is true, default true in Development/local.

- [ ] **Step 9: Generate initial migration**

Run:

```bash
dotnet ef migrations add InitialCreate --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```

Expected: migration contains `CREATE EXTENSION IF NOT EXISTS vector` before vector columns and creates all mapped tables/indexes.

- [ ] **Step 10: Run tests and build**

```bash
dotnet test tests/HydraForge.Infrastructure.Tests/HydraForge.Infrastructure.Tests.csproj
dotnet build
```

Expected: tests and build pass.

- [ ] **Step 11: Commit task branch**

```bash
git add HydraForge.slnx src/HydraForge.Domain src/HydraForge.Infrastructure src/HydraForge.Server tests/HydraForge.Infrastructure.Tests
git commit -m "feat: add ef pgvector schema"
git push
```
