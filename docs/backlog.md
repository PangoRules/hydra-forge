# Backlog

Ideas and enhancements outside the current phase scope. Not committed — revisit during phase planning.

- **Configurable default Kanban columns** — `ProjectService.DefaultColumnNames` at `src/HydraForge.Application/Projects/ProjectService.cs:15` is a static array. Make it admin-configurable via `SystemSettings` or a new `ProjectTemplate` entity so each project can have custom default columns (name, order, WIP limits, color).
