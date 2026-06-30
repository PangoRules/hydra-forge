# Plan 5a: Doc Model Schema — DocType + PlanStatus + Multi-Plan
**Branch:** `task/phase-3-5a-doc-model-schema`
**Parent branch:** `feat/phase-3-web-ui`
**Parent spec:** N/A — implements D-44 (Doc Model Redesign); plan 5b covers the UI.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Add `DocType` to `Spec`, add `Status` and `Position` to `Plan`, add `SpawnedFrom` to `RelationshipType`, wire EF config + migration, enforce `PlanStatus.Done` read-only in the service layer, expose new fields through DTOs and controllers.

**Architecture:**
- Domain enums are value types in `HydraForge.Domain/Enums/` — no dependencies inward.
- `Spec.DocType` is a discriminator: same entity, UI label varies per card type. The service/controller caller supplies the correct `DocType` at creation time; it is never inferred from the card after the fact.
- `Plan.Status` lifecycle: `Pending → Active → Done`. The service enforces that `Update` and `RestoreVersion` both fail with `PLAN_EDIT_FORBIDDEN_WHEN_DONE`. A new `ReactivateAsync` service method transitions `Done → Active`.
- `Plan.Position` is an `int` assigned by the caller at creation; the service does not auto-compact gaps (keep it simple — re-ordering is a future concern).
- No breaking change to existing API consumers: `DocType` defaults to `Specification` if absent in old rows (migration sets default), `Status` defaults to `Pending`, `Position` defaults to `0`.

**Tech Stack:** .NET 10 / EF Core / PostgreSQL

**Depends on:** Plan 5 (SpecService, PlanService, controllers already exist)

## Global Constraints

- **Result<T, Error> only** — services never throw for expected failures
- **Domain entity methods** — never set entity properties directly from services; use the pattern established in existing entities (direct property set is acceptable for simple value fields without invariants, but add `SetStatus` / `Reactivate` methods to enforce the lifecycle)
- **xUnit plain assertions** — `Assert.*` only, no FluentAssertions
- **No pending model changes** — verify with `dotnet ef migrations has-pending-model-changes` after migration
- **Error codes** — new codes go in `DomainErrorCodes.Plans`
- **`dotnet-verification` skill** — run build + test + EF drift check after each task

---

## Task 1: Domain Enums and Entity Fields

**Files:**
- Create: `src/HydraForge.Domain/Enums/DocType.cs`
- Create: `src/HydraForge.Domain/Enums/PlanStatus.cs`
- Modify: `src/HydraForge.Domain/Enums/RelationshipType.cs`
- Modify: `src/HydraForge.Domain/Entities/ProjectSpace/Spec.cs`
- Modify: `src/HydraForge.Domain/Entities/ProjectSpace/Plan.cs`

- [x] **Step 1: Create DocType enum**

Create `src/HydraForge.Domain/Enums/DocType.cs`:

```csharp
namespace HydraForge.Domain.Enums;

public enum DocType
{
    Specification = 1,
    Concept = 2,
    Report = 3
}
```

- [x] **Step 2: Create PlanStatus enum**

Create `src/HydraForge.Domain/Enums/PlanStatus.cs`:

```csharp
namespace HydraForge.Domain.Enums;

public enum PlanStatus
{
    Pending = 1,
    Active = 2,
    Done = 3
}
```

- [x] **Step 3: Add SpawnedFrom to RelationshipType**

Open `src/HydraForge.Domain/Enums/RelationshipType.cs`. Current content:

```csharp
namespace HydraForge.Domain.Enums;

public enum RelationshipType
{
    BlockedBy = 1,
    Precedes = 2,
    Relates = 3
}
```

Add `SpawnedFrom`:

```csharp
namespace HydraForge.Domain.Enums;

public enum RelationshipType
{
    BlockedBy = 1,
    Precedes = 2,
    Relates = 3,
    SpawnedFrom = 4
}
```

- [x] **Step 4: Add DocType to Spec entity**

