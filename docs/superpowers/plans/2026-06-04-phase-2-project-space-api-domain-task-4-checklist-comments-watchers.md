# Task 4: Checklist and comment APIs, mention extraction, CardWatcher auto-add
**Branch:** `task/checklist-comments-watchers`
**Parent branch:** `feat/phase-2-project-space-api-domain`
**Parent spec:** `2026-06-04-phase-2-project-space-api-domain-design.md`

**Goal:** Ship checklist and comment APIs, mention extraction against enabled users, comment archive, watcher auto-add.

**Files:** Modify/read `ChecklistItem.cs`, `Comment.cs`, `CardWatcher.cs`, `User.cs`, `DomainErrorCodes.cs`, `ProblemDetailsMapper.cs`, `Program.cs`. Create `src/HydraForge.Application/Checklist/*`, `src/HydraForge.Application/Comments/*`, `src/HydraForge.Infrastructure/Checklist/*`, `src/HydraForge.Infrastructure/Comments/*`, `src/HydraForge.Server/Controllers/Projects/CardChecklistController.cs`, `CardCommentsController.cs`, tests, smoke `src/HydraForge.Server/HttpTests/ChecklistComments.http`.

## Steps

- [ ] Write Application tests for checklist create/update/delete/toggle/reorder dense positions and optional assignee must be project member and enabled.
- [ ] Write Application tests for comments create/update/archive, `@mention` extraction from `@username`, ignores disabled/archived/non-member users, comment author auto-added to `CardWatcher`, mentioned users included in response as `MentionedUserIds`.
- [ ] Add error codes: `CHECKLIST_ITEM_NOT_FOUND`, `CHECKLIST_INVALID_POSITION`, `CHECKLIST_INVALID_ASSIGNEE`, `COMMENT_NOT_FOUND`, `COMMENT_ARCHIVED`, `MENTION_USER_NOT_FOUND` if exposed.
- [ ] Implement `ChecklistService`, `CommentService`, `MentionExtractor` in Application. `MentionExtractor` pure function returns distinct normalized usernames using regex `(?<!\w)@([A-Za-z0-9_.-]{1,64})`.
- [ ] Implement EF repositories. Transactions for reorder/delete compaction and comment+watcher insert.
- [ ] Write Server tests for `/api/projects/{projectId}/cards/{cardIdOrNumber}/checklist` CRUD/toggle/reorder and `/comments` CRUD/archive.
- [ ] Implement controllers. Return `ProblemDetails` on expected failures.
- [ ] Update mapper.
- [ ] Create `src/HydraForge.Server/HttpTests/ChecklistComments.http` covering all checklist + comment endpoints, including mention and archive.
- [ ] Run `dotnet test --filter Checklist`; `dotnet test --filter Comment`; `dotnet test`.
- [ ] Commit: `git add src tests && git commit -m "feat: add checklist and comments"`.

**Acceptance:** watcher rows created idempotently; comments archive via `ArchivedAt`; `.http` covers every endpoint.
