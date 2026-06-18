# Spec/Plan Ownership Redesign Implementation Plan

> **For agentic workers:** Execute this plan task-by-task. Steps use checkbox syntax.

**Goal:** Replace `Card.SpecId`/`Card.PlanId` (1-to-1) with `Spec.CardId`/`Plan.CardId` (1-to-many ownership FKs), remove link/unlink endpoints, add card-scoped create routes.

**Architecture:** Domain entities → Application services/commands → Infrastructure repos → Server controllers. Remove FK from Card, add FK on Spec/Plan, cascade ownership through all layers.

**Tech Stack:** .NET 10, EF Core 10, Npgsql, xUnit, ASP.NET Core

---

### Task 1: Domain entity changes

**Files:**
- Modify: `src/HydraForge.Domain/Entities/ProjectSpace/Spec.cs`
- Modify: `src/HydraForge.Domain/Entities/ProjectSpace/Plan.cs`
- Modify: `src/HydraForge.Domain/Entities/ProjectSpace/Card.cs`

- [ ] **Update Spec.cs — add CardId**

```csharp
public class Spec
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid CardId { get; set; }  // ← NEW: owning card
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Update Plan.cs — add CardId + SpecId**

```csharp
public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid CardId { get; set; }  // ← NEW: owning card
    public Guid? SpecId { get; set; }  // ← NEW: parent spec
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Update Card.cs — remove SpecId + PlanId**

```csharp
public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid ColumnId { get; set; }
    public Guid? ParentCardId { get; set; }
    // REMOVE: public Guid? SpecId { get; set; }
    // REMOVE: public Guid? PlanId { get; set; }
    public int CardNumber { get; set; }
    // ... rest stays the same
}
```

- [ ] **Build and verify**

Run: `dotnet build`  
Expected: Build succeeds (0 errors)

- [ ] **Commit**

```bash
git add src/HydraForge.Domain/Entities/ProjectSpace/
git commit -m "refactor: add Spec.CardId, Plan.CardId/Plan.SpecId, remove Card.SpecId/PlanId"
```

---

### Task 2: Update EF configuration + migration

**Files:**
- Modify: `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`

- [ ] **Read current HydraForgeDbContext.cs** — find Spec and Plan entity config, and any references to Card.SpecId/PlanId

- [ ] **Update entity configuration for Spec** — add CardId FK config

In the Spec entity configuration section, add:
```csharp
// Inside Spec entity config
entity.Property(s => s.CardId).HasColumnName("card_id").IsRequired();
entity.HasOne<Card>().WithMany().HasForeignKey(s => s.CardId).OnDelete(DeleteBehavior.Cascade);
entity.HasIndex(s => s.CardId).HasDatabaseName("ix_specs_card_id");
```

- [ ] **Update entity configuration for Plan** — add CardId + SpecId FK config

```csharp
// Inside Plan entity config
entity.Property(p => p.CardId).HasColumnName("card_id").IsRequired();
entity.Property(p => p.SpecId).HasColumnName("spec_id");
entity.HasOne<Card>().WithMany().HasForeignKey(p => p.CardId).OnDelete(DeleteBehavior.Cascade);
entity.HasOne<Spec>().WithMany().HasForeignKey(p => p.SpecId).OnDelete(DeleteBehavior.SetNull);
entity.HasIndex(p => p.CardId).HasDatabaseName("ix_plans_card_id");
entity.HasIndex(p => p.SpecId).HasDatabaseName("ix_plans_spec_id");
```

- [ ] **Remove Card.SpecId and Card.PlanId config** from Card entity configuration

Find any existing config for `Card.SpecId` and `Card.PlanId` and remove it.

- [ ] **Run migration**

```bash
PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations add SpecPlanOwnership --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server
```

Expected: Migration created successfully.

- [ ] **Build and verify**

```bash
dotnet build
```

Expected: 0 errors

- [ ] **Commit**

```bash
git add src/HydraForge.Infrastructure/ && git commit -m "feat: add EF config for Spec.CardId, Plan.CardId/Plan.SpecId; add migration"
```

---

### Task 3: Update repositories