Open `src/HydraForge.Domain/Entities/ProjectSpace/Spec.cs`. Add `DocType` property and lifecycle methods:

```csharp
using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.ProjectSpace;

public class Spec
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid CardId { get; set; }
    public DocType DocType { get; set; } = DocType.Specification;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [x] **Step 5: Add Status and Position to Plan entity + lifecycle methods**

Open `src/HydraForge.Domain/Entities/ProjectSpace/Plan.cs`. Add `Status`, `Position`, and lifecycle methods:

```csharp
using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.ProjectSpace;

public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid CardId { get; set; }
    public Guid? SpecId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public PlanStatus Status { get; set; } = PlanStatus.Pending;
    public int Position { get; set; } = 0;
    public int Version { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDone => Status == PlanStatus.Done;

    public void Activate() => Status = PlanStatus.Active;

    public void Complete() => Status = PlanStatus.Done;

    public void Reactivate() => Status = PlanStatus.Active;
}
```

- [x] **Step 6: Build — zero errors**

```bash
dotnet build
```

Expected: Build succeeded, 0 errors.

- [x] **Step 7: Commit**

```bash
git add src/HydraForge.Domain/Enums/DocType.cs \
        src/HydraForge.Domain/Enums/PlanStatus.cs \
        src/HydraForge.Domain/Enums/RelationshipType.cs \
        src/HydraForge.Domain/Entities/ProjectSpace/Spec.cs \
        src/HydraForge.Domain/Entities/ProjectSpace/Plan.cs
git commit -m "feat(domain): DocType + PlanStatus enums, Spec.DocType, Plan.Status/Position + SpawnedFrom"
```

---

## Task 2: EF Core Configuration and Migration

**Files:**
- Modify: `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`
- Auto-generated: migration files under `src/HydraForge.Infrastructure/Migrations/`
- Modify: test files (model contract assertions)

- [x] **Step 1: Update DbContext — Spec config**

Open `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`.

Find the `ConfigureEntity<Spec>` block (currently lines 122–128). Replace with:

```csharp
ConfigureEntity<Spec>(modelBuilder, "specs", b =>
{
    b.HasIndex(e => e.ProjectId);
    b.Property(e => e.CardId).HasColumnName("card_id").IsRequired();
    b.Property(e => e.DocType)
        .HasColumnName("doc_type")
        .HasConversion<int>()
        .HasDefaultValue(DocType.Specification)
        .IsRequired();
    b.HasOne<Card>().WithMany().HasForeignKey(e => e.CardId).OnDelete(DeleteBehavior.Cascade);
    b.HasIndex(e => e.CardId).HasDatabaseName("ix_specs_card_id");
});
```

Add `using HydraForge.Domain.Enums;` at the top of the file if not already present.

- [x] **Step 2: Update DbContext — Plan config**

Find the `ConfigureEntity<Plan>` block (currently lines 140–149). Replace with:

```csharp
ConfigureEntity<Plan>(modelBuilder, "plans", b =>
{
    b.HasIndex(e => e.ProjectId);
    b.Property(e => e.CardId).HasColumnName("card_id").IsRequired();
    b.Property(e => e.SpecId).HasColumnName("spec_id");
    b.Property(e => e.Status)
        .HasColumnName("status")
        .HasConversion<int>()
        .HasDefaultValue(PlanStatus.Pending)
        .IsRequired();
    b.Property(e => e.Position)
        .HasColumnName("position")
        .HasDefaultValue(0)
        .IsRequired();
    b.HasOne<Card>().WithMany().HasForeignKey(e => e.CardId).OnDelete(DeleteBehavior.Cascade);
    b.HasOne<Spec>().WithMany().HasForeignKey(e => e.SpecId).OnDelete(DeleteBehavior.SetNull);
    b.HasIndex(e => e.CardId).HasDatabaseName("ix_plans_card_id");
    b.HasIndex(e => e.SpecId).HasDatabaseName("ix_plans_spec_id");
});
```

- [x] **Step 3: Add EF migration**

```bash
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef migrations add AddDocTypeAndPlanStatus \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server
```

Open the generated migration file and verify it contains:
- `AddColumn` for `doc_type` on `specs` table (default 1)
- `AddColumn` for `status` on `plans` table (default 1)
- `AddColumn` for `position` on `plans` table (default 0)

- [x] **Step 4: Verify no pending model changes**

```bash
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef migrations has-pending-model-changes \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server
```

Expected: `No pending model changes detected.` (exit 0)

- [x] **Step 5: Run build + tests**

```bash
dotnet build && dotnet test
```

Expected: Build succeeded, all tests pass.

- [x] **Step 6: Commit**

```bash
git add src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs \
        src/HydraForge.Infrastructure/Migrations/
