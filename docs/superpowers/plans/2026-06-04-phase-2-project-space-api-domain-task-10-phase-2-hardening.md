# Task 10: Cross-cutting audit/error/test hardening for Phase 2 completeness
**Branch:** `task/phase-2-hardening`
**Parent branch:** `feat/phase-2-project-space-api-domain`
**Parent spec:** `2026-06-04-phase-2-project-space-api-domain-design.md`

**Goal:** Close Phase 2 acceptance gaps: exhaustive audit coverage, error mapping, endpoint smoke files, model/migration checks, and full test/build pass.

**Files:** Modify/read `DomainErrorCodes.cs`, `ProblemDetailsMapper.cs`, `GlobalExceptionMiddleware.cs`, `AuditService.cs`, `EfAuditLogWriter.cs`, `HydraForgeDbContext.cs`, all `src/HydraForge.Server/HttpTests/*.http`, all Phase 2 Application services/controllers/tests from merged tasks.

## Steps

- [ ] Build audit matrix from spec lines 170-184. For each mutation service, write/extend tests asserting `AuditLogRequest` with `Scope=Project`, `ProjectId`, actor, entity type/id, action, useful old/new JSON.
- [ ] Fill any missing audit calls in Project, Column, Card, Checklist, Comment, Attachment, Spec, Plan, Relationship services. Do not audit presence. Snapshot only piggybacks on triggering mutation.
- [ ] Write mapper tests for every Phase 2 `DomainErrorCodes` constant: expected status, `code`, `correlationId`, stable type URL.
- [ ] Update `ProblemDetailsMapper`; keep 404 for not-visible/project-not-found leaks, 403 only where existence already known or admin-only system operation.
- [ ] Write/extend Infrastructure model tests: `Project.ArchivedAt`, unique/project member, unique card number, column/card position indices, relationship index, cascade rules unaffected, vector extension still configured.
- [ ] Run EF pending-model check: `PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server`. Expected: no pending changes. If pending, add migration in the task that introduced schema change or this hardening task if cross-cutting.
- [ ] Review every `src/HydraForge.Server/HttpTests/*.http`. Add missing endpoint smoke requests so every route in spec lines 87-104 appears at least once. Include auth token variables and correlation header examples.
- [ ] Add `src/HydraForge.Server/HttpTests/README.md` explaining order: auth/project setup first, then columns/cards/etc. This is docs-only and part of endpoint smoke acceptance.
- [ ] Run: `dotnet build`; expected success.
- [ ] Run: `dotnet test`; expected success.
- [ ] Commit: `git add src tests && git commit -m "test: harden phase 2 API coverage"`.

**Acceptance:** full build/test pass; no pending EF model changes; all expected errors map to ProblemDetails; all Phase 2 endpoints have `.http` smoke coverage; audit rows exist for all project-space mutations.
