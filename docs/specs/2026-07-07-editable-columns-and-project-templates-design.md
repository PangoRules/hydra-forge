# Editable Columns + Project Templates — Design

**Date:** 2026-07-07
**Status:** Approved (design phase) — pending implementation plan

## Problem

Board columns cannot be renamed, added, or deleted from the UI. The backend already supports full CRUD (`ColumnsController`: Create/Update/Delete/Reorder), but no web-ui component calls it beyond drag-reorder. Separately, the hardcoded default column set (`Backlog, Spec-ing, Planned, In Dev, In Review, Done`) is software-delivery-specific, which works against HydraForge's goal of being usable by non-dev audiences too.

## Decisions

- Column management ships as **project templates at creation** (Software / General / Blank) + **post-creation editing** (rename/color/WIP/delete/add), not just one or the other.
- Templates are **not persisted** on `Project` — they only seed initial columns at creation time. No schema change, no migration.
- Column deletion is **blocked** (not cascaded/reassigned) when the column has cards — backend already enforces this via `DomainErrorCodes.Columns.DeleteNonEmpty` in `ColumnService.DeleteAsync`; the UI surfaces the existing error, no new guard needed.
- Editing UI is **split by viewport**, matching the existing desktop-kanban/mobile-list divergence already in the codebase (`BoardView.vue` vs `BoardMobileList.vue`): desktop gets an inline popover per column, mobile gets a dedicated "Manage Columns" modal.
- Default pre-selected template is **General** — the neutral, non-dev-flavored option — supporting the "make this broader" goal. Existing Software-style naming remains available, just not the default.

## Templates

Owned server-side (`HydraForge.Application`, not Domain — creation-time convenience, not a persisted domain concept) so the future TUI project-creation flow reuses the same definitions:

| Template | Columns |
|---|---|
| Software | Backlog, Spec-ing, Planned, In Dev, In Review, Done |
| General | Backlog, In Progress, Review, Done |
| Blank | To Do, Done |

## Backend changes

- New `ColumnTemplate` enum (`Software = 1, General = 2, Blank = 3`) in `HydraForge.Application`.
- New static `ColumnTemplates` map: `ColumnTemplate → string[]`, replacing the hardcoded `DefaultColumnNames` array in `ProjectService.cs`.
- `CreateProjectCommand` gains a `Template` field, default `General`.
- `ProjectService.CreateAsync` seeds columns from `ColumnTemplates.Get(cmd.Template)` instead of the hardcoded array.
- No changes to `ColumnsController`, `ColumnService`, `Column` entity, or the database schema — Create/Update/Delete/Reorder and the non-empty-delete guard already exist and are reused as-is.

## Frontend changes

**New Project modal:** add a `Template` select (Software / General / Blank), default General. Value passed through in the create-project API call.

**Desktop (`ColumnHeader.vue`):** hover-revealed gear icon next to the drag handle opens a `UPopover` with Name, Color, WIP limit, and a Delete button. Save → `PUT columns/{id}`; Delete → `DELETE columns/{id}`, surfacing the server's `DeleteNonEmpty` message via toast on failure. A trailing `+ Add Column` control at the end of the column row opens a minimal inline form (name required, color/WIP optional) → `POST columns`.

**Mobile:** new `ColumnManageModal.vue`, reachable from the `BoardMobileList.vue` toolbar. Lists all columns with drag-handle reorder (reuses `useColumnReorder`), tap-to-edit fields inline, per-row delete (same server-validated block), and a trailing `+ Add Column` row.

**API usage:** every new call site uses `useApi()` wrapped in try/catch per D-40 — no bare `const { error } = await api...` without a surrounding try/catch. Failures toast via `useToast().add()`.

**Routes:** any new column endpoints referenced from the frontend go through `ApiRoutes.Columns.*` in `app/lib/routes.ts` — no inline path strings.

## Testing

- xUnit (Application): `ProjectService.CreateAsync` produces the correct column set for each of the three templates.
- Vitest (component): `ColumnHeader` popover open/edit/save/delete flow; new `ColumnManageModal` mobile flow; New Project modal template-select wiring and default value.
- No new EF model test — no schema change.

## Out of scope

- AI-driven or automatic column suggestions.
- Persisting which template a project was created from.
- Per-card-type column visibility rules.
- TUI implementation of column management (tracked separately when TUI phase starts; the template map is designed to be reused there, not built there now).