git commit -m "feat(infra): EF config + migration for DocType, PlanStatus, Plan.Position"
```

---

## Task 3: Application Layer — DTOs, Error Codes, Services

**Files:**
- Modify: `src/HydraForge.Domain/Common/DomainErrorCodes.cs`
- Modify: `src/HydraForge.Application/Specs/SpecModels.cs`
- Modify: `src/HydraForge.Application/Plans/PlanModels.cs`
- Modify: `src/HydraForge.Application/Specs/SpecService.cs`
- Modify: `src/HydraForge.Application/Plans/PlanService.cs`
- Create or modify: test files for service layer

- [x] **Step 1: Add PLAN_EDIT_FORBIDDEN_WHEN_DONE error code**

Open `src/HydraForge.Domain/Common/DomainErrorCodes.cs`. Find the `Plans` class and add:

```csharp
public static class Plans
{
    public const string NotFound = "PLAN_NOT_FOUND";
    public const string DocumentVersionNotFound = "DOCUMENT_VERSION_NOT_FOUND";
    public const string MarkdownPayloadTooLarge = "MARKDOWN_PAYLOAD_TOO_LARGE";
    public const string CardDocumentProjectMismatch = "CARD_DOCUMENT_PROJECT_MISMATCH";
    public const string EditForbiddenWhenDone = "PLAN_EDIT_FORBIDDEN_WHEN_DONE";
}
```

- [x] **Step 2: Update SpecModels.cs — add DocType everywhere**

Open `src/HydraForge.Application/Specs/SpecModels.cs`. Replace the full file:

```csharp
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Specs;

public record CreateSpecCommand(
    Guid ProjectId,
    Guid CardId,
    Guid ActorId,
    DocType DocType,
    string Title,
    string? Description,
    string Content
);

public record UpdateSpecCommand(
    Guid ProjectId,
    Guid SpecId,
    Guid ActorId,
    string Title,
    string? Description,
    string Content
);

public record RestoreSpecVersionCommand(
    Guid ProjectId,
    Guid SpecId,
    int Version,
    Guid ActorId
);

