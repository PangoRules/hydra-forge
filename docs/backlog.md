# Backlog

Ideas and enhancements outside the current phase scope. Not committed — revisit during phase planning.

- **Admin-configurable Kanban column templates** — Partially resolved: `ColumnTemplates` (Software/General/Blank) replaced the old static `DefaultColumnNames` array and ships today, but the three templates are still compile-time constants. Still open: let an admin edit/add templates at runtime (via `SystemSettings` or a new `ProjectTemplate` entity) — name, order, WIP limits, color per template.
- **Floating card windows** — replace UModal-based card detail with draggable, resizable windows (one per card). Enables multi-card view (see two cards side-by-side, reference one while editing another). Close per-window. Potential libs: `vue-draggable-resizable`, native `useDraggable` from `@vueuse/core`. Would replace CardModal with a window manager component.
- **Keyboard navigation for projects page** — add `useRovingFocus`-based keyboard nav to the project list table (arrow keys, Enter to open, shortcuts for edit/archive). Currently the board page has full keyboard nav but the projects list doesn't.

