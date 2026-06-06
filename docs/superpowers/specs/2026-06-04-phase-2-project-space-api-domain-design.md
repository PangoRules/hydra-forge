# Phase 2 Project Space API & Domain Design

**Branch:** `feat/phase-2-project-space-api-domain`

## Purpose

Phase 2 makes the project board complete through the backend API. It delivers project, board, card, spec, plan, attachment, relationship, snapshot, realtime, and presence behavior with business logic covered by tests. It does not build Web UI or TUI screens.

This phase turns the Phase 1 skeleton into a usable API surface for later Web/TUI phases while preserving Clean Architecture, server-authoritative state, PostgreSQL persistence, `Result<T, Error>` expected failures, audit logging, SignalR broadcasts, and member-only project visibility.

## Source Requirements

- `docs/functional-spec.md` Phase 2 checklist lines 454-477.
- Project-space FRs: FR-1 through FR-15, FR-22 through FR-30, FR-43 through FR-48, FR-52, FR-149 through FR-159, FR-160 where manual card creation is backend-supported.
- Architecture: Domain ← Application ← Infrastructure ← Server/TUI; all mutations through API; SignalR push after DB mutation; ProblemDetails with correlation id for API errors.
- Data model: Project, Column, Card, ProjectMember, CardAssignee, ChecklistItem, Comment, Attachment, Spec/SpecVersion, Plan/PlanVersion, CardRelationship, CardWatcher, ProjectContextSnapshot, AuditLogEntry.
- Decisions: no offline mode, member-only visibility, any authenticated user can create project, Owner/Member roles only, local card numbers, local/S3 attachment storage abstraction, typed relationships with application-level cycle detection, ProjectContextSnapshot template regenerated on every board mutation.

## Decided Scope

### In Scope

- Project CRUD, archive, delete policy, git remote fields, and ProjectMember Owner/Member management.
- Project archive entry point that adds/sets `Project.ArchivedAt` and cascades to project chat folder/sessions through a minimal `ChatArchiveService.ArchiveFolder` implementation.
- Per-project columns with default set: Backlog, Spec-ing, Planned, In Dev, In Review, Done.
- Column CRUD and reordering.
- Card CRUD, per-project `CardNumber`, type, assignees, parent epic link, column move, position ordering, blocked move warning payload.
- Checklist item CRUD, completion toggle, position ordering, optional assignee.
- Comment CRUD/archive, `@mention` extraction, CardWatcher auto-add on comment and assignment.
- Attachment upload/list/download/delete metadata with `IFileStore` abstraction.
- Local filesystem file storage default; S3-compatible storage opt-in by env/config.
- Versioned markdown Specs and Plans linked to cards.
- CardRelationship CRUD for `BlockedBy`, `Precedes`, `Relates`; same-project validation; acyclic validation; archive behavior for dependent cards.
- ProjectContextSnapshot `TemplateContent` regeneration after each project-board mutation; `AiNarrative` remains null/deferred until scheduled LLM phase.
- SignalR board mutation broadcasts to connected project members.
- PresenceHub ephemeral join/leave/card-focus events with no DB writes.
- API authorization rules, structured audit entries, named Domain error codes, xUnit business-logic coverage.

### Out of Scope

- Web UI and TUI screens.
- LLM calls, AI card proposals, chat sessions, project chat panel, nightly `AiNarrative` generation.
- ntfy delivery and notification bell UI. Phase 2 may create notification records only where needed for project-space rules if existing infra supports it; external push remains Phase 5.
- Housekeeping hard-delete service and file cleanup scheduler. Phase 2 must mark archive state and expose delete/archive semantics; later housekeeping does retention deletion.
- Cross-project card dependencies.
- Offline sync, local caches, conflict resolution.

## Architecture

### Layer Boundaries

- Domain owns entities, enums, value-level validation helpers, error codes, and `Result<T, Error>`.
- Application owns use cases and orchestration: membership checks, ordering, watcher behavior, cycle detection, snapshot rendering, audit requests, file-store calls through abstractions.
- Infrastructure owns EF Core repositories/unit-of-work, migrations, local/S3 file-store implementations, optional chat archive infrastructure adapter, SignalR sender implementation.
- Server owns controllers, request/response DTO mapping, auth policies, multipart upload endpoints, ProblemDetails mapping, and SignalR hubs.

