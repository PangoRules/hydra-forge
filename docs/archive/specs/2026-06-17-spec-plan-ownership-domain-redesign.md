# Spec/Plan Ownership Domain Redesign

**Date:** 2026-06-17
**Branch:** `task/versioned-specs-plans`
**Parent spec:** `2026-06-04-phase-2-project-space-api-domain-design.md`

## Problem

Current model uses `Card.SpecId: Guid?` and `Card.PlanId: Guid?` — single nullable FKs from Card to Spec/Plan. This enforces a 1-to-1 relationship: one card can have at most one spec and one plan.

The domain requires:
- A card may have **multiple** specs (one card → many design documents)
- A spec may have **multiple** plans (one design doc → many implementation plans)
- Each spec/plan "belongs" to the card that created it (ownership)
- Other cards may read but not mutate owned specs/plans
- This mirrors the superpowers workflow: Spec (design) → Plan (impl) → Cards (tasks)

## Solution: Approach A — Ownership FK on Spec/Plan

Remove the FKs from Card. Add ownership FKs on Spec and Plan instead.

### Entity Changes

**Spec** — add owning card FK:
```
Spec:
  + CardId: Guid (FK → Card, NOT NULL)   ← the card that created this spec
```

**Plan** — add owning card FK + optional parent spec FK:
```
Plan:
  + CardId: Guid (FK → Card, NOT NULL)   ← the card that created this plan
  + SpecId: Guid? (FK → Spec, nullable)  ← optional parent spec
```

**Card** — remove the single-link FKs:
```
Card:
  - SpecId: Guid? (remove)
  - PlanId: Guid? (remove)
```

### Relationships (1-to-many)

```
Card (1) ──┬── (N) Spec     ← Card.Specs = Specs.Where(s => s.CardId == cardId)
Spec (1) ──┬── (N) Plan     ← Spec.Plans = Plans.Where(p => p.SpecId == specId)
Card (1) ──┬── (N) Plan     ← denormalized for fast direct card→plans lookup
```

Navigation properties on entities:
- `Spec.Card` (→ Card), `Spec.Plans` (→ Plan[])
- `Plan.Card` (→ Card), `Plan.Spec` (→ Spec, nullable)
- `Card.Specs` (→ Spec[]), `Card.Plans` (→ Plan[])

### Business Rules

- Creating a spec **requires** a `CardId` — the owning card
- Creating a plan **requires** a `CardId` — owning card; optional `SpecId` for parent spec
- Only the owning card's members can edit/update/delete its specs and plans
- Any project member can read any spec/plan (project-level visibility)
- Archiving/deleting a card cascades to its specs and plans
- `card.SpecId` / `card.PlanId` no longer exist — no separate "link/unlink" concept
- Ownership is immutable: `CardId` cannot change after creation

### API Surface Changes

**Replace project-level routes with card-scoped routes:**

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/projects/{projectId}/cards/{cardId}/specs` | Create spec on card (ownership) |
| GET | `/api/projects/{projectId}/cards/{cardId}/specs` | List specs belonging to card |
| GET | `/api/projects/{projectId}/specs/{specId}` | Get single spec by ID |
| PUT | `/api/projects/{projectId}/specs/{specId}` | Update spec (memb. check verifies owning card's project) |
| GET | `/api/projects/{projectId}/specs/{specId}/versions` | List versions |
| POST | `/api/projects/{projectId}/specs/{specId}/restore` | Restore version |
| POST | `/api/projects/{projectId}/cards/{cardId}/plans` | Create plan on card |
| GET | `/api/projects/{projectId}/cards/{cardId}/plans` | List plans belonging to card |
| GET | `/api/projects/{projectId}/plans/{planId}` | Get single plan by ID |
| PUT | `/api/projects/{projectId}/plans/{planId}` | Update plan |
| GET | `/api/projects/{projectId}/plans/{planId}/versions` | List versions |
| POST | `/api/projects/{projectId}/plans/{planId}/restore` | Restore version |

**Removed:** Separate `link` and `unlink` endpoints — ownership replaces the concept.

### Audit Log

- Create spec: `"SpecCreated"` on card — includes owning cardId
- Create plan: `"PlanCreated"` on card and parent spec
- Update/restore: existing behavior, no change
- Delete spec/plan: cascade from card archive/delete

### Migration Strategy

1. Entity changes: add `Spec.CardId`, `Plan.CardId`, `Plan.SpecId`; remove `Card.SpecId`, `Card.PlanId`
2. EF migration: add FKs, drop old columns, set `Spec.CardId`/`Plan.CardId` based on existing link data (if any links exist in a dev DB, migrate them; in practice this feature is freshly built so it's a schema-only migration)
3. Update DbContext model configuration
4. Update repositories: remove `GetCardByIdAsync`, `GetLinkedCardIdAsync` (done in prior refactor), add card-scoped list methods

### Testing

- Application tests: create spec on card → `Spec.CardId == cardId`; create plan on spec → `Plan.SpecId == specId`; update from non-owning card rejected; card archive cascades
- Infrastructure tests: FK relationships, cascade behavior, indexes
- Server tests: new routes respond correctly, old link/unlink endpoints return 404

### File Changes

| File | Change |
|------|--------|
| `Spec.cs` | Add `CardId`, remove `Card.SpecId` refs |
| `Plan.cs` | Add `CardId`, `SpecId`; remove `Card.PlanId` refs |
| `Card.cs` | Remove `SpecId`, `PlanId` |
| `HydraForgeDbContext.cs` | Update entity config, migration |
| `SpecService.cs` | Change create to require CardId; update membership checks |
| `PlanService.cs` | Same |
| `SpecsController.cs` | Change routes to card-scoped |
| `PlansController.cs` | Same |
| `SpecModels.cs` | Update commands |
| `PlanModels.cs` | Update commands |
| Delete link/unlink endpoints | Removed from services and controllers |
| Tests | Update to match new model |
