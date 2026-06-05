# Task 8: ProjectContextSnapshot template regeneration on board mutations
**Branch:** `task/project-context-snapshot`
**Parent branch:** `feat/phase-2-project-space-api-domain`
**Parent spec:** `2026-06-04-phase-2-project-space-api-domain-design.md`

**Goal:** Regenerate deterministic `ProjectContextSnapshot.TemplateContent` after project-board mutations and expose snapshot endpoint.

**Files:** Modify/read `ProjectContextSnapshot.cs`, `Card.cs`, `Column.cs`, `CardRelationship.cs`, `DomainErrorCodes.cs`, `HydraForgeDbContext.cs`, `ProblemDetailsMapper.cs`, `Program.cs`. Modify services from tasks 1-7 after merge to call snapshot port. Create `src/HydraForge.Application/ProjectSnapshots/*`, `src/HydraForge.Infrastructure/ProjectSnapshots/*`, `src/HydraForge.Server/Controllers/Projects/ProjectSnapshotController.cs`, tests, smoke `http/phase-2/snapshot.http`.

## Steps

- [ ] Write Application tests for deterministic renderer: groups by column position, cards by position/card number, includes card id, `#CardNumber`, title, column, type, blockers, recent moved cards; excludes archived cards/relationships by default; leaves `AiNarrative` null.
- [ ] Define `IProjectContextSnapshotRepository` and `ProjectContextSnapshotService` in Application. Renderer is pure and separately tested.
- [ ] Implement EF repository loading current board shape and upserting snapshot in same transaction when called by mutation service.
- [ ] Add `ProjectBoardMutationHooks` or simple `IProjectSnapshotRefresher` port; update Project/Column/Card/Checklist/Comment/Attachment/Spec/Plan/Relationship services to call after successful board mutation before publish step (publish happens Task 9). Avoid noisy standalone audit.
- [ ] Write Server tests for `GET /api/projects/{projectId}/snapshot`: members/admin can read, non-members get not-found-style ProblemDetails.
- [ ] Implement controller.
- [ ] Create `http/phase-2/snapshot.http` covering get current snapshot and non-member/invalid project.
- [ ] Run `dotnet test --filter Snapshot`; `dotnet test`.
- [ ] Commit: `git add src tests http && git commit -m "feat: maintain project snapshots"`.

**Acceptance:** every board mutation regenerates template synchronously after DB state changes; no LLM call; endpoint has `.http` smoke test.
