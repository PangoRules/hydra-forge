## Validate: Notification Trigger Points (Plan 7)

7 triggers across 4 services. Automated tests cover NotifyRequest recording + actor-skip; manual covers real wiring end-to-end.

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
5. **Dependency resolved** — User A moves card #2 (blocker of #1) to Archived → User B (assignee of #1) gets notification: "#1 is no longer blocked"
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