Controllers must not directly use `HydraForgeDbContext`, file-system APIs, S3 SDKs, or SignalR client groups except hub endpoints. All expected failures return Domain/Application errors and map to ProblemDetails.

### Application Services

- `ProjectService`: create/read/update/archive/delete projects, manage members, enforce visibility and Owner-only membership/admin operations, and call `ChatArchiveService.ArchiveFolder` during project archive when a project chat folder exists.
- `ColumnService`: create/update/delete/reorder columns, create default columns, enforce project membership and non-empty delete rules.
- `CardService`: create/update/archive/delete/move cards, assign/unassign users, parent epic link, positions, blocked move warning, audit, snapshot refresh, board broadcast.
- `ChecklistService`: checklist CRUD, toggle, reorder, optional assignee validation.
- `CommentService`: comment CRUD/archive, mention extraction, watcher auto-add, audit, snapshot refresh when needed.
- `AttachmentService`: upload metadata + file content via `IFileStore`, list/download/delete attachments, validate limits and membership.
- `SpecService` and `PlanService`: current markdown document CRUD, version append on save, restore previous version, link/unlink to card.
- `CardRelationshipService`: typed relationship CRUD, same-project validation, cycle detection, dependent archive warning, confirm archive flow.
- `ProjectContextSnapshotService`: pure template builder that renders deterministic markdown/text from current board state and stores it after mutations.
- `ProjectBoardEventPublisher`: Application abstraction for realtime events; infrastructure/server implementation sends SignalR messages to project-member groups.
- `PresenceService`: server-side in-memory tracker for project/card presence; no persistence.

### Persistence

- Add `Project.ArchivedAt: DateTime?` via EF Core migration because Phase 1 entity/data model currently lack it while Phase 2 archive behavior requires it.
- Keep table naming `snake_case` plural.
- Keep `ArchivedAt: DateTime?` for soft archive. Do not add `IsArchived`.
- Archived-by-default queries manually filter `ArchivedAt == null`; no global query filters.
- Use transactions around multi-row mutations: project creation/default columns/snapshot/chat folder, card moves/reorders, document version saves, relationship archive confirmation.
- Use PostgreSQL uniqueness/indices for invariants where possible: project member uniqueness, card number per project, column position lookups, card position lookups, relationship source/target/type active uniqueness.
- Implement minimal chat archive persistence needed by project archive: set `ChatFolder.ArchivedAt` for the project folder and every child folder/session under it. Full chat CRUD and chat UX remain Phase 7.

## API Surface

Route names may adjust to project conventions, but behavior must remain stable for Web/TUI parity.

- `/api/projects`: list visible projects, create project.
- `/api/projects/{projectId}`: get/update/archive/delete project.
- `/api/projects/{projectId}/members`: list/add/update/remove members.
- `/api/projects/{projectId}/columns`: list/create/reorder.
- `/api/projects/{projectId}/columns/{columnId}`: update/delete.
- `/api/projects/{projectId}/cards`: list/create; supports filtering by column, archived flag, assignee, type.
- `/api/projects/{projectId}/cards/{cardIdOrNumber}`: get/update/archive/delete.
- `/api/projects/{projectId}/cards/{cardIdOrNumber}/move`: move with blocked-warning preflight and confirm flag.
- `/api/projects/{projectId}/cards/{cardIdOrNumber}/assignees`: assign/unassign.
- `/api/projects/{projectId}/cards/{cardIdOrNumber}/checklist`: item CRUD/toggle/reorder.
- `/api/projects/{projectId}/cards/{cardIdOrNumber}/comments`: comment CRUD/archive.
- `/api/projects/{projectId}/cards/{cardIdOrNumber}/attachments`: multipart upload/list.
- `/api/projects/{projectId}/cards/{cardIdOrNumber}/attachments/{attachmentId}`: download/delete.
- `/api/projects/{projectId}/specs` and `/api/projects/{projectId}/plans`: document CRUD/list.
- `/api/projects/{projectId}/specs/{specId}/versions` and `/plans/{planId}/versions`: version list/restore.
- `/api/projects/{projectId}/cards/{cardIdOrNumber}/relationships`: list/create/delete.
- `/api/projects/{projectId}/cards/{cardIdOrNumber}/archive-impact`: dependent-card warning payload.
- `/api/projects/{projectId}/snapshot`: returns current `ProjectContextSnapshot.TemplateContent` for test/debug/admin use.