**Files:**
- Modify: `src/HydraForge.Application/Specs/ISpecRepository.cs`
- Modify: `src/HydraForge.Application/Plans/IPlanRepository.cs`
- Modify: `src/HydraForge.Infrastructure/Specs/EfSpecRepository.cs`
- Modify: `src/HydraForge.Infrastructure/Plans/EfPlanRepository.cs`

- [ ] **Update ISpecRepository** — add `ListByCardAsync`, remove methods that reference Card FKs

```csharp
// Add:
Task<IReadOnlyList<Spec>> ListByCardAsync(Guid cardId, SpecListFilter filter, CancellationToken ct = default);

// Already removed in prior refactors:
// GetCardByIdAsync — gone
// GetLinkedCardIdAsync — gone
// GetLinkedCardIdsAsync — gone
```

- [ ] **Update IPlanRepository** — add `ListByCardAsync`, remove methods that reference Card FKs

```csharp
// Add:
Task<IReadOnlyList<Plan>> ListByCardAsync(Guid cardId, PlanListFilter filter, CancellationToken ct = default);
```

- [ ] **Update EfSpecRepository** — implement `ListByCardAsync`

```csharp
public async Task<IReadOnlyList<Spec>> ListByCardAsync(Guid cardId, SpecListFilter filter, CancellationToken ct = default)
{
    var query = _context.Specs.Where(s => s.CardId == cardId);
    return await query.OrderBy(s => s.CreatedAt).ToListAsync(ct);
}
```

- [ ] **Update EfPlanRepository** — implement `ListByCardAsync`

```csharp
public async Task<IReadOnlyList<Plan>> ListByCardAsync(Guid cardId, PlanListFilter filter, CancellationToken ct = default)
{
    var query = _context.Plans.Where(p => p.CardId == cardId);
    return await query.OrderBy(p => p.CreatedAt).ToListAsync(ct);
}
```

- [ ] **Build and verify**

```bash
dotnet build
```

Expected: 0 errors

- [ ] **Commit**

```bash
git add src/HydraForge.Application/Specs/ISpecRepository.cs src/HydraForge.Application/Plans/IPlanRepository.cs src/HydraForge.Infrastructure/Specs/EfSpecRepository.cs src/HydraForge.Infrastructure/Plans/EfPlanRepository.cs && git commit -m "feat: add ListByCardAsync to spec/plan repos"
```

---

### Task 4: Update service layer

**Files:**
- Modify: `src/HydraForge.Application/Specs/SpecService.cs`
- Modify: `src/HydraForge.Application/Plans/PlanService.cs`
- Modify: `src/HydraForge.Application/Specs/SpecModels.cs`
- Modify: `src/HydraForge.Application/Plans/PlanModels.cs`

- [ ] **Read current SpecService.cs** — understand CreateAsync, LinkToCardAsync, UnlinkFromCardAsync

- [ ] **Update SpecModels.cs commands**

```csharp
// Replace CreateSpecCommand — add CardId
public record CreateSpecCommand(
    Guid ProjectId,
    Guid CardId,       // ← NEW: owning card
    Guid ActorId,
    string Title,
    string? Description,
    string Content
);

// Remove LinkSpecToCardCommand (no longer needed)
// Remove UnlinkSpecFromCardCommand (no longer needed)

// Update RestoreSpecVersionCommand — no change needed
```

- [ ] **Update PlanModels.cs commands**

```csharp
// Replace CreatePlanCommand — add CardId, optional SpecId
public record CreatePlanCommand(
    Guid ProjectId,
    Guid CardId,       // ← NEW: owning card
    Guid? SpecId,      // ← NEW: optional parent spec
    Guid ActorId,
    string Title,
    string? Description,
    string Content
);

// Remove LinkPlanToCardCommand (no longer needed)
// Remove UnlinkPlanFromCardCommand (no longer needed)
```

- [ ] **Update SpecService.cs**

Changes:
- `CreateAsync` — accept `CreateSpecCommand` with CardId, set `spec.CardId = cmd.CardId`
- Remove `LinkToCardAsync` entirely
- Remove `UnlinkFromCardAsync` entirely
- Remove `GetLinkedCardIdAsync` calls in GetByIdAsync, UpdateAsync, RestoreVersionAsync (use `spec.CardId` directly or just drop `linkedCardId` from DTO)
- `MapToDto` — remove `linkedCardId` parameter

