# Card Doc Rules + Project Documents — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Backend-enforce new card-type→doc-type rules (Goal=N Specs[Specification+ValidationMatrix], Task=N Plans+N ValidationMatrix Specs, Issue=N Reports, Idea=N Concepts, Security=N Reports), add `CardType.Security` + `DocType.ValidationMatrix`, add `ProjectDocument` entity for project-level versioned docs, restructure project page into Board/Docs tabs with full-bleed Tiptap editor, and fix CardCreateModal parent/assignee selectors. See "Plan Revisions" section above for corrections to the tasks below.

**Architecture:** Domain enums/entities first, then Application services mirroring Spec/Plan patterns, then Server controllers, then Web UI fixes (selector → docs tab → Documents page), then TUI, then tests, then docs. `ProjectDocument` follows the same versioned-document pattern as Spec/Plan (entity + version snapshot table, CRUD + restore).

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, Nuxt 4 + Nuxt UI v4, Spectre.Console TUI, xUnit + NSubstitute.

**New decisions this plan introduces (for later addition to DECISIONS.md):**
- **D-XX1: Goal-no-Plans** — Goal cards no longer own Plans directly. A Goal's "plan" is the set of child Task cards. `Card.ValidateAllowsPlan` rejects Goal. Existing Goal-owned Plans are deleted.
- **D-XX2: Security card type** — `CardType.Security = 6`. Same doc shape as Issue (one Report Spec, no Plans) but distinct type for filtering/reporting. General projects have security concerns too (physical security, compliance, access control).
- **D-XX3: ValidationMatrix DocType** — `DocType.ValidationMatrix = 4`. A structured checklist of validation steps (unit tests, e2e, manual checks). Allowed on Goal and Task cards. Replaces loose `docs/archive/manual-validation/*.md` files.
- **D-XX4: ProjectDocument entity** — Project-level versioned documents not owned by a card. `ProjectDocType` enum: `Scope`, `Glossary`, `DataModel`, `Architecture`, `FunctionalSpec`, `Decisions`, `Reference`. Unique-per-project for Scope/Glossary/DataModel/Architecture/FunctionalSpec/Decisions; N allowed for Reference. Versioned via `ProjectDocumentVersion` (mirrors SpecVersion/PlanVersion).
- **D-XX5: Decisions-as-ProjectDocument** — `docs/DECISIONS.md` lives as a `ProjectDocument` with `DocType.Decisions`. LLM or user appends to it. Not a card on the board — it's a project-level document section.
- **D-XX6: Multiple docs per card (all types)** — The 1-Spec-per-card limit is removed entirely. Every card type that allows Specs can have N Specs within its allowed DocTypes. Goal=N Specs (Specification+ValidationMatrix), Issue=N Reports, Idea=N Concepts, Task=N ValidationMatrix Specs. Common case is 1, but unlimited. Each Concept on an Idea can spawn its own Goal.
- **D-XX7: Project page = Board tab + Docs tab** — The project board page becomes a two-tab layout: "Board" (existing board view) and "Docs" (project-level documents). Clicking a project document opens a full-bleed Tiptap editor using all available real estate. Not a separate `/documents` page — it's a tab on the existing project page.
- **D-XX8: Chat context-awareness (future)** — The Docs tab + in-app editable project documents are the foundation for the chat eventually having "hands" (knowing where the user is, interacting with docs/cards). Not built in this plan — noted as direction only.

---

## ⚠️ Plan Revisions (post-review — OVERRIDES contradictory sections below)

The following revisions take precedence over any contradictory text in Tasks 1–12 below. Read these first.

### R1: Multiple Specs per card — remove 1-per-card limit everywhere

**Overrides:** Task 1 Step 4, Task 1 Step 6, Task 3 (SpecService section), Task 11 Step 1 & Step 3.

The 1-Spec-per-card limit (`SpecService.cs` lines 80–84: `if (existingSpecs.Count > 0) return ...AlreadyExists`) is **deleted entirely**. All card types that allow Specs can have N Specs.

**`Card.ValidateAllowsSpec` (Task 1 Step 4) — corrected:**
```csharp
public static Error? ValidateAllowsSpec(CardType type) =>
    type is CardType.Goal or CardType.Idea or CardType.Issue or CardType.Security or CardType.Task
        ? null
        : new Error(DomainErrorCodes.Specs.InvalidCardType, $"{type} cards cannot have a Spec.");
```
Task is now included (it allows ValidationMatrix Specs).

**`Card.ExpectedSpecDocType` is REPLACED by `Card.IsValidSpecDocType(CardType, DocType)`** — the singular method is removed. SpecService validation (line 72) changes from `cmd.DocType != Card.ExpectedSpecDocType(card.Type)` to `!Card.IsValidSpecDocType(card.Type, cmd.DocType)`.

```csharp
public static bool IsValidSpecDocType(CardType cardType, DocType docType) =>
    cardType switch
    {
        CardType.Goal => docType is DocType.Specification or DocType.ValidationMatrix,
        CardType.Task => docType is DocType.ValidationMatrix,
        CardType.Idea => docType is DocType.Concept,
        CardType.Issue => docType is DocType.Report,
        CardType.Security => docType is DocType.Report,
        _ => false,
    };
```

**SpecService.CreateAsync changes:**
1. Replace `ExpectedSpecDocType` check (line 72) with `IsValidSpecDocType` check.
2. **Delete** the `existingSpecs.Count > 0` block (lines 80–84) — no count limit.
3. Error code for doc-type mismatch: reuse `SPEC_INVALID_DOC_TYPE_FOR_CARD` (already added in Task 1 Step 9).

**Task 11 Step 1 corrections:** Task → null (allows Spec, ValidationMatrix only). The test "Task → error" is WRONG — Task allows ValidationMatrix Spec. Test `IsValidSpecDocType` not `ExpectedSpecDocType`.

**Task 11 Step 3 corrections:** "Task card: cannot create Specification Spec" stays (wrong doc type), but "Task card: can create ValidationMatrix Spec" is the valid case. "Goal card: can create 2 Specs" → generalize to "can create N Specs of allowed DocTypes."

### R2: Project page = Board tab + Docs tab (not separate page)

**Overrides:** Task 9.

Task 9 becomes: **restructure the project board page into a two-tab layout** ("Board" and "Docs"), not a separate `/documents` page.

- The existing project board page (`src/web-ui/app/pages/projects/[id]/index.vue` or wherever the board lives — find it) gets a tab bar at the top: `Board` | `Docs`.
- `Board` tab = the existing board view (columns, cards, filters). Unchanged behavior, just wrapped in a tab.
- `Docs` tab = project-level documents list. Grouped by `ProjectDocType` with labels. Click a document → **full-bleed Tiptap editor** (`MarkdownEditor` maximized/fullscreen, using all available real estate, smooth open/close transition). Save → `PUT` endpoint. Version history + restore accessible from the editor.
- Create new document: button in Docs tab → DocType selector → create.
- Use `ApiRoutes.Documents.*` for all API calls. `useAppToast` for feedback. All `useApi()` in try/catch.
- Do NOT create `pages/projects/[id]/documents.vue` as a separate routed page. It's a tab within the project page.