All endpoints require auth. Project members can view/mutate project-space resources. Project Owners manage membership and archive/delete project. Admin can manage all projects but must not gain access to personal data.

## Realtime & Presence

### BoardHub

- Clients join project group only after membership/admin authorization.
- Mutations publish event envelope: `eventId`, `projectId`, `entityType`, `entityId`, `action`, `version?`, `occurredAt`, minimal payload.
- Events are emitted after successful DB commit only.
- Clients treat events as invalidation/update hints; server remains source of truth.

### PresenceHub

- Tracks online users per project and optional active card focus.
- Join/leave/card-focus changes broadcast to project group.
- State is ephemeral in memory; no DB rows, no audit entries.
- Disconnect removes presence after SignalR disconnect timeout.

## File Storage Decision

Default is local filesystem. S3-compatible storage is opt-in via environment/app settings.

Config keys should support:

- `FileStorage:Provider` or `FILE_STORAGE_PROVIDER`: `Local` default, `S3` opt-in.
- `FileStorage:LocalPath` or `FILE_STORAGE_PATH`: default under app data volume, not repo source tree.
- S3 endpoint, bucket, region, access key, secret key, force-path-style for MinIO/self-hosted compatibility.
- `.env.example` and server config docs/examples should show Local defaults and commented S3 opt-in keys.

`IFileStore` must expose store/read/delete operations using opaque storage keys. Application stores only metadata and opaque key/path in `Attachment.StoragePath`. File names are sanitized for metadata/display; storage keys are generated server-side and must not trust user filenames.

## Business Rules

- Any authenticated, enabled user may create a project and becomes Owner.
- Non-members cannot see project existence; return not-found style errors, not forbidden, where revealing existence would leak project data.
- Admin can see/manage all projects, but Phase 2 APIs must not expose user personal chats/notes/tasks/calendar/gallery/documents.
- Project members can mutate cards/columns/specs/plans. View-only member role is deferred.
- Project must always have at least one Owner.
- Column reorder is dense 0-based positions within project.
- Card positions are dense 0-based within each column after create/move/archive/delete/reorder.
- `CardNumber` is sequential per project, never reused, shown in API responses.
- Parent epic link only allows parent card type `Epic`; child and parent must be in same project; parent cycles are rejected.
- Card relationships must be same-project. `BlockedBy` and `Precedes` participate in cycle detection; `Relates` does not create blocking dependency cycles but still cannot self-link.
- Moving a blocked card returns warning payload unless `confirmBlockedMove=true`; move is never hard-blocked by dependency state.
- Archiving a card with dependents supports preflight impact list. Confirming archive sets card `ArchivedAt` and archives active relationship rows touching it.
- Comments and assignments auto-create CardWatcher row if missing.
- `@mention` extraction matches valid usernames and ignores archived/disabled users.
- Spec/Plan save increments current version and writes immutable version snapshot in same transaction.
- Snapshot template includes card id, card number, title, column, type, blockers, recent moves, and deterministic ordering.

## Error Handling

Add named error codes for every expected Phase 2 failure, including but not limited to:

- Project not found/not visible, project archived, owner required, last owner removal denied.
- Column not found, invalid column position, deleting non-empty column denied.
- Card not found, invalid card type, card archived, invalid assignee, duplicate assignee, invalid parent epic, parent cycle.
- Relationship not found, duplicate relationship, cross-project relationship denied, relationship cycle, self relationship denied.
- Spec/Plan not found, version not found, invalid markdown payload size.
- Attachment not found, unsupported content type, file too large, file-store unavailable.
- Membership denied, admin required, concurrency/version mismatch.

Expected failures return `Result<T, Error>` and map to ProblemDetails with correlation id. Unexpected exceptions go through global middleware. Infrastructure failures are wrapped in user-safe errors; raw SQL, filesystem, or S3 exceptions must not leak to clients.

## Audit Logging