```csharp
// CreateAsync changes:
var spec = new Spec
{
    Id = Guid.NewGuid(),
    ProjectId = cmd.ProjectId,
    CardId = cmd.CardId,  // ← NEW
    Title = cmd.Title,
    // ...
};
// No link/unlink operations — ownership replaces link concept

// GetByIdAsync, UpdateAsync, RestoreVersionAsync — remove linkedCardId lookup
// MapToDto — remove linkedCardId parameter
private static SpecDto MapToDto(Spec spec) =>
    new(spec.Id, spec.ProjectId, spec.Title, spec.Description, spec.Content,
        spec.Version, spec.CreatedByUserId, spec.CreatedAt, spec.UpdatedAt);
```

- [ ] **Update PlanService.cs** — same pattern as SpecService

- [ ] **Remove LinkSpecToCardRequest and UnlinkSpecFromCardRequest from request models** if they exist

- [ ] **Remove LinkPlanToCardRequest and UnlinkPlanFromCardRequest from request models** if they exist

- [ ] **Build and verify**

```bash
dotnet build
```

Expected: 0 errors

- [ ] **Commit**

```bash
git add src/HydraForge.Application/ && git commit -m "feat: update spec/plan services — ownership replaces link/unlink"
```

---

### Task 5: Update controllers

**Files:**
- Modify: `src/HydraForge.Server/Controllers/Projects/SpecsController.cs`
- Modify: `src/HydraForge.Server/Controllers/Projects/PlansController.cs`

- [ ] **Read current SpecsController.cs**

- [ ] **Rewrite SpecsController.cs** — card-scoped create/list, remove link/unlink

```csharp
[Route("api/projects/{projectId:guid}/cards/{cardId:guid}/specs")]
public class SpecsController(SpecService specService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, Guid cardId, [FromBody] CreateSpecRequest request)
    {
        var userId = User.GetRequiredUserId();
        var cmd = new CreateSpecCommand(projectId, cardId, userId, request.Title, request.Description, request.Content);
        var result = await specService.CreateAsync(cmd);
        if (result.IsFailure) return this.ToProblemResult(result.Error);
        return CreatedAtAction(nameof(GetById), new { projectId, specId = result.Value.Id }, MapResponse(result.Value));
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, Guid cardId)
    {
        var userId = User.GetRequiredUserId();
        var result = await specService.ListByCardAsync(projectId, cardId, new SpecListFilter(), userId);
        if (result.IsFailure) return this.ToProblemResult(result.Error);
        return Ok(new SpecListResponse(result.Value.Select(MapResponse).ToList()));
    }

    [HttpGet("~/api/projects/{projectId:guid}/specs/{specId:guid}")]
    public async Task<IActionResult> GetById(Guid projectId, Guid specId) { ... }

    [HttpPut("~/api/projects/{projectId:guid}/specs/{specId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid specId, [FromBody] UpdateSpecRequest request) { ... }

    [HttpGet("~/api/projects/{projectId:guid}/specs/{specId:guid}/versions")]
    public async Task<IActionResult> ListVersions(Guid projectId, Guid specId) { ... }

    [HttpPost("~/api/projects/{projectId:guid}/specs/{specId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid projectId, Guid specId, [FromBody] RestoreSpecVersionRequest request) { ... }
}
```

Note: Routes with `~/` use absolute routing to override the controller-level prefix. Create/list are card-scoped, get/update/versions/restore are spec-scoped via absolute routes.

- [ ] **Rewrite PlansController.cs** — same pattern

- [ ] **Remove old link/unlink request models** from SpecModels.cs and PlanModels.cs

- [ ] **Build and verify**

```bash
dotnet build
```

Expected: 0 errors

- [ ] **Commit**

```bash
git add src/HydraForge.Server/Controllers/Projects/ && git commit -m "feat: update spec/plan controllers — card-scoped create/list, remove link/unlink"
```

---