### R3: ProjectDocument unique index — remove DB constraint

**Overrides:** Task 2 Step 2 (the `HasIndex(...).IsUnique()` block on ProjectDocument).

Remove the unique DB index on `(ProjectId, DocType)`. Uniqueness for single-doc types (Scope/Glossary/DataModel/Architecture/FunctionalSpec/Decisions) is enforced in `ProjectDocumentService.CreateAsync` (service-layer check via `GetByDocTypeAsync`, already in Task 3 Step 3). Reference allows N. A DB unique index would block Reference and complicate archive/restore flows. Service-layer check is sufficient and more flexible.

Keep the non-unique index `HasIndex(e => new { e.ProjectId, e.DocType })` for query performance — just drop `.IsUnique()`.

### R4: Chat context-awareness — direction only, not built

The Docs tab + in-app editable project docs are the foundation for future chat "hands" (chat knowing what the user is viewing/editing, eventually driving mutations). No code for this in the plan. Do not add context-tracking hooks or chat integration. Just build the Docs tab clean.

---

## Task 1: Domain — Enums, Entities, Validation Rules

**Files:**
- Modify: `src/HydraForge.Domain/Enums/CardType.cs`
- Modify: `src/HydraForge.Domain/Enums/DocType.cs`
- Create: `src/HydraForge.Domain/Enums/ProjectDocType.cs`
- Modify: `src/HydraForge.Domain/Entities/ProjectSpace/Card.cs`
- Create: `src/HydraForge.Domain/Entities/ProjectSpace/ProjectDocument.cs`
- Create: `src/HydraForge.Domain/Entities/ProjectSpace/ProjectDocumentVersion.cs`
- Modify: `src/HydraForge.Domain/Common/DomainErrorCodes.cs`

### Step 1: Add `CardType.Security = 6`

```csharp
// src/HydraForge.Domain/Enums/CardType.cs
namespace HydraForge.Domain.Enums;

public enum CardType
{
    Task = 1,
    Issue = 2,
    // 3 intentionally skipped — was Spec
    Idea = 4,
    Goal = 5,
    Security = 6,
}
```

### Step 2: Add `DocType.ValidationMatrix = 4`

```csharp
// src/HydraForge.Domain/Enums/DocType.cs
namespace HydraForge.Domain.Enums;

public enum DocType
{
    Specification = 1,
    Concept = 2,
    Report = 3,
    ValidationMatrix = 4,
}
```

### Step 3: Create `ProjectDocType` enum

```csharp
// src/HydraForge.Domain/Enums/ProjectDocType.cs
namespace HydraForge.Domain.Enums;

public enum ProjectDocType
{
    Scope = 1,
    Glossary = 2,
    DataModel = 3,
    Architecture = 4,
    FunctionalSpec = 5,
    Decisions = 6,
    Reference = 7,
}
```

### Step 4: Update `Card.ValidateAllowsSpec` — new rules

```csharp
// In Card.cs, replace ValidateAllowsSpec:
public static Error? ValidateAllowsSpec(CardType type) =>
    type is CardType.Goal or CardType.Idea or CardType.Issue or CardType.Security
        ? null
        : new Error(
            DomainErrorCodes.Specs.InvalidCardType,
            $"{type} cards cannot have a Spec."
        );
```

### Step 5: Update `Card.ValidateAllowsPlan` — new rules (Goal NO Plans, Security NO Plans)

```csharp
// In Card.cs, replace ValidateAllowsPlan:
public static Error? ValidateAllowsPlan(CardType type) =>
    type is CardType.Issue or CardType.Task
        ? null
        : new Error(
            DomainErrorCodes.Plans.InvalidCardType,
            $"{type} cards cannot have a Plan."
        );
```

### Step 6: Update `Card.ExpectedSpecDocType` — add Security→Report, allow ValidationMatrix on Goal

```csharp
// In Card.cs, replace ExpectedSpecDocType:
public static DocType ExpectedSpecDocType(CardType type) =>
    type switch
    {
        CardType.Goal => DocType.Specification,
        CardType.Idea => DocType.Concept,
        CardType.Issue => DocType.Report,
        CardType.Security => DocType.Report,
        _ => throw new InvalidOperationException($"{type} cards have no Spec."),
    };

// New method: which DocTypes are valid for a given CardType (for multi-spec support on Goal)
public static bool IsValidSpecDocType(CardType cardType, DocType docType) =>
    cardType switch
    {
        CardType.Goal => docType is DocType.Specification or DocType.ValidationMatrix,
        CardType.Task => docType is DocType.ValidationMatrix,
        CardType.Idea => docType is DocType.Concept,
        CardType.Issue => docType is DocType.Report,
        CardType.Security => docType is DocType.Report,
        _ => false,
    };
```

### Step 7: Create `ProjectDocument` entity

```csharp
// src/HydraForge.Domain/Entities/ProjectSpace/ProjectDocument.cs
using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.ProjectSpace;

public class ProjectDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public ProjectDocType DocType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
}
```

### Step 8: Create `ProjectDocumentVersion` entity

```csharp
// src/HydraForge.Domain/Entities/ProjectSpace/ProjectDocumentVersion.cs
namespace HydraForge.Domain.Entities.ProjectSpace;

public class ProjectDocumentVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectDocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
}
```

### Step 9: Add new DomainErrorCodes

```csharp
// In DomainErrorCodes.cs, add to Specs class:
public const string InvalidDocTypeForCard = "SPEC_INVALID_DOC_TYPE_FOR_CARD";

// Add new ProjectDocuments class:
public static class ProjectDocuments
{
    public const string NotFound = "PROJECT_DOCUMENT_NOT_FOUND";
    public const string DocumentVersionNotFound = "DOCUMENT_VERSION_NOT_FOUND";
    public const string MarkdownPayloadTooLarge = "MARKDOWN_PAYLOAD_TOO_LARGE";
    public const string DuplicateDocType = "PROJECT_DOCUMENT_DUPLICATE_DOC_TYPE";
}
```

### Step 10: Build + run Domain tests

```bash
dotnet build src/HydraForge.Domain
dotnet test tests/HydraForge.Domain.Tests
```

### Step 11: Commit

```bash
git add src/HydraForge.Domain/
git commit -m "feat: add Security card type, ValidationMatrix doc type, ProjectDocument entity, updated card doc rules"
```

---

## Task 2: EF Migration — New Tables + Enum Values

**Files:**
- Modify: `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`
- Create: Migration file (via `dotnet ef migrations add`)

### Step 1: Add DbSet properties to HydraForgeDbContext

