# Backlog

Ideas and enhancements outside the current phase scope. Not committed — revisit during phase planning.

- **Configurable default Kanban columns** — `ProjectService.DefaultColumnNames` at `src/HydraForge.Application/Projects/ProjectService.cs:15` is a static array. Make it admin-configurable via `SystemSettings` or a new `ProjectTemplate` entity so each project can have custom default columns (name, order, WIP limits, color).
- **Floating card windows** — replace UModal-based card detail with draggable, resizable windows (one per card). Enables multi-card view (see two cards side-by-side, reference one while editing another). Close per-window. Potential libs: `vue-draggable-resizable`, native `useDraggable` from `@vueuse/core`. Would replace CardModal with a window manager component.