### Task 6: Remove link/unlink from ProblemDetailsMapper and DomainErrorCodes

**Files:**
- Modify: `src/HydraForge.Server/Errors/ProblemDetailsMapper.cs`
- Modify: `src/HydraForge.Domain/Common/DomainErrorCodes.cs`

- [ ] **Read DomainErrorCodes.cs** — find CARD_DOCUMENT_PROJECT_MISMATCH

- [ ] **Remove link-related error codes** — `CARD_DOCUMENT_PROJECT_MISMATCH` is no longer needed (ownership replaces cross-referencing)

```csharp
// Remove from Specs section:
// public const string CardDocumentProjectMismatch = "CARD_DOCUMENT_PROJECT_MISMATCH";
// Remove from Plans section:
// public const string CardDocumentProjectMismatch = "CARD_DOCUMENT_PROJECT_MISMATCH";
```

- [ ] **Read ProblemDetailsMapper.cs** — find CARD_DOCUMENT_PROJECT_MISMATCH

- [ ] **Remove its mapping** from ProblemDetailsMapper

- [ ] **Build and verify**

```bash
dotnet build
```

Expected: 0 errors

- [ ] **Commit**

```bash
git add src/HydraForge.Domain/Common/DomainErrorCodes.cs src/HydraForge.Server/Errors/ProblemDetailsMapper.cs && git commit -m "refactor: remove CARD_DOCUMENT_PROJECT_MISMATCH error code"
```

---

### Task 7: Update application tests

**Files:**
- Modify: `tests/HydraForge.Application.Tests/Specs/SpecServiceTests.cs`
- Modify: `tests/HydraForge.Application.Tests/Plans/PlanServiceTests.cs`

- [ ] **Read current SpecServiceTests.cs** — find link/unlink tests, examine test structure

- [ ] **Remove link/unlink tests** from SpecServiceTests.cs (delete `LinkToCardAsync_...` and `UnlinkFromCardAsync_...` tests)

- [ ] **Update create test** — add CardId to `CreateSpecCommand`

```csharp
var cmd = new CreateSpecCommand(projectId, cardId, actorId, "Test Spec", null, "Content");
```

- [ ] **Update InMemorySpecRepository** — add `ListByCardAsync`

```csharp
public Task<IReadOnlyList<Spec>> ListByCardAsync(Guid cardId, SpecListFilter filter, CancellationToken ct = default)
    => Task.FromResult<IReadOnlyList<Spec>>(Specs.Where(s => s.CardId == cardId).ToList());
```

- [ ] **Same for PlanServiceTests.cs** — remove link/unlink, update create test, add ListByCardAsync

- [ ] **Build Application tests**

```bash
dotnet build
```

Expected: 0 errors

- [ ] **Run Application tests**

```bash
dotnet test --no-build --filter Spec ServiceTests
dotnet test --no-build --filter Plan ServiceTests
```

Expected: tests pass (fewer tests since link/unlink removed)

- [ ] **Commit**

```bash
git add tests/HydraForge.Application.Tests/ && git commit -m "test: update spec/plan app tests — remove link/unlink, add CardId to create"
```

---

### Task 8: Update server tests

**Files:**
- Modify: `tests/HydraForge.Server.Tests/Specs/SpecsControllerTests.cs`
- Modify: `tests/HydraForge.Server.Tests/Plans/PlansControllerTests.cs`

- [ ] **Read current SpecsControllerTests.cs** — find link/unlink test methods, examine URL patterns

- [ ] **Remove link/unlink test methods** and the in-memory repo methods they exercise

- [ ] **Update create test URL** to use card-scoped route

```csharp
var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/cards/{cardId}/specs");
// Body includes title, description, content (NOT cardId — it's in the URL)
```

- [ ] **Remove Card.SpecId / Card.PlanId assignments** from test data setup (entity no longer has these properties)

- [ ] **Update SpecsTestSpecRepository** — remove `GetCardByIdAsync`, `GetLinkedCardIdAsync`, `GetLinkedCardIdsAsync` if present; add `ListByCardAsync`

- [ ] **Same for PlansControllerTests.cs**

- [ ] **Build and run server tests**

