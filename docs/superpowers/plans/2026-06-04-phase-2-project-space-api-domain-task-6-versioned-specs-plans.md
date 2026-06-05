# Task 6: Versioned Specs and Plans, card links, restore flow
**Branch:** `task/versioned-specs-plans`
**Parent branch:** `feat/phase-2-project-space-api-domain`
**Parent spec:** `2026-06-04-phase-2-project-space-api-domain-design.md`

**Goal:** Ship project-level versioned markdown specs/plans, immutable version snapshots, restore, and card link/unlink APIs.

**Files:** Modify/read `Spec.cs`, `SpecVersion.cs`, `Plan.cs`, `PlanVersion.cs`, `Card.cs`, `DomainErrorCodes.cs`, `HydraForgeDbContext.cs`, `ProblemDetailsMapper.cs`, `Program.cs`. Create `src/HydraForge.Application/ProjectDocuments/*`, `src/HydraForge.Infrastructure/ProjectDocuments/*`, `src/HydraForge.Server/Controllers/Projects/SpecsController.cs`, `PlansController.cs`, tests, smoke `http/phase-2/specs-plans.http`.

## Steps

- [ ] Write Application tests for create spec/plan creates version 1 in same transaction; update increments version and writes immutable snapshot; restore copies old version content into current and writes new version; markdown payload limit enforced; link/unlink to card validates same project and membership.
- [ ] Add error codes: `SPEC_NOT_FOUND`, `PLAN_NOT_FOUND`, `DOCUMENT_VERSION_NOT_FOUND`, `MARKDOWN_PAYLOAD_TOO_LARGE`, `CARD_DOCUMENT_PROJECT_MISMATCH`.
- [ ] Implement shared internal document logic without over-abstracting public API: `SpecService` and `PlanService` may share helper for version append.
- [ ] Implement EF repository transactions for save/restore/link.
- [ ] Add EF tests for version relationships/indexes if missing.
- [ ] Write Server tests for `/api/projects/{projectId}/specs`, `/specs/{specId}/versions`, restore, card link/unlink; repeat for plans.
- [ ] Implement controllers. Routes per spec: `/api/projects/{projectId}/specs`, `/api/projects/{projectId}/plans`, `/versions`.
- [ ] Update mapper.
- [ ] Create `http/phase-2/specs-plans.http` covering create/list/get/update/version-list/restore/link/unlink for spec and plan.
- [ ] Run `dotnet test --filter Spec`; `dotnet test --filter Plan`; `dotnet test`.
- [ ] Commit: `git add src tests http && git commit -m "feat: add versioned specs and plans"`.

**Acceptance:** every save creates version snapshot atomically; restore creates new version not mutating old; `.http` covers every endpoint.
