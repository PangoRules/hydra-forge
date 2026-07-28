## Validate: Notification Trigger Points (Plan 7)

7 triggers across 4 services. Automated tests cover NotifyRequest recording + actor-skip; manual covers real wiring end-to-end.

### Code review findings (triggers 4–7, prior to manual pass)

- **Trigger 4 (@mention)** — backend already fully implemented (`CommentService.cs`): regex-parses `@username` from raw comment text, resolves to project members only, notifies independently of the watcher notification. **No autocomplete/suggestion UI exists yet** in `CardComments.vue` (plain textarea) — to test, type the literal text `@UserB` by hand; the backend doesn't need any client-side mention markup.
- **Trigger 5 (dependency resolved) — was broken, now fixed.** The notify call was wired into `CardService.MoveAsync`, but only `Archive()` sets `ArchivedAt`, and Move never does — so the "last active blocker" check always saw the moved card itself as still-active and never fired, regardless of which column it moved to (there's no "Archived column" concept in this codebase). Fixed by moving the call into `CardService.ArchiveAsync` (the actual place `ArchivedAt` gets set). Added two regression tests (`ArchiveAsync_ArchivingLastActiveBlocker_NotifiesBlockedCardAssignees`, `ArchiveAsync_OtherActiveBlockerRemains_DoesNotNotify`) — this had zero automated coverage before, which is why the wiring bug went unnoticed. **To test this trigger: archive card #2 via the Archive action (not by moving it to any column) — there is no "Archived" pseudo-column.**
- **Triggers 6 & 7 (project archived / updated)** — confirmed working as specced, including edge case 5 (single-field edits still notify).

### Setup

- [x] Docker Compose up (postgres + minio)
- [x] Server running (`dotnet run --project src/HydraForge.Server`)
- [x] Web UI or TUI running (or use raw HTTP / `.http` file)
- [x] Two users exist (User A, User B) — both members of a test project
- [x] Test project has at least one card (#1) assigned to User B
- [ ] User B is watcher on card #1
- [ ] Card #1 has a "blocked by" relationship from another card (#2) in the same project
- [ ] User A is the actor for all actions

### Happy Path — 7 Triggers

1. **Card moved (User B watcher/assignee)** — User A moves card #1 to a new column → User B gets notification: "{UserA} moved #1 to {column}"
2. **Card assigned** — User A assigns card #1 to User B → User B gets notification: "{UserA} assigned you to #1"
3. **Comment added** — User A adds comment to card #1 → User B (watcher) gets notification: "{UserA} commented on #1"
4. **@mention in comment** — User A adds comment "@UserB look at this" → User B gets notification: "{UserA} mentioned you in #1"
5. **Dependency resolved** — User A archives card #2 (blocker of #1, via the Archive action, not a column move) → User B (assignee of #1) gets notification: "#1 is no longer blocked"
6. **Project archived** — User A archives the test project → every project member gets notification: "{project} has been archived"
7. **Project updated** — User A edits project name/description → every project member gets notification: "{project} was updated by {UserA}"

### Edge Cases

1. **Actor self-exclusion** — User A triggers any notification (e.g. moves card, adds comment) → User A does NOT receive their own notification (actor==recipient is skipped by NotificationService)
2. **Card move without watchers/assignees** — Card with no assignees or watchers moves → no crash, no notification sent
3. **Comment without @mention** — Comment without @username → no @mention notification generated (watcher notification still fires)
4. **Card move without resolved blockers** — Card moves but its blockers are still active → no "unblocked" notification
5. **Empty project update** — Update project with only name change → notification fires for all members (still counts as update)

### Regressions

1. **Card CRUD still works** — Create, move, assign, comment on cards returns 200 and produces no unhandled server errors
2. **Project CRUD still works** — Create, update, archive, unarchive project returns 200
3. **Existing NotificationService behavior** — Mark-as-read, list, count-unread all unchanged
4. **Existing SignalR notifications** — Previous notification types pushed via NotificationHub still work

### Cleanup

- [ ] No special cleanup required — test data can remain