public record SpecDto(
    Guid Id,
    Guid ProjectId,
    Guid CardId,
    DocType DocType,
    string Title,
    string? Description,
    string Content,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SpecVersionDto(
    Guid Id,
    Guid SpecId,
    int Version,
    string Title,
    string? Description,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

public record SpecListFilter(bool IncludeArchived = false);

public record CreateSpecRequest(
    DocType DocType,
    string Title,
    string? Description,
    string Content
);

public record UpdateSpecRequest(
    string Title,
    string? Description,
    string Content
);

public record RestoreSpecVersionRequest(
    int Version
);

public record SpecResponse(
    Guid Id,
    Guid ProjectId,
    Guid CardId,
    DocType DocType,
    string Title,
    string? Description,
    string Content,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SpecVersionResponse(
    Guid Id,
    Guid SpecId,
    int Version,
    string Title,
    string? Description,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

public record SpecListResponse(IReadOnlyList<SpecResponse> Specs);

public record SpecVersionListResponse(IReadOnlyList<SpecVersionResponse> Versions);
```

- [x] **Step 3: Update PlanModels.cs — add Status, Position, ReactivatePlanCommand**

Open `src/HydraForge.Application/Plans/PlanModels.cs`. Replace the full file:

```csharp
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Plans;

public record CreatePlanCommand(
    Guid ProjectId,
    Guid CardId,
    Guid? SpecId,
    Guid ActorId,
    string Title,
    string? Description,
    string Content,
    int Position = 0
);

public record UpdatePlanCommand(
    Guid ProjectId,
    Guid PlanId,
    Guid ActorId,
    string Title,
    string? Description,
    string Content
);

public record ActivatePlanCommand(
    Guid ProjectId,
    Guid PlanId,
    Guid ActorId
);

public record CompletePlanCommand(
    Guid ProjectId,
    Guid PlanId,
    Guid ActorId
);

public record ReactivatePlanCommand(
    Guid ProjectId,
    Guid PlanId,
    Guid ActorId
);

public record RestorePlanVersionCommand(
    Guid ProjectId,
    Guid PlanId,
    int Version,
    Guid ActorId
);

public record PlanDto(
    Guid Id,
    Guid ProjectId,
    Guid CardId,
    Guid? SpecId,
    string Title,
    string? Description,
    string Content,
    PlanStatus Status,
    int Position,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record PlanVersionDto(
    Guid Id,
    Guid PlanId,
    int Version,
    string Title,
    string? Description,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

public record PlanListFilter(bool IncludeArchived = false);

public record CreatePlanRequest(
    string Title,
    string? Description,
    string Content,
    Guid? SpecId = null,
    int Position = 0
);

public record UpdatePlanRequest(
    string Title,
    string? Description,
    string Content
);

public record RestorePlanVersionRequest(
    int Version
);

public record PlanResponse(
    Guid Id,
    Guid ProjectId,
    Guid CardId,
    Guid? SpecId,
    string Title,
    string? Description,
    string Content,
    PlanStatus Status,
    int Position,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record PlanVersionResponse(
    Guid Id,
    Guid PlanId,
    int Version,
    string Title,
    string? Description,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

public record PlanListResponse(IReadOnlyList<PlanResponse> Plans);

public record PlanVersionListResponse(IReadOnlyList<PlanVersionResponse> Versions);
```

- [x] **Step 4: Update SpecService — pass DocType on create + MapToDto**

Open `src/HydraForge.Application/Specs/SpecService.cs`.

In `CreateAsync`, set `DocType` on the new entity:

```csharp
var spec = new Spec
{
    Id = Guid.NewGuid(),
    ProjectId = cmd.ProjectId,
    CardId = cmd.CardId,
    DocType = cmd.DocType,
    Title = cmd.Title,
    Description = cmd.Description,
    Content = cmd.Content,
    Version = 1,
    CreatedByUserId = cmd.ActorId,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow,
};
```

Update `MapToDto`:

```csharp
private static SpecDto MapToDto(Spec spec) =>
    new(
        spec.Id,
        spec.ProjectId,
        spec.CardId,
        spec.DocType,
        spec.Title,
        spec.Description,
        spec.Content,
        spec.Version,
        spec.CreatedByUserId,
        spec.CreatedAt,
        spec.UpdatedAt
    );
```

- [x] **Step 5: Update PlanService — Status/Position on create, Done guard on update, lifecycle methods**

Open `src/HydraForge.Application/Plans/PlanService.cs`.

In `CreateAsync`, set `Status` and `Position`:

```csharp
var plan = new Plan
{
    Id = Guid.NewGuid(),
    ProjectId = cmd.ProjectId,
    CardId = cmd.CardId,
    SpecId = cmd.SpecId,
    Title = cmd.Title,
    Description = cmd.Description,
    Content = cmd.Content,
    Status = PlanStatus.Pending,
    Position = cmd.Position,
    Version = 1,
    CreatedByUserId = cmd.ActorId,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow,
};
```

In `UpdateAsync`, add Done guard immediately after the plan null-check:

```csharp
if (plan.IsDone)
    return Result<PlanDto>.Failure(
        new Error(DomainErrorCodes.Plans.EditForbiddenWhenDone, "Done plans are read-only. Reactivate before editing.")
    );
```

In `RestoreVersionAsync`, add the same guard after the plan null-check:

```csharp
if (plan.IsDone)
    return Result<PlanDto>.Failure(
        new Error(DomainErrorCodes.Plans.EditForbiddenWhenDone, "Done plans are read-only. Reactivate before editing.")
    );
```

Add three new public methods before `PublishAsync`:

```csharp
public async Task<Result<PlanDto>> ActivateAsync(
    ActivatePlanCommand cmd,
    CancellationToken ct = default
)
{
    var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
    if (membership == null)
        return Result<PlanDto>.Failure(new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied."));

    var plan = await _planRepo.GetByIdAsync(cmd.PlanId, ct);
    if (plan == null || plan.ProjectId != cmd.ProjectId)
        return Result<PlanDto>.Failure(new Error(DomainErrorCodes.Plans.NotFound, "Plan not found."));

    plan.Activate();
    plan.UpdatedAt = DateTime.UtcNow;

    await _planRepo.UpdateAsync(plan, ct);
    await _planRepo.SaveChangesAsync(ct);
    await PublishAsync(cmd.ProjectId, plan.Id, BoardAction.Updated, ct);

    return Result<PlanDto>.Success(MapToDto(plan));
}

public async Task<Result<PlanDto>> CompleteAsync(
    CompletePlanCommand cmd,
    CancellationToken ct = default
)
{
    var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
    if (membership == null)
        return Result<PlanDto>.Failure(new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied."));

    var plan = await _planRepo.GetByIdAsync(cmd.PlanId, ct);
    if (plan == null || plan.ProjectId != cmd.ProjectId)
        return Result<PlanDto>.Failure(new Error(DomainErrorCodes.Plans.NotFound, "Plan not found."));

    plan.Complete();
    plan.UpdatedAt = DateTime.UtcNow;

    await _planRepo.UpdateAsync(plan, ct);
    await _planRepo.SaveChangesAsync(ct);
    await PublishAsync(cmd.ProjectId, plan.Id, BoardAction.Updated, ct);

    return Result<PlanDto>.Success(MapToDto(plan));
}

public async Task<Result<PlanDto>> ReactivateAsync(
    ReactivatePlanCommand cmd,
    CancellationToken ct = default
)
{
    var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
    if (membership == null)
        return Result<PlanDto>.Failure(new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied."));

    var plan = await _planRepo.GetByIdAsync(cmd.PlanId, ct);
    if (plan == null || plan.ProjectId != cmd.ProjectId)
        return Result<PlanDto>.Failure(new Error(DomainErrorCodes.Plans.NotFound, "Plan not found."));

    plan.Reactivate();
    plan.UpdatedAt = DateTime.UtcNow;

    await _planRepo.UpdateAsync(plan, ct);
    await _planRepo.SaveChangesAsync(ct);
    await PublishAsync(cmd.ProjectId, plan.Id, BoardAction.Updated, ct);

    return Result<PlanDto>.Success(MapToDto(plan));
}
```

Update `MapToDto`:

```csharp
private static PlanDto MapToDto(Plan plan) =>
    new(
        plan.Id,
        plan.ProjectId,
        plan.CardId,
        plan.SpecId,
        plan.Title,
        plan.Description,
        plan.Content,
        plan.Status,
        plan.Position,
        plan.Version,
        plan.CreatedByUserId,
        plan.CreatedAt,
        plan.UpdatedAt
    );
```

- [x] **Step 6: Write failing service tests**

Find or create the test file for PlanService (check `tests/HydraForge.Application.Tests/Plans/PlanServiceTests.cs` or equivalent path).

Add these tests:

```csharp
[Fact]
public async Task UpdateAsync_WhenPlanIsDone_ReturnsEditForbiddenError()
{
    var plan = new Plan { Status = PlanStatus.Done, ProjectId = _projectId, CardId = _cardId };
    // arrange: stub repo to return this plan, member check passes
    // act
    var result = await _sut.UpdateAsync(new UpdatePlanCommand(_projectId, plan.Id, _actorId, "New Title", null, "content"));
    // assert
    Assert.False(result.IsSuccess);
    Assert.Equal(DomainErrorCodes.Plans.EditForbiddenWhenDone, result.Error.Code);
}

[Fact]
public async Task RestoreVersionAsync_WhenPlanIsDone_ReturnsEditForbiddenError()
{
    var plan = new Plan { Status = PlanStatus.Done, ProjectId = _projectId, CardId = _cardId };
    // arrange same as above
    var result = await _sut.RestoreVersionAsync(new RestorePlanVersionCommand(_projectId, plan.Id, 1, _actorId));
    Assert.False(result.IsSuccess);
    Assert.Equal(DomainErrorCodes.Plans.EditForbiddenWhenDone, result.Error.Code);
}

[Fact]
public async Task ReactivateAsync_WhenPlanIsDone_TransitionsToActive()
{
    var plan = new Plan { Status = PlanStatus.Done, ProjectId = _projectId, CardId = _cardId };
    // arrange
    var result = await _sut.ReactivateAsync(new ReactivatePlanCommand(_projectId, plan.Id, _actorId));
    Assert.True(result.IsSuccess);
    Assert.Equal(PlanStatus.Active, result.Value.Status);
}

[Fact]
public async Task CreateAsync_SetsStatusToPending()
{
    var result = await _sut.CreateAsync(new CreatePlanCommand(_projectId, _cardId, null, _actorId, "T", null, "C"));
    Assert.True(result.IsSuccess);
    Assert.Equal(PlanStatus.Pending, result.Value.Status);
}
```

- [x] **Step 7: Run tests (expect new tests to fail — stubs not yet wired)**

```bash
dotnet build && dotnet test
```

Fix any compilation errors from the DTO/service changes. New service tests will fail if stubs aren't in place — wire them using the existing test patterns in the test project.

- [x] **Step 8: Commit**

```bash
git add src/HydraForge.Domain/Common/DomainErrorCodes.cs \
        src/HydraForge.Application/Specs/SpecModels.cs \
        src/HydraForge.Application/Plans/PlanModels.cs \
        src/HydraForge.Application/Specs/SpecService.cs \
        src/HydraForge.Application/Plans/PlanService.cs \
        tests/
git commit -m "feat(app): DocType in SpecService, PlanStatus lifecycle + Done guard in PlanService"
```

---

## Task 4: API Controllers

**Files:**
- Modify: `src/HydraForge.Server/Controllers/Projects/SpecsController.cs`
- Modify: `src/HydraForge.Server/Controllers/Projects/PlansController.cs`
- Modify: `src/web-ui/app/lib/routes.ts` (add reactivate/activate/complete endpoints)

- [x] **Step 1: Read the current SpecsController**

Read `src/HydraForge.Server/Controllers/Projects/SpecsController.cs` in full before editing.

- [x] **Step 2: Update SpecsController — pass DocType from request to command**

In the `POST` (create) action, pass `request.DocType` to `CreateSpecCommand`:

```csharp
var cmd = new CreateSpecCommand(
    projectId,
    cardId,
    actorId,
    request.DocType,
    request.Title,
    request.Description,
    request.Content
);
```

In the response mapping from `SpecDto` → `SpecResponse`, include `DocType`:

```csharp
new SpecResponse(
    dto.Id,
    dto.ProjectId,
    dto.CardId,
    dto.DocType,
    dto.Title,
    dto.Description,
    dto.Content,
    dto.Version,
    dto.CreatedByUserId,
    dto.CreatedAt,
    dto.UpdatedAt
)
```

- [x] **Step 3: Read the current PlansController**

Read `src/HydraForge.Server/Controllers/Projects/PlansController.cs` in full before editing.

- [x] **Step 4: Update PlansController — Status, Position in responses + new lifecycle endpoints**

In the create action, pass `Position` and `SpecId` from request:

```csharp
var cmd = new CreatePlanCommand(
    projectId,
    cardId,
    request.SpecId,
    actorId,
    request.Title,
    request.Description,
    request.Content,
    request.Position
);
```

Update all response mappings to include `SpecId`, `Status`, `Position`:

```csharp
new PlanResponse(
    dto.Id,
    dto.ProjectId,
    dto.CardId,
    dto.SpecId,
    dto.Title,
    dto.Description,
    dto.Content,
    dto.Status,
    dto.Position,
    dto.Version,
    dto.CreatedByUserId,
    dto.CreatedAt,
    dto.UpdatedAt
)
```

Add three new action methods:

```csharp
[HttpPost("{planId:guid}/activate")]
public async Task<IActionResult> Activate(Guid projectId, Guid planId, CancellationToken ct)
{
    var actorId = User.GetUserId();
    var result = await _planService.ActivateAsync(new ActivatePlanCommand(projectId, planId, actorId), ct);
    if (!result.IsSuccess)
        return result.Error.ToProblemDetails();
    return Ok(MapToResponse(result.Value));
}

[HttpPost("{planId:guid}/complete")]
public async Task<IActionResult> Complete(Guid projectId, Guid planId, CancellationToken ct)
{
    var actorId = User.GetUserId();
    var result = await _planService.CompleteAsync(new CompletePlanCommand(projectId, planId, actorId), ct);
    if (!result.IsSuccess)
        return result.Error.ToProblemDetails();
    return Ok(MapToResponse(result.Value));
}

[HttpPost("{planId:guid}/reactivate")]
public async Task<IActionResult> Reactivate(Guid projectId, Guid planId, CancellationToken ct)
{
    var actorId = User.GetUserId();
    var result = await _planService.ReactivateAsync(new ReactivatePlanCommand(projectId, planId, actorId), ct);
    if (!result.IsSuccess)
        return result.Error.ToProblemDetails();
    return Ok(MapToResponse(result.Value));
}
```

Where `MapToResponse` extracts the existing inline mapping into a private static helper (refactor as part of this step if it isn't already).

- [x] **Step 5: Add new plan endpoints to ApiRoutes in routes.ts**

Open `src/web-ui/app/lib/routes.ts`. In the `Plans` block add:

```ts
Plans: {
  // existing entries ...
  activate: (projectId: string, planId: string) =>
    `/api/projects/${projectId}/Plans/${planId}/activate` as const,
  complete: (projectId: string, planId: string) =>
    `/api/projects/${projectId}/Plans/${planId}/complete` as const,
  reactivate: (projectId: string, planId: string) =>
    `/api/projects/${projectId}/Plans/${planId}/reactivate` as const,
},
```

- [x] **Step 6: Build + tests + EF drift check**

```bash
dotnet build && dotnet test
PATH="$PATH:/home/pango/.dotnet/tools" \
  dotnet ef migrations has-pending-model-changes \
    --project src/HydraForge.Infrastructure \
    --startup-project src/HydraForge.Server
```

Expected: zero build errors, all tests pass, no pending model changes.

Also run:

```bash
cd src/web-ui && pnpm typecheck && pnpm lint
```

Expected: zero errors.

- [x] **Step 7: Verify via Scalar UI**

Start the dev server (`dotnet run --project src/HydraForge.Server`) and open `http://localhost:5000/scalar/v1`. Confirm:
- `POST /api/projects/{projectId}/cards/{cardId}/Plans` request body includes `specId`, `position`.
- `GET /api/projects/{projectId}/Plans/{planId}` response includes `status`, `position`, `specId`.
- `POST .../Plans/{planId}/activate`, `.../complete`, `.../reactivate` are listed.
- `POST /api/projects/{projectId}/cards/{cardId}/Specs` request body includes `docType`.

- [x] **Step 8: Commit**

```bash
git add src/HydraForge.Server/Controllers/Projects/SpecsController.cs \
        src/HydraForge.Server/Controllers/Projects/PlansController.cs \
        src/web-ui/app/lib/routes.ts
git commit -m "feat(api): expose DocType on Spec, Status/Position on Plan, add activate/complete/reactivate endpoints"
```
