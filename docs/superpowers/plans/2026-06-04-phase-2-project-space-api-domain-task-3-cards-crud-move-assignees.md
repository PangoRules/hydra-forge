# Task 3: Card CRUD, card numbering, movement, assignees, parent epic links, blocked move warning
**Branch:** `task/cards-crud-move-assignees`
**Parent branch:** `feat/phase-2-project-space-api-domain`
**Parent spec:** `2026-06-04-phase-2-project-space-api-domain-design.md`

**Goal:** Ship card APIs with per-project card numbers, dense positions, assignees/watchers, parent epic validation, archive/delete semantics, blocked-move warning preflight.

**Files:** Modify/read `Card.cs`, `CardAssignee.cs`, `CardWatcher.cs`, `CardRelationship.cs`, `Column.cs`, `DomainErrorCodes.cs`, `HydraForgeDbContext.cs`, `ProblemDetailsMapper.cs`, `Program.cs`. Create `src/HydraForge.Application/Cards/*`, `src/HydraForge.Infrastructure/Cards/*`, `src/HydraForge.Server/Controllers/Projects/CardsController.cs`, tests under `tests/.../Cards`, smoke `http/phase-2/cards.http`.

## Steps

- [ ] Write Domain tests for allowed `CardType` enum values and parent epic helper behavior: parent must be same-project, type `Epic`, no self/ancestor cycle.
- [ ] Add Application tests: create assigns `MAX(CardNumber)+1` never reuses archived/deleted numbers; create appends to target column; list filters by column/archived/assignee/type; get accepts GUID or numeric card number string; update increments `Version`; move compacts old/new columns and sets `MovedAt`; blocked card move without confirm returns warning result with blockers; confirm moves; assign creates `CardAssignee` + `CardWatcher`; duplicate assign returns error; unassign removes assignee only; archive sets `ArchivedAt` and compacts positions; delete hard-deletes only if allowed by spec/service contract.
- [ ] Add error codes: `CARD_NOT_FOUND`, `CARD_ARCHIVED`, `CARD_INVALID_TYPE`, `CARD_INVALID_ASSIGNEE`, `CARD_DUPLICATE_ASSIGNEE`, `CARD_INVALID_PARENT_EPIC`, `CARD_PARENT_CYCLE`, `CARD_BLOCKED_MOVE_WARNING`, `CARD_CONCURRENCY_MISMATCH`.
- [ ] Implement `CardService` and repository ports. Keep dependency-cycle/blocked lookup as Application logic over repository graph data.
- [ ] Implement EF repository with transactions for create/move/archive/delete position compaction. Keep manual `ArchivedAt == null` filters.
- [ ] Update EF model tests for `(ProjectId, CardNumber)` uniqueness, `(ColumnId, Position)` index if added, `CardAssignee(CardId,UserId)` uniqueness, `CardWatcher(CardId,UserId)` key.
- [ ] Write Server tests for `/api/projects/{projectId}/cards`, `/cards/{cardIdOrNumber}`, `/move`, `/assignees`, auth, member-only, ProblemDetails.
- [ ] Implement controller records: create/update/move/assign/unassign. Return warning payload with 409 or 200 warning contract; document in tests and `.http`.
- [ ] Update `ProblemDetailsMapper`.
- [ ] Create `http/phase-2/cards.http`: create epic, create task, list filters, get by number, update, assign, unassign, blocked move preflight, confirmed move, archive, delete.
- [ ] Run `dotnet test --filter Card`; run `dotnet test`.
- [ ] Commit: `git add src tests http && git commit -m "feat: add card API"`.

**Acceptance:** card numbers sequential per project; all position lists dense; parent epic cycles rejected; `.http` covers every card endpoint.