```csharp
// In HydraForgeDbContext.cs, add after the PlanVersion DbSet:
public DbSet<ProjectDocument> ProjectDocuments => Set<ProjectDocument>();
public DbSet<ProjectDocumentVersion> ProjectDocumentVersions => Set<ProjectDocumentVersion>();
```

### Step 2: Add EF configuration in OnModelCreating

Add after the PlanVersion configuration block (around line 249):

```csharp
ConfigureEntity<ProjectDocument>(
    modelBuilder,
    "project_documents",
    b =>
    {
        b.HasIndex(e => e.ProjectId);
        b.Property(e => e.DocType)
            .HasColumnName("doc_type")
            .HasConversion<int>()
            .HasDefaultValue(ProjectDocType.Reference)
            .HasSentinel(default)
            .IsRequired();
        b.Property(e => e.Title).HasColumnName("title").IsRequired();
        b.Property(e => e.Description).HasColumnName("description").HasColumnType("text");
        b.Property(e => e.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        b.Property(e => e.Version).HasColumnName("version").IsRequired();
        b.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(e => e.ArchivedAt).HasColumnName("archived_at");
        b.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        // Unique index for single-doc-per-type constraint (Scope/Glossary/etc.)
        // Only enforced where ArchivedAt is null (partial unique index via filter)
        b.HasIndex(e => new { e.ProjectId, e.DocType })
            .HasDatabaseName("ix_project_documents_project_id_doc_type")
            .HasFilter("archived_at IS NULL")
            .IsUnique();
    }
);

ConfigureEntity<ProjectDocumentVersion>(
    modelBuilder,
    "project_document_versions",
    b =>
    {
        b.HasIndex(e => e.ProjectDocumentId);
        b.HasIndex(e => new { e.ProjectDocumentId, e.Version }).IsUnique();
        b.Property(e => e.Title).HasColumnName("title").IsRequired();
        b.Property(e => e.Description).HasColumnName("description").HasColumnType("text");
        b.Property(e => e.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        b.Property(e => e.Version).HasColumnName("version").IsRequired();
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.HasOne<ProjectDocument>()
            .WithMany()
            .HasForeignKey(e => e.ProjectDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
);
```

### Step 3: Generate migration

```bash
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef migrations add AddProjectDocumentsAndSecurityCardType \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server
```

### Step 4: Verify no pending model changes

```bash
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef migrations has-pending-model-changes \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server
```

Expected: "No pending model changes found."

### Step 5: Build + run Infrastructure tests

```bash
dotnet build src/HydraForge.Infrastructure
dotnet test tests/HydraForge.Infrastructure.Tests
```

### Step 6: Commit

```bash
git add src/HydraForge.Infrastructure/
git commit -m "feat: add ProjectDocument/ProjectDocumentVersion tables + migration"
```

---

## Task 3: Application — ProjectDocumentService + Updated Spec/Plan Rules

**Files:**
- Create: `src/HydraForge.Application/ProjectDocuments/IProjectDocumentRepository.cs`
- Create: `src/HydraForge.Application/ProjectDocuments/ProjectDocumentService.cs`
- Create: `src/HydraForge.Application/ProjectDocuments/ProjectDocumentDtos.cs`
- Modify: `src/HydraForge.Application/Specs/SpecService.cs`
- Modify: `src/HydraForge.Application/Plans/PlanService.cs`

### Step 1: Create `IProjectDocumentRepository`

```csharp
// src/HydraForge.Application/ProjectDocuments/IProjectDocumentRepository.cs
using HydraForge.Domain.Entities.ProjectSpace;

namespace HydraForge.Application.ProjectDocuments;

public interface IProjectDocumentRepository
{
    Task<ProjectDocument?> GetByIdAsync(Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectDocument>> ListByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectDocument?> GetByDocTypeAsync(Guid projectId, ProjectDocType docType, CancellationToken ct = default);
    Task<ProjectDocumentVersion?> GetVersionAsync(Guid documentId, int version, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectDocumentVersion>> ListVersionsAsync(Guid documentId, CancellationToken ct = default);
    Task AddAsync(ProjectDocument document, CancellationToken ct = default);
    Task AddVersionAsync(ProjectDocumentVersion version, CancellationToken ct = default);
    Task UpdateAsync(ProjectDocument document, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

### Step 2: Create `ProjectDocumentDtos.cs`

Mirror the Spec/Plan DTO pattern. Include commands, responses, and list filters.

```csharp
// src/HydraForge.Application/ProjectDocuments/ProjectDocumentDtos.cs
using HydraForge.Domain.Enums;

namespace HydraForge.Application.ProjectDocuments;

public record CreateProjectDocumentCommand(
    Guid ProjectId,
    ProjectDocType DocType,
    string Title,
    string? Description,
    string Content,
    Guid ActorId
);

public record UpdateProjectDocumentCommand(
    Guid ProjectId,
    Guid DocumentId,
    string Title,
    string? Description,
    string Content,
    Guid ActorId
);

public record RestoreProjectDocumentVersionCommand(
    Guid ProjectId,
    Guid DocumentId,
    int Version,
    Guid ActorId
);

public record ProjectDocumentDto(
    Guid Id,
    Guid ProjectId,
    ProjectDocType DocType,
    string Title,
    string? Description,
    string Content,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt
);

public record ProjectDocumentVersionDto(
    Guid Id,
    Guid ProjectDocumentId,
    int Version,
    string Title,
    string? Description,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);
```

### Step 3: Create `ProjectDocumentService`

Mirror `SpecService` pattern exactly: membership guard → validate → create entity + version snapshot → save → audit → publish → refresh snapshot. Include `CreateAsync`, `GetByIdAsync`, `ListAsync`, `UpdateAsync`, `ListVersionsAsync`, `RestoreVersionAsync`.

Key difference from SpecService: `CreateAsync` must check uniqueness for non-Reference DocTypes (Scope/Glossary/DataModel/Architecture/FunctionalSpec/Decisions). Use `GetByDocTypeAsync` — if a non-archived document already exists for that DocType, return `PROJECT_DOCUMENT_DUPLICATE_DOC_TYPE`. Reference DocType allows N documents.

```csharp
// src/HydraForge.Application/ProjectDocuments/ProjectDocumentService.cs
using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Realtime;
using HydraForge.Application.Shared;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.ProjectDocuments;