```bash
dotnet build && dotnet test --no-build --filter SpecsControllerTests
dotnet test --no-build --filter PlansControllerTests
```

- [ ] **Commit**

```bash
git add tests/HydraForge.Server.Tests/ && git commit -m "test: update spec/plan server tests — card-scoped routes, remove link/unlink"
```

---

### Task 9: Update HTTP test file + migrate old HTTP tests

**Files:**
- Modify: `src/HydraForge.Server/HttpTests/SpecsPlans.http`

- [ ] **Read current SpecsPlans.http** — find all link/unlink and create endpoints

- [ ] **Rewrite HTTP tests** — card-scoped create/list, remove link/unlink tests

Create pattern:
```http
### Create a Spec on Card
POST {{baseUrl}}/api/projects/{{createProject.response.body.$.id}}/cards/{{createCard.response.body.$.id}}/specs HTTP/1.1
Authorization: Bearer {{adminToken}}
Content-Type: application/json
X-Correlation-Id: smoke-test-specplan-001

{
    "title": "Auth Module Spec",
    "description": "Specification for auth module",
    "content": "# Auth Module\n\n## Requirements\n- Login via email/password\n- JWT token refresh\n- Role-based access"
}
```

Remove all link/unlink test blocks.

- [ ] **Commit**

```bash
git add src/HydraForge.Server/HttpTests/SpecsPlans.http && git commit -m "test: update HTTP tests — card-scoped routes, remove link/unlink"
```

---

### Task 10: Update data-model.md

**Files:**
- Modify: `docs/data-model.md`

- [ ] **Update Card entity section** — remove SpecId, PlanId fields

```markdown
### Card

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| CardNumber | int | Sequential per project (e.g. #1, #42) — unique within project, never reused |
| ProjectId | Guid | FK to Project |
| ColumnId | Guid | FK to Column |
| ParentCardId | Guid? | FK to parent card (epic → child) |
<!-- REMOVED: SpecId, PlanId -->
```

- [ ] **Update Spec entity section** — add CardId

```markdown
### Spec

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| ProjectId | Guid | FK to Project |
| CardId | Guid | FK to Card — owning card |
| Title | string | Display name, e.g. "Auth Module Spec" |
| Content | string | Current markdown content |
| Version | int | Increments on each edit |
| CreatedByUserId | Guid | |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
```

- [ ] **Update Plan entity section** — add CardId, SpecId

```markdown
### Plan

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| ProjectId | Guid | FK to Project |
| CardId | Guid | FK to Card — owning card |
| SpecId | Guid? | FK to Spec — optional parent specification |
| Title | string | Display name, e.g. "Auth Implementation Plan" |
| Content | string | Current markdown (numbered steps) |
| Version | int | Increments on each edit |
| CreatedByUserId | Guid | |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
```

- [ ] **Update ER diagram** — fix Card→Spec and Card→Plan relationship lines

```diff
- Card (1) ──┬── (1) Spec? (optional link)
-            └── (1) Plan? (optional link)
+ Card (1) ──┬── (N) Spec (ownership via Spec.CardId)
+ Spec (1) ──┬── (N) Plan (ownership via Plan.SpecId)
```

- [ ] **Commit**

```bash
git add docs/data-model.md && git commit -m "docs: update data model for spec/plan ownership redesign"
```

---

### Task 11: Run full test suite

- [ ] **Run all tests**

```bash
dotnet test
```

Expected: All tests pass.

- [ ] **Fix any failures** — iterate until green

- [ ] **Commit any remaining fixes**

```bash
git add -A && git commit -m "fix: test/testdata cleanup after spec/plan ownership refactor"
```

---

### Task 12: Update functional-spec.md and architecture.md

- [ ] **Read functional-spec.md** — check FR-12, FR-13, FR-14 descriptions

- [ ] **Update FR-12**: "Cards: Linked to Specs and Plans" → "Cards: Own multiple specs and plans"

- [ ] **Check architecture.md** — update SpecService/PlanService description to reflect ownership

- [ ] **Commit**

```bash
git add docs/ && git commit -m "docs: update functional-spec and architecture for ownership model"
```