Every mutation writes an audit row with `Scope=Project`, `ProjectId`, actor id, entity type/id, action, old/new JSON snapshots when useful, and timestamp.

Required audited actions:

- Project create/update/archive/delete/member changes.
- Column create/update/delete/reorder.
- Card create/update/move/archive/delete/assign/unassign/parent-link changes.
- Checklist create/update/delete/toggle/reorder.
- Comment create/update/archive.
- Attachment upload/delete.
- Spec/Plan create/update/version restore/link/unlink.
- Relationship create/delete/archive-on-card-archive.
- Snapshot regeneration may be audited only as part of triggering board mutation; avoid noisy standalone snapshot audit rows unless needed for debugging.

## Testing Strategy

- Domain tests for entity invariants, enum coverage, error codes, parent epic validation helper, relationship graph cycle detection.
- Application tests for each use case with pure fakes where possible: membership checks, ordering, card numbering, blocked move warning, archive impact, watcher auto-add, mention extraction, snapshot rendering.
- Infrastructure model tests for EF mappings, required properties, indices, delete behavior, archive columns, vector extension unaffected.
- Server tests for endpoint auth, ProblemDetails mapping, route behavior, multipart upload validation, SignalR authorization where practical.
- Optional PostgreSQL-backed tests only behind `HYDRAFORGE_TEST_CONNECTION_STRING`; no SQLite substitute for PostgreSQL behavior.
- Plain xUnit `Assert.*`; no FluentAssertions.

## Implementation Decomposition

This is a multi-task milestone. Each task below should ship on its own implementation branch created by the implementing architect/developer from this milestone branch.

## Tasks

- [X] Task 1: Project CRUD, membership, archive, visibility, default project creation workflow
- [ ] Task 2: Column CRUD, default columns, column reorder, ordering invariants
- [ ] Task 3: Card CRUD, card numbering, movement, assignees, parent epic links, blocked move warning
- [ ] Task 4: Checklist and comment APIs, mention extraction, CardWatcher auto-add
- [ ] Task 5: Attachment storage abstraction, local file store default, S3-compatible opt-in, attachment APIs
- [ ] Task 6: Versioned Specs and Plans, card links, restore flow
- [ ] Task 7: CardRelationship CRUD, same-project validation, cycle detection, archive impact/confirm flow
- [ ] Task 8: ProjectContextSnapshot template regeneration on board mutations
- [ ] Task 9: BoardHub mutation broadcasts and ephemeral PresenceHub
- [ ] Task 10: Cross-cutting audit/error/test hardening for Phase 2 completeness

## Acceptance Criteria

- `dotnet build` passes.
- `dotnet test` passes with Phase 2 tests added.
- Every Phase 2 endpoint returns ProblemDetails with `correlationId` on expected/unexpected errors.
- Non-members cannot detect private projects through list/get/mutation APIs.
- Project Owner/Member/Admin permissions match decisions and do not expose personal-space data.
- Board mutations regenerate `ProjectContextSnapshot.TemplateContent` and broadcast board events after commit.
- Presence works without DB writes.
- Attachment upload works with local filesystem by default and does not require paid/external services.
- S3-compatible configuration is present and disabled unless explicitly selected.
- Card relationships reject cycles and cross-project edges.
- Archiving cards/projects follows `ArchivedAt` semantics and leaves hard deletion to future housekeeping.
- Audit log entries exist for all project-space mutations.

## Reviewer Notes

Reviewer should verify this spec against:

- Phase 2 checklist in `docs/functional-spec.md`.
- Clean Architecture and realtime/error rules in `docs/architecture.md`.
- Entity shape and archive semantics in `docs/data-model.md`.
- Settled decisions in `docs/DECISIONS.md`.

Known intentional choices:

- UI work is excluded because Phase 3/4 own Web/TUI.
- LLM/chat behavior is excluded except project archive dependency on chat archive abstraction; full project chat is Phase 7.
- Local FS is default for attachments; S3 is opt-in for open-source/self-hosted friendliness.
- `Project.ArchivedAt` is required for Phase 2; add it with EF migration, not hand SQL.
- Minimal `ChatArchiveService.ArchiveFolder` is required only for project archive cascade; broader chat feature work remains Phase 7.
