# Task 7: CardRelationship CRUD, same-project validation, cycle detection, archive impact/confirm flow
**Branch:** `task/card-relationships-cycle-archive`
**Parent branch:** `feat/phase-2-project-space-api-domain`
**Parent spec:** `2026-06-04-phase-2-project-space-api-domain-design.md`

**Goal:** Ship relationship APIs for `BlockedBy`, `Precedes`, `Relates`, DAG validation, archive-impact preflight, confirmed relationship archive.

**Files:** Modify/read `CardRelationship.cs`, `RelationshipType.cs`, `Card.cs`, `DomainErrorCodes.cs`, `HydraForgeDbContext.cs`, `ProblemDetailsMapper.cs`, `Program.cs`. Create `src/HydraForge.Application/CardRelationships/*`, `src/HydraForge.Infrastructure/CardRelationships/*`, `src/HydraForge.Server/Controllers/Projects/CardRelationshipsController.cs`, tests, smoke `http/phase-2/card-relationships.http`.

## Steps

- [ ] Write pure Domain/Application graph tests: self-link rejected; cross-project rejected; duplicate active type rejected; `BlockedBy` and `Precedes` cycle rejected; `Relates` does not participate in cycle but cannot self-link.
- [ ] Add error codes: `RELATIONSHIP_NOT_FOUND`, `RELATIONSHIP_DUPLICATE`, `RELATIONSHIP_CROSS_PROJECT_DENIED`, `RELATIONSHIP_CYCLE`, `RELATIONSHIP_SELF_DENIED`, `CARD_ARCHIVE_IMPACT_CONFIRM_REQUIRED`.
- [ ] Implement `CardDependencyGraph` pure helper in Application.
- [ ] Implement service list/create/delete, archive impact list, `ArchiveCardWithRelationshipsAsync(cardId, confirm)` used by Task 3 integration after merge.
- [ ] Implement EF repository. Unique active relationship cannot be partial unique in current simple model unless configured; prefer application duplicate check plus DB index `(SourceCardId, TargetCardId, Type)` if adding migration. Include `ArchivedAt == null` in all active queries.
- [ ] Write Server tests for list/create/delete and `/archive-impact`; confirmed archive flow should archive relationships touching card.
- [ ] Implement controller routes per spec under `/relationships` and `/archive-impact`.
- [ ] Update mapper.
- [ ] Create `http/phase-2/card-relationships.http` covering create blocked-by, duplicate failure, cycle failure, relates success, list, delete, archive impact, confirm archive.
- [ ] Run `dotnet test --filter Relationship`; `dotnet test`.
- [ ] Commit: `git add src tests http && git commit -m "feat: add card relationships"`.

**Acceptance:** cycles never persist; cross-project edges rejected; archive preflight lists dependents; `.http` covers every relationship endpoint.