public class ProjectDocumentService(
    IProjectDocumentRepository docRepo,
    IProjectMemberRepository memberRepo,
    IUserRepository userRepo,
    IAuditLogWriter auditLogWriter,
    IProjectSnapshotRefresher snapshotRefresher,
    IProjectBoardEventPublisher publisher
)
{
    private static readonly HashSet<ProjectDocType> UniqueDocTypes = new()
    {
        ProjectDocType.Scope,
        ProjectDocType.Glossary,
        ProjectDocType.DataModel,
        ProjectDocType.Architecture,
        ProjectDocType.FunctionalSpec,
        ProjectDocType.Decisions,
    };

    private sealed record DocAuditSnapshot(string Title, string? Description, string Content);

    private static DocAuditSnapshot BuildSnapshot(ProjectDocument doc) =>
        new(doc.Title, doc.Description, doc.Content);

    public async Task<Result<ProjectDocumentDto>> CreateAsync(
        CreateProjectDocumentCommand cmd,
        CancellationToken ct = default
    )
    {
        if (!await MembershipGuard.HasAccessAsync(userRepo, memberRepo, cmd.ProjectId, cmd.ActorId, ct))
            return Result<ProjectDocumentDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied."));

        if (cmd.Content.Length > DocumentMarkdownLimits.MaxMarkdownPayloadBytes)
            return Result<ProjectDocumentDto>.Failure(
                new Error(DomainErrorCodes.ProjectDocuments.MarkdownPayloadTooLarge,
                    "Markdown payload exceeds limit."));

        // Uniqueness check for single-doc-per-type DocTypes
        if (UniqueDocTypes.Contains(cmd.DocType))
        {
            var existing = await docRepo.GetByDocTypeAsync(cmd.ProjectId, cmd.DocType, ct);
            if (existing != null && existing.ArchivedAt == null)
                return Result<ProjectDocumentDto>.Failure(
                    new Error(DomainErrorCodes.ProjectDocuments.DuplicateDocType,
                        $"A {cmd.DocType} document already exists for this project."));
        }

        var doc = new ProjectDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = cmd.ProjectId,
            DocType = cmd.DocType,
            Title = cmd.Title,
            Description = cmd.Description,
            Content = cmd.Content,
            Version = 1,
            CreatedByUserId = cmd.ActorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var version = new ProjectDocumentVersion
        {
            Id = Guid.NewGuid(),
            ProjectDocumentId = doc.Id,
            Version = 1,
            Title = cmd.Title,
            Description = cmd.Description,
            Content = cmd.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await docRepo.AddAsync(doc, ct);
        await docRepo.AddVersionAsync(version, ct);
        await docRepo.SaveChangesAsync(ct);
        await snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await auditLogWriter.WriteAsync(new AuditLogRequest(
            cmd.ActorId, AuditLogScope.Project, "ProjectDocument", doc.Id, "Created",
            cmd.ProjectId, null, AuditSnapshot.Serialize(BuildSnapshot(doc))), ct);

        await PublishAsync(cmd.ProjectId, doc.Id, BoardAction.Created, ct);

        return Result<ProjectDocumentDto>.Success(MapToDto(doc));
    }

    // GetByIdAsync, ListAsync, UpdateAsync, ListVersionsAsync, RestoreVersionAsync
    // follow the exact same pattern as SpecService — see SpecService.cs lines 145-380.
    // UpdateAsync: membership guard → fetch doc → validate content size → snapshot old →
    //   update fields → create version → save → audit → publish.
    // RestoreVersionAsync: membership guard → fetch doc → fetch old version → snapshot old →
    //   restore fields → increment version → create new version → save → audit → publish.

    // ... (implement remaining methods following SpecService pattern)

    private async Task PublishAsync(Guid projectId, Guid docId, BoardAction action, CancellationToken ct)
    {
        var envelope = new ProjectBoardEventEnvelope(
            Guid.NewGuid(), projectId, BoardEntityType.ProjectDocument, docId,
            action, 1, DateTime.UtcNow, null!, null);
        await publisher.PublishAsync(envelope, ct);
    }

    private static ProjectDocumentDto MapToDto(ProjectDocument doc) =>
        new(doc.Id, doc.ProjectId, doc.DocType, doc.Title, doc.Description, doc.Content,
            doc.Version, doc.CreatedByUserId, doc.CreatedAt, doc.UpdatedAt, doc.ArchivedAt);
}
```

**Note:** `BoardEntityType.ProjectDocument` must be added to the `BoardEntityType` enum in Domain. Add it in Task 1 or here — whichever is cleaner. Value: add after the last existing member.

### Step 4: Update `SpecService.CreateAsync` — lift 1-Spec-per-card limit for Goal, enforce new doc-type rules

In `SpecService.CreateAsync` (line 80-84), the existing check:

```csharp
var existingSpecs = await _specRepo.ListByCardAsync(cmd.CardId, new SpecListFilter(), ct);
if (existingSpecs.Count > 0)
    return Result<SpecDto>.Failure(
        new Error(DomainErrorCodes.Specs.AlreadyExists, "Card already has a Spec."));
```

Replace with:

```csharp
// Goal cards allow multiple Specs (Specification + ValidationMatrix).
// All other card types: max 1 Spec.
if (card.Type != CardType.Goal)
{
    var existingSpecs = await _specRepo.ListByCardAsync(cmd.CardId, new SpecListFilter(), ct);
    if (existingSpecs.Count > 0)
        return Result<SpecDto>.Failure(
            new Error(DomainErrorCodes.Specs.AlreadyExists, "Card already has a Spec."));
}

// Validate DocType is valid for this card type
if (!Card.IsValidSpecDocType(card.Type, cmd.DocType))
    return Result<SpecDto>.Failure(
        new Error(DomainErrorCodes.Specs.InvalidDocTypeForCard,
            $"{cmd.DocType} is not a valid Spec type for {card.Type} cards."));
```

Also update the `ExpectedSpecDocType` check (line 72-78) — for Goal cards, the expected DocType is `Specification` for the first Spec, but `ValidationMatrix` is also valid. Change the check to use `IsValidSpecDocType` instead of strict `ExpectedSpecDocType` equality:

```csharp
// Replace lines 72-78:
if (!Card.IsValidSpecDocType(card.Type, cmd.DocType))
    return Result<SpecDto>.Failure(
        new Error(DomainErrorCodes.Specs.InvalidDocTypeForCard,
            $"{cmd.DocType} is not a valid Spec type for {card.Type} cards."));
```

### Step 5: Update `PlanService.CreateAsync` — Goal no longer allows Plans

The existing `Card.ValidateAllowsPlan` already rejects Goal (updated in Task 1 Step 5). The `SpecId` linking logic (lines 80-98) for Goal cards is now dead code — Goal cards can't create Plans. Remove the `SpecId` validation block for Goal cards, or keep it as a defensive check that will never be reached.

Simplify lines 80-98: remove the `if (cmd.SpecId != null)` block entirely since Goal cards can no longer create Plans. The `SpecId` field on Plan remains for backward compatibility with existing data but new Plans won't set it.

### Step 6: Build + run Application tests

```bash
dotnet build src/HydraForge.Application
dotnet test tests/HydraForge.Application.Tests
```

### Step 7: Commit

```bash
git add src/HydraForge.Application/
git commit -m "feat: add ProjectDocumentService, update Spec/Plan rules for new card types"
```

---

## Task 4: Infrastructure — EfProjectDocumentRepository + DI Wiring

**Files:**
- Create: `src/HydraForge.Infrastructure/ProjectDocuments/EfProjectDocumentRepository.cs`
- Create: `src/HydraForge.Infrastructure/ProjectDocuments/ProjectDocumentServiceCollectionExtensions.cs`
- Modify: `src/HydraForge.Server/Program.cs`

### Step 1: Create `EfProjectDocumentRepository`

Mirror `EfSpecRepository` pattern. Key method: `GetByDocTypeAsync` uses `FirstOrDefaultAsync` with `ArchivedAt == null` filter.

```csharp
// src/HydraForge.Infrastructure/ProjectDocuments/EfProjectDocumentRepository.cs
using HydraForge.Application.ProjectDocuments;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.ProjectDocuments;

public class EfProjectDocumentRepository(HydraForgeDbContext context) : IProjectDocumentRepository
{
    public async Task<ProjectDocument?> GetByIdAsync(Guid documentId, CancellationToken ct = default) =>
        await context.ProjectDocuments.FindAsync([documentId], ct);

    public async Task<IReadOnlyList<ProjectDocument>> ListByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await context.ProjectDocuments
            .Where(d => d.ProjectId == projectId && d.ArchivedAt == null)
            .OrderBy(d => d.DocType).ThenBy(d => d.Title)
            .ToListAsync(ct);

    public async Task<ProjectDocument?> GetByDocTypeAsync(Guid projectId, ProjectDocType docType, CancellationToken ct = default) =>
        await context.ProjectDocuments
            .FirstOrDefaultAsync(d => d.ProjectId == projectId && d.DocType == docType && d.ArchivedAt == null, ct);

    public async Task<ProjectDocumentVersion?> GetVersionAsync(Guid documentId, int version, CancellationToken ct = default) =>
        await context.ProjectDocumentVersions
            .FirstOrDefaultAsync(v => v.ProjectDocumentId == documentId && v.Version == version, ct);

    public async Task<IReadOnlyList<ProjectDocumentVersion>> ListVersionsAsync(Guid documentId, CancellationToken ct = default) =>
        await context.ProjectDocumentVersions
            .Where(v => v.ProjectDocumentId == documentId)
            .OrderByDescending(v => v.Version)
            .ToListAsync(ct);

    public async Task AddAsync(ProjectDocument document, CancellationToken ct = default) =>
        await context.ProjectDocuments.AddAsync(document, ct);

    public async Task AddVersionAsync(ProjectDocumentVersion version, CancellationToken ct = default) =>
        await context.ProjectDocumentVersions.AddAsync(version, ct);

    public Task UpdateAsync(ProjectDocument document, CancellationToken ct = default)
    {
        context.ProjectDocuments.Update(document);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        await context.SaveChangesAsync(ct);
}
```

### Step 2: Create `ProjectDocumentServiceCollectionExtensions`

```csharp
// src/HydraForge.Infrastructure/ProjectDocuments/ProjectDocumentServiceCollectionExtensions.cs
using HydraForge.Application.ProjectDocuments;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.ProjectDocuments;

public static class ProjectDocumentServiceCollectionExtensions
{
    public static IServiceCollection AddProjectDocumentServices(this IServiceCollection services)
    {
        services.AddScoped<IProjectDocumentRepository, EfProjectDocumentRepository>();
        services.AddScoped<ProjectDocumentService>();
        return services;
    }
}
```

### Step 3: Wire in `Program.cs`

Add after the existing `AddPlanServices()` line:

```csharp
builder.Services.AddProjectDocumentServices();
```

### Step 4: Build + verify

```bash
dotnet build
```

### Step 5: Commit

```bash
git add src/HydraForge.Infrastructure/ProjectDocuments/ src/HydraForge.Server/Program.cs
git commit -m "feat: add EfProjectDocumentRepository + DI wiring"
```

---

## Task 5: Server — ProjectDocumentsController

**Files:**
- Create: `src/HydraForge.Server/Controllers/Projects/ProjectDocumentsController.cs`
- Create: `src/HydraForge.Server/Controllers/Projects/ProjectDocumentRequests.cs`
- Modify: `src/HydraForge.Domain/Enums/BoardEntityType.cs` (add `ProjectDocument`)

### Step 1: Add `ProjectDocument` to `BoardEntityType`

```csharp
// In BoardEntityType.cs, add:
ProjectDocument = 12,  // or next available value
```

### Step 2: Create request/response DTOs

```csharp
// src/HydraForge.Server/Controllers/Projects/ProjectDocumentRequests.cs
using HydraForge.Domain.Enums;

namespace HydraForge.Server.Controllers.Projects;

public record CreateProjectDocumentRequest(
    ProjectDocType DocType,
    string Title,
    string? Description,
    string Content
);

public record UpdateProjectDocumentRequest(
    string Title,
    string? Description,
    string Content
);

public record RestoreProjectDocumentVersionRequest(int Version);

public record ProjectDocumentResponse(
    Guid Id,
    Guid ProjectId,
    ProjectDocType DocType,
    string Title,
    string? Description,
    string Content,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt
);

public record ProjectDocumentListResponse(IReadOnlyList<ProjectDocumentResponse> Documents);

public record ProjectDocumentVersionResponse(
    Guid Id,
    Guid ProjectDocumentId,
    int Version,
    string Title,
    string? Description,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

public record ProjectDocumentVersionListResponse(IReadOnlyList<ProjectDocumentVersionResponse> Versions);
```

### Step 3: Create `ProjectDocumentsController`

Mirror `SpecsController` pattern exactly. Route: `[Route("api/projects/{projectId:guid}/[controller]")]`. Endpoints:

- `POST /` — Create (returns 201)
- `GET /` — List all (returns 200)
- `GET /{documentId:guid}` — Get by ID (returns 200)
- `PUT /{documentId:guid}` — Update (returns 200)
- `GET /{documentId:guid}/versions` — List versions (returns 200)
- `POST /{documentId:guid}/restore` — Restore version (returns 200)

Include HTML↔Markdown conversion (same `InContent`/`OutContent` pattern as SpecsController).

### Step 4: Build + verify

```bash
dotnet build
```

### Step 5: Commit

```bash
git add src/HydraForge.Server/Controllers/Projects/ src/HydraForge.Domain/Enums/BoardEntityType.cs
git commit -m "feat: add ProjectDocumentsController"
```

---

## Task 6: Web UI — CardCreateModal USelectMenu Fix

**Files:**
- Modify: `src/web-ui/app/components/board/CardCreateModal.vue`

### Step 1: Replace raw `<select>` for Assignees with `USelectMenu`

Current (lines 138-153): raw `<select>` with `@change` handler pushing to array.

Replace with the `USelectMenu` pattern from `CardMetadata.vue` (lines 519-528):

```vue
<USelectMenu
  v-if="props.members && props.members.length > 0"
  :model-value="''"
  :items="availableMembers.map(m => ({ label: m.username, value: m.userId }))"
  value-key="value"
  size="xs"
  class="w-full"
  placeholder="+ Add assignee"
  @update:model-value="(v: string) => v && !selectedAssignees.includes(v) && selectedAssignees.push(v)"
/>
```

Add computed:

```ts
const availableMembers = computed(() =>
  (props.members ?? []).filter(m => !selectedAssignees.value.includes(m.userId))
)
```

### Step 2: Replace raw `<select>` for Parent with `USelectMenu`

Current (lines 205-219): raw `<select>` with `<option>` for each card.

Replace with the `USelectMenu` pattern from `CardMetadata.vue` (lines 404-413):

```vue
<USelectMenu
  :model-value="''"
  :items="parentCandidates.map(c => ({ label: `#${c.cardNumber} — ${c.title}`, value: c.id }))"
  value-key="value"
  size="xs"
  class="w-full"
  placeholder="Search cards..."
  @update:model-value="(v: string) => v && (selectedParentId = v)"
/>
```

Keep the existing `selectedParentId` ref and `parentCandidates` — just swap the `<select>` for `USelectMenu`.

### Step 3: Verify typecheck + lint

```bash
cd src/web-ui && pnpm typecheck && pnpm lint
```

### Step 4: Commit

```bash
git add src/web-ui/app/components/board/CardCreateModal.vue
git commit -m "fix: replace raw selects with USelectMenu in CardCreateModal"
```

---

## Task 7: Web UI — Card Type Updates (Security + ValidationMatrix)

**Files:**
- Modify: `src/web-ui/app/lib/card-type.ts`
- Modify: `src/web-ui/app/components/card/CardMetadata.vue`
- Modify: `src/web-ui/app/components/board/CardCreateModal.vue`

### Step 1: Add Security to `card-type.ts`

```ts
// In CARD_TYPE_MAP, add:
4: 'Security'

// In CARD_TYPE_ICONS, add:
4: 'i-lucide-shield'

// In CARD_TYPE_OPTIONS, add after Idea:
{ value: 4, apiValue: 'Security', label: 'Security', color: 'info', icon: 'i-lucide-shield' }

// In CARD_TYPE_FILTER_OPTIONS, add after Idea:
{ label: 'Security', value: 'Security' }
```

### Step 2: Update `CardMetadata.vue` type-change confirm logic

The `wouldLoseSpec`/`wouldLosePlan` logic (lines 102-106) needs updating for Security type. Security has same doc shape as Issue (one Report Spec, no Plans). Update the `isGoalOrIdea` and `isGoal` helpers to account for Security:

```ts
const hasSpec = (t: string) => ['Goal', 'Idea', 'Issue', 'Security'].includes(t)
const hasPlan = (t: string) => ['Issue', 'Task'].includes(t)

const wouldLoseSpec = hasSpec(currentType) && !hasSpec(newType)
const wouldLosePlan = hasPlan(currentType) && !hasPlan(newType)
```

### Step 3: Update `CardCreateModal.vue` type selector

The `CARD_TYPE_OPTIONS` import already includes Security after Step 1. No additional changes needed — the `<select>` already iterates `CARD_TYPE_OPTIONS`.

### Step 4: Verify typecheck + lint

```bash
cd src/web-ui && pnpm typecheck && pnpm lint
```

### Step 5: Commit

```bash
git add src/web-ui/app/lib/card-type.ts src/web-ui/app/components/card/CardMetadata.vue
git commit -m "feat: add Security card type to Web UI"
```

---

## Task 8: Web UI — CardModal Docs Tab Auto-Open + Updated Maps

**Files:**
- Modify: `src/web-ui/app/components/card/CardModal.vue`
- Modify: `src/web-ui/app/components/card/CardPlan.vue`

### Step 1: Update `SPEC_CARD_TYPES`, `PLAN_CARD_TYPES`, `CARD_TYPE_TO_DOC_TYPE`

In `CardModal.vue` (lines 43-47):

```ts
// Security has same doc shape as Issue
const SPEC_CARD_TYPES = ['Goal', 'Idea', 'Issue', 'Security'] as const
// Goal no longer has Plans; Security has no Plans
const PLAN_CARD_TYPES = ['Issue', 'Task'] as const

// Security → Report (same as Issue)
const CARD_TYPE_TO_DOC_TYPE: Record<string, string> = {
  Goal: 'Specification',
  Idea: 'Concept',
  Issue: 'Report',
  Security: 'Report'
}
```

### Step 2: Auto-open first document on Docs tab activation

Add a watcher on `activeTab` that auto-expands the first Spec or Plan when switching to the Docs tab. The `CardSpec` component auto-loads its spec on mount — no change needed there. For `CardPlan`, the first plan should be auto-expanded.

Add to `CardModal.vue` script:

```ts
// Auto-expand first plan when docs tab opens
const expandFirstPlan = ref(false)

watch(activeTab, (tab) => {
  if (tab === 'docs') {
    expandFirstPlan.value = true
  }
})
```

Pass `expandFirstPlan` to `CardPlan`:

```vue
<CardPlan
  :card-id="card.id"
  :project-id="projectId"
  :spec-id="String(card.type) === 'Goal' ? linkedSpecId : null"
  :readonly="isReadonly"
  :refresh-key="contentRefresh"
  :auto-expand-first="expandFirstPlan"
  @first-expanded="expandFirstPlan = false"
/>
```

### Step 3: Update `CardPlan.vue` to support `autoExpandFirst` prop

Add prop:

```ts
const props = defineProps<{
  // ... existing props
  autoExpandFirst?: boolean
}>()

const emit = defineEmits<{
  'first-expanded': []
}>()
```

In `fetchPlans()` (after plans are loaded), auto-expand the first plan:

```ts
async function fetchPlans() {
  loading.value = true
  try {
    const { data } = await api.GET(ApiRoutes.Plans.forCard(props.projectId, props.cardId))
    const list = data as { plans: PlanResponse[] } | undefined
    plans.value = (list?.plans ?? []).sort((a, b) => a.position - b.position)
    for (const p of plans.value) initEditState(p)
    // Auto-expand first plan
    if (props.autoExpandFirst && plans.value.length > 0) {
      expandedPlans.value = new Set([plans.value[0].id])
      emit('first-expanded')
    }
  } catch {
    toast.error('Failed to load plans')
  } finally {
    loading.value = false
  }
}
```

### Step 4: Verify typecheck + lint

```bash
cd src/web-ui && pnpm typecheck && pnpm lint
```

### Step 5: Commit

```bash
git add src/web-ui/app/components/card/CardModal.vue src/web-ui/app/components/card/CardPlan.vue
git commit -m "feat: update doc type maps, auto-expand first plan on docs tab"
```

---

## Task 9: Web UI — Project Documents Page

**Files:**
- Create: `src/web-ui/app/pages/projects/[id]/documents.vue`
- Modify: `src/web-ui/app/lib/routes.ts`
- Modify: Project board navigation (add Documents link)

### Step 1: Add API routes

```ts
// In routes.ts, add after Plans:
Documents: {
  list: (projectId: string) => `/api/projects/${projectId}/Documents`,
  create: (projectId: string) => `/api/projects/${projectId}/Documents`,
  detail: (projectId: string, docId: string) => `/api/projects/${projectId}/Documents/${docId}`,
  update: (projectId: string, docId: string) => `/api/projects/${projectId}/Documents/${docId}`,
  versions: (projectId: string, docId: string) => `/api/projects/${projectId}/Documents/${docId}/versions`,
  restore: (projectId: string, docId: string) => `/api/projects/${projectId}/Documents/${docId}/restore`,
},
```

### Step 2: Create Documents page

Create `src/web-ui/app/pages/projects/[id]/documents.vue`:

- Fetch project documents via `GET /api/projects/{projectId}/Documents`
- List documents grouped by DocType with labels (Scope, Glossary, Data Model, Architecture, Functional Spec, Decisions, Reference)
- Click a document → open in a `MarkdownEditor` (same component used by CardSpec/CardPlan)
- Save triggers `PUT /api/projects/{projectId}/Documents/{docId}`
- Create new document: `POST /api/projects/{projectId}/Documents` with DocType selector
- Version history + restore (same pattern as CardSpec)
- Use `AppModal` for the editor (or inline — follow existing Spec/Plan UX)
- Use `ApiRoutes.Documents.*` for all API calls
- Use `useAppToast` for success/error feedback
- Wrap all `useApi()` calls in try/catch

### Step 3: Add navigation link

Add a "Documents" link to the project board page navigation (alongside the existing board view). This can be a simple `<NuxtLink>` or tab in the project layout.

### Step 4: Verify typecheck + lint + build

```bash
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
```

### Step 5: Commit

```bash
git add src/web-ui/app/pages/projects/ src/web-ui/app/lib/routes.ts
git commit -m "feat: add project documents page"
```

---

## Task 10: TUI — Project Documents Viewer + Security Card Type

**Files:**
- Modify: TUI screens for project documents (new screen)
- Modify: TUI card type display (Security icon/label)
- Modify: TUI spec/plan viewer (ValidationMatrix support)

### Step 1: Add Security card type to TUI card display

Find where card types are rendered in the TUI (board view, card detail). Add Security with shield icon/color. Follow existing pattern for Issue (same doc shape).

### Step 2: Add ValidationMatrix doc type label

In the spec viewer, add "Validation Matrix" label for `DocType.ValidationMatrix`. Follow existing `DOC_TYPE_LABELS` pattern.

### Step 3: Create Project Documents viewer screen

New screen following the spec/plan viewer pattern:
- List project documents (fetch via API)
- Select to view/edit
- `$EDITOR` for editing (same as spec/plan)
- Version history + restore
- Keyboard shortcuts consistent with existing screens

### Step 4: Build + verify TUI

```bash
dotnet build src/HydraForge.Tui
```

### Step 5: Commit

```bash
git add src/HydraForge.Tui/
git commit -m "feat: add project documents viewer + Security card type to TUI"
```

---

## Task 11: Tests — Domain + Application + Infrastructure

**Files:**
- Create/modify: `tests/HydraForge.Domain.Tests/...`
- Create/modify: `tests/HydraForge.Application.Tests/...`
- Create/modify: `tests/HydraForge.Infrastructure.Tests/...`
- Modify: `tests/HydraForge.Server.Tests/...` (register new port stubs)

### Step 1: Domain tests — Card validation rules

Test `Card.ValidateAllowsSpec`:
- Goal → null (allows Spec)
- Idea → null
- Issue → null
- Security → null
- Task → error `SPEC_INVALID_CARD_TYPE`

Test `Card.ValidateAllowsPlan`:
- Issue → null
- Task → null
- Goal → error `PLAN_INVALID_CARD_TYPE`
- Idea → error `PLAN_INVALID_CARD_TYPE`
- Security → error `PLAN_INVALID_CARD_TYPE`

Test `Card.IsValidSpecDocType`:
- Goal + Specification → true
- Goal + ValidationMatrix → true
- Goal + Concept → false
- Task + ValidationMatrix → true
- Task + Specification → false
- Security + Report → true
- Security + Concept → false

### Step 2: Application tests — ProjectDocumentService

Follow `SpecServiceTests` pattern. Test:
- Create: membership denied, success, duplicate DocType rejection (for Scope/Glossary/etc.), Reference allows N
- GetById: not found, wrong project, success
- List: returns only non-archived
- Update: success, content too large
- RestoreVersion: success, version not found

### Step 3: Application tests — Updated SpecService/PlanService

Test SpecService:
- Goal card: can create 2 Specs (Specification + ValidationMatrix)
- Task card: can create ValidationMatrix Spec
- Task card: cannot create Specification Spec
- Security card: can create Report Spec
- Security card: cannot create Concept Spec

Test PlanService:
- Goal card: cannot create Plan (rejected by ValidateAllowsPlan)
- Security card: cannot create Plan

### Step 4: Infrastructure tests — EF model contract

Add `ProjectDocument` and `ProjectDocumentVersion` to the EF model contract test. Use `AssertProperties` pattern:

```csharp
var entity = model.FindEntityType(typeof(ProjectDocument))!;
AssertProperties(entity, "Id", "ProjectId", "DocType", "Title", "Description",
    "Content", "Version", "CreatedByUserId", "CreatedAt", "UpdatedAt", "ArchivedAt");
```

### Step 5: Server tests — register new port stubs

`IProjectDocumentRepository` is a new Application-layer port. Register a stub in EVERY test factory's `ConfigureServices`. Follow the "New Application-layer port checklist" in AGENTS.md:

```bash
grep -rl "ConfigureServices" tests/HydraForge.Server.Tests/
```

For each factory found, add:
```csharp
services.AddScoped<IProjectDocumentRepository>(_ => Substitute.For<IProjectDocumentRepository>());
```

Also register `ProjectDocumentService` in test factories.

### Step 6: Run all tests

```bash
dotnet test
```

### Step 7: Commit

```bash
git add tests/
git commit -m "test: add tests for new card doc rules, ProjectDocumentService, EF model"
```

---

## Task 12: Docs — Update data-model.md, board-workflow.md, DECISIONS.md

**Files:**
- Modify: `docs/data-model.md`
- Modify: `docs/board-workflow.md`
- Modify: `docs/DECISIONS.md`

### Step 1: Update `docs/data-model.md`

- Add `Security = 6` to CardType enum table
- Add `ValidationMatrix = 4` to DocType enum table
- Add `ProjectDocType` enum table (Scope/Glossary/DataModel/Architecture/FunctionalSpec/Decisions/Reference)
- Add `ProjectDocument` entity table
- Add `ProjectDocumentVersion` entity table
- Update card-type doc rules section (line 63-67):
  ```
  Card-type doc rules:
  - Goal  → Specs (Specification + ValidationMatrix) — no Plans; child Task cards carry the plans
  - Idea  → Spec (Concept) only — no Plans
  - Issue → Spec (Report) only — no Plans
  - Task  → Plans + ValidationMatrix Spec — no other Spec types
  - Security → Spec (Report) only — no Plans
  ```
- Add ProjectDocument to entity relationship overview

### Step 2: Update `docs/board-workflow.md`

- Add Security card type to the mapping table (line 14): Carter's security audit findings → Security card
- Update Goal card row: "Its Specs (Specification + ValidationMatrix) hold the spec content. Multiple Specs per Goal."
- Update Task card row: "Its Plans hold the plan content. Can also have a ValidationMatrix Spec."
- Add Project Documents section: "Project-level docs (Scope, Glossary, Data Model, Architecture, Functional Spec, Decisions, Reference) live as ProjectDocument entities, not cards. Accessible from the project board's Documents tab."

### Step 3: Update `docs/DECISIONS.md`

Add five new decisions after the last existing entry (D-58):

**D-XX1: Goal-no-Plans**
| Field | Value |
|---|---|
| Topic | Goal cards no longer own Plans |
| Date | 2026-08-05 |
| Status | ✅ Settled |
| Decision | Goal cards cannot have Plans. A Goal's implementation plan is the set of child Task cards, each with their own Plans. Existing Goal-owned Plans must be migrated or deleted. |
| Rationale | Goals are milestones, not implementation units. Plans describe step-by-step implementation — that belongs on the Task cards that actually do the work. This removes the awkward "Goal has Plans grouped under its Spec" indirection. |
| Impact | `Card.ValidateAllowsPlan` rejects Goal. `Plan.SpecId` remains for backward compat but new Plans won't set it. `CardModal.vue` PLAN_CARD_TYPES drops Goal. |

**D-XX2: Security card type**
| Field | Value |
|---|---|
| Topic | New CardType for security findings |
| Date | 2026-08-05 |
| Status | ✅ Settled |
| Decision | `CardType.Security = 6`. Same doc shape as Issue (one Report Spec, no Plans) but distinct type for filtering/reporting. |
| Rationale | Security findings (XSS, authz gaps, secret leaks) are not bugs — they have different severity, triage, and reporting needs. A separate card type makes them filterable and reportable without conflating them with general issues. |
| Impact | New enum value. `Card.ValidateAllowsSpec` accepts Security. `Card.ExpectedSpecDocType` returns Report. Web UI card-type selector gains Security option. |

**D-XX3: ValidationMatrix DocType**
| Field | Value |
|---|---|
| Topic | New DocType for structured validation checklists |
| Date | 2026-08-05 |
| Status | ✅ Settled |
| Decision | `DocType.ValidationMatrix = 4`. A structured checklist of validation steps (unit tests, e2e, manual checks). Allowed on Goal and Task cards. |
| Rationale | Replaces loose `docs/archive/manual-validation/*.md` files. A ValidationMatrix is a Spec (versioned, editable, restorable) but semantically distinct from a Specification/Concept/Report. |
| Impact | New enum value. `Card.IsValidSpecDocType` allows ValidationMatrix on Goal and Task. Web UI CardSpec labels it "Validation Matrix". |

**D-XX4: ProjectDocument entity**
| Field | Value |
|---|---|
| Topic | Project-level versioned documents |
| Date | 2026-08-05 |
| Status | ✅ Settled |
| Decision | New `ProjectDocument` entity with `ProjectDocType` enum (Scope, Glossary, DataModel, Architecture, FunctionalSpec, Decisions, Reference). Versioned via `ProjectDocumentVersion` (mirrors SpecVersion/PlanVersion). Unique-per-project for Scope/Glossary/DataModel/Architecture/FunctionalSpec/Decisions; N allowed for Reference. |
| Rationale | Today these docs live as loose .md files in the repo. They need a home in HydraForge — versioned, editable, linked to the project. The Spec/Plan versioning pattern is proven and directly applicable. |
| Impact | New entities, new controller, new Web UI page, new TUI screen. `docs/*.md` files remain as the initial seed content but the DB becomes the source of truth. |

**D-XX5: Decisions-as-ProjectDocument**
| Field | Value |
|---|---|
| Topic | DECISIONS.md lives as a ProjectDocument |
| Date | 2026-08-05 |
| Status | ✅ Settled |
| Decision | `docs/DECISIONS.md` is stored as a `ProjectDocument` with `DocType.Decisions`. LLM or user appends to it. Not a card on the board — it's a project-level document. |
| Rationale | Decisions are project-scoped, not card-scoped. They're an append-log updated over time. A ProjectDocument with version snapshots is the natural fit — each append creates a new version. |
| Impact | The Decisions doc appears in the project Documents page. LLM can append new D-XX entries programmatically. |

### Step 4: Commit

```bash
git add docs/data-model.md docs/board-workflow.md docs/DECISIONS.md
git commit -m "docs: update data model, board workflow, decisions for card doc rules + project documents"
```

---

## Execution Order

```
Task 1 (Domain) ─────────────────────────────────────────────────────────────┐
     │                                                                        │
Task 2 (Migration) ──────────────────────────────────────────────────────┐    │
     │                                                                    │    │
Task 3 (Application) ───────────────────────────────────────────────┐    │    │
     │                                                               │    │    │
Task 4 (Infrastructure) ────────────────────────────────────────┐   │    │    │
     │                                                          │   │    │    │
Task 5 (Server controller) ─────────────────────────────────┐   │   │    │    │
     │                                                      │   │   │    │    │
     ├── Task 6 (CardCreateModal fix) ── parallel            │   │   │    │    │
     ├── Task 7 (Card type updates) ── parallel              │   │   │    │    │
     ├── Task 8 (Docs tab auto-open) ── parallel             │   │   │    │    │
     │                                                      │   │   │    │    │
Task 9 (Documents page) ── after Task 5 + 8 ────────────────┤   │   │    │    │
Task 10 (TUI) ── after Task 5 ──────────────────────────────┤   │   │    │    │
Task 11 (Tests) ── after Task 3 + 4 + 5 ────────────────────┤   │   │    │    │
Task 12 (Docs) ── after all ────────────────────────────────┘   │   │    │    │
                                                                │   │    │    │
```

**Parallel groups:**
- Tasks 6, 7, 8 can run in parallel after Task 5 (all Web UI, no shared state)
- Task 9 depends on Task 5 (needs API) and Task 8 (shares CardModal context)
- Task 10 can run in parallel with Tasks 6-9 (TUI is independent)
- Task 11 can start after Tasks 3+4+5 (needs services + controllers)
- Task 12 runs last (docs reflect final state)

---

## Verification

After all tasks complete, run the full verification suite:

```bash
# .NET
dotnet build && dotnet test

# EF migration drift
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef migrations has-pending-model-changes \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server

# Web UI
cd src/web-ui && pnpm typecheck && pnpm lint && pnpm build
```
