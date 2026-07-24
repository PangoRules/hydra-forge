# Card Type Redesign — Universal Project Management

**Date:** 2026-06-28
**Status:** Approved
**Scope:** Rename card types, remove parent-type restriction, update UI labels

## Problem

Current card types are software-development-specific and don't make sense for Hydra-Forge's universal project management vision:

| Current Type | Problem |
|---|---|
| **Bug** | Software-only. "Bug" means nothing in event planning, content strategy, construction. |
| **Epic** | Agile jargon. Non-developers don't know what an "Epic" is. |
| **Spec** | Redundant with the `/Specs` document system. Any card can already own a spec document via `Spec.CardId` FK — the card type doesn't gate document ownership. |

Additionally, `parentCardId` is restricted to Epic-type parents only (`Card.ValidateParentEpic` checks `parent.Type != CardType.Epic`). This blocks the natural workflow of Goal→Tasks hierarchy where the Goal card groups its child tasks.

## Design

### Card Types (4 types)

| Type | Icon | Purpose | Example |
|---|---|---|---|
| **Goal** | 🎯 `layers` | A significant objective. Groups child tasks. Can own a spec document. | "Phase 3 Web UI", "v1.0 Release", "Q3 Content Calendar" |
| **Task** | ✅ `square-check` | A unit of work. Child of a Goal. Can own a plan document. | "Plan 4a: Board filtering", "Write blog post", "Book venue" |
| **Issue** | ⚠️ `bug` | A problem, concern, or question. Not planned work. | "Login broken on mobile", "Caterer cancelled", "Need clarification on scope" |
| **Idea** | 💡 `lightbulb` | A suggestion. May become a Goal or Task later. | "Add dark mode", "Start podcast", "Redesign logo" |

### Parent Hierarchy

- **Remove the `parent.Type != CardType.Epic` restriction** in `Card.ValidateParentEpic()`
- Any card can be a parent of any other card (cycle detection still applies)
- Rename "Parent Epic" → "Parent" in all UI labels
- Parent dropdown shows all non-archived cards in the project (not filtered by type)

### Workflow

```
Idea → becomes Goal (when decided) → Goal spawns Tasks → Tasks complete → Goal completes
                                                                     ↓
                                                              Issue (if something breaks)
```

### What Stays Unchanged

- Spec/Plan document system (`/Specs`, `/Plans` endpoints, versioning, restore) — unchanged
- Card relationships (blocker/duplicate/relates-to graph) — unchanged
- `parentCardId` FK on Card entity — unchanged, just less restricted
- `Spec.CardId` and `Plan.CardId` FKs — any card type can own documents (already true)

## Migration

### Backend Changes

1. **Rename enum values** in `Domain/Enums/CardType.cs`:
   - `Bug = 2` → `Issue = 2`
   - `Epic = 5` → `Goal = 5`
   - `Spec = 3` → *removed* (numeric value 3 becomes unused)

   Final enum:
   ```csharp
   public enum CardType
   {
       Task = 1,
       Issue = 2,
       // 3 = removed (was Spec)
       Idea = 4,
       Goal = 5
   }
   ```

2. **Remove parent-type restriction** in `Domain/Entities/ProjectSpace/Card.cs`:
   - Delete the `parent.Type != CardType.Epic` check in `ValidateParentEpic()`
   - Rename method `ValidateParentEpic` → `ValidateParent`

3. **Update error codes** in `Domain/Common/DomainErrorCodes.cs`:
   - `InvalidParentEpic = "CARD_INVALID_PARENT_EPIC"` → `InvalidParent = "CARD_INVALID_PARENT"`

4. **Update all references** to renamed enum values across Application, Infrastructure, Server layers

5. **EF Core migration**: Renaming enum values stored as integers in the database — existing rows with value `2` (Bug→Issue) and `5` (Epic→Goal) keep their integer values. Value `3` (Spec) rows need migration to another type or archival handling.

6. **Update OpenAPI spec**: Regenerate to reflect new enum names

### Frontend Changes

1. **Update `app/lib/card-type.ts`**:
   - Replace `CARD_TYPE_MAP`, `CARD_TYPE_OPTIONS`, `CARD_TYPE_FILTER_OPTIONS` with new types
   - Remove Spec, rename Bug→Issue, Epic→Goal
   - New indices: `0: Task, 1: Issue, 2: Goal, 3: Idea`

2. **Update `CardCreateModal.vue`**:
   - Rename "Parent Epic" → "Parent"
   - Parent dropdown fetches all cards (remove `?type=Epic` filter)
   - Update type dropdown options

3. **Update `BoardCard.vue`** and `BoardMobileList.vue`:
   - Epic indicator → Parent indicator (generic, not type-specific)
   - Update type icons and labels

4. **Update `CardMetadata.vue`**:
   - Add parent card display/editing in metadata panel

5. **Update all filter bars and dropdowns** referencing old type names

### Data Migration Strategy

Existing cards with type `Spec` (value 3) need handling:
- **Option A**: Map Spec→Goal (treat existing specs as goals)
- **Option B**: Map Spec→Task (treat as regular tasks)
- **Recommendation**: Option A — Spec cards were used as grouping documents, which maps to Goal

### Affected Files

**Backend:**
- `src/HydraForge.Domain/Enums/CardType.cs`
- `src/HydraForge.Domain/Entities/ProjectSpace/Card.cs`
- `src/HydraForge.Domain/Common/DomainErrorCodes.cs`
- `src/HydraForge.Application/Cards/CardService.cs`
- `src/HydraForge.Application/Cards/CardModels.cs`
- `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`
- `src/HydraForge.Server/Controllers/Projects/CardsController.cs`
- All test files referencing CardType enum

**Frontend:**
- `src/web-ui/app/lib/card-type.ts`
- `src/web-ui/app/components/board/CardCreateModal.vue`
- `src/web-ui/app/components/board/BoardCard.vue`
- `src/web-ui/app/components/board/BoardMobileList.vue`
- `src/web-ui/app/components/board/BoardFilterBar.vue`
- `src/web-ui/app/components/board/ColumnHeader.vue`
- `src/web-ui/app/components/board/BoardColumn.vue`
- `src/web-ui/app/components/card/CardMetadata.vue`
- `src/web-ui/app/stores/board.ts`
- `src/web-ui/app/composables/useBoardFilters.ts`
- `src/web-ui/app/types/api.d.ts` (regenerate from OpenAPI)

**Documentation:**
- `docs/functional-spec.md` — update FR-6, FR-10
- `docs/data-model.md` — update CardType enum
- `docs/glossary.md` — add Goal, Issue; update Bug→Issue, Epic→Goal