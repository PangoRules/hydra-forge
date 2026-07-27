# Backlog

Ideas and enhancements outside the current phase scope. Not committed — revisit during phase planning.

- **Admin-configurable Kanban column templates** — Partially resolved: `ColumnTemplates` (Software/General/Blank) replaced the old static `DefaultColumnNames` array and ships today, but the three templates are still compile-time constants. Still open: let an admin edit/add templates at runtime (via `SystemSettings` or a new `ProjectTemplate` entity) — name, order, WIP limits, color per template.
- **Floating card windows** — replace UModal-based card detail with draggable, resizable windows (one per card). Enables multi-card view (see two cards side-by-side, reference one while editing another). Close per-window. Potential libs: `vue-draggable-resizable`, native `useDraggable` from `@vueuse/core`. Would replace CardModal with a window manager component.
- **Keyboard navigation for projects page** — add `useRovingFocus`-based keyboard nav to the project list table (arrow keys, Enter to open, shortcuts for edit/archive). Currently the board page has full keyboard nav but the projects list doesn't.
- **Per-user notification preferences** — Plan 7 (`docs/plans/2026-07-25-phase-5-notifications-admin-plan-7-notification-triggers.md`) wires 7 fixed triggers (card move/assign, comment, @mention, dependency resolved, project archive/update) with no opt-out. Every recipient in the computed set (assignees, watchers, or all project members depending on trigger) always gets notified — no way for a user to mute a card, a project, or a trigger type. Would need a `NotificationPreference` entity (per-user, possibly per-project/per-card) checked inside `NotificationService.NotifyAsync` before the DB write + ntfy push.

