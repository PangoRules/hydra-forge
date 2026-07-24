# Project List Redesign — Design

**Date:** 2026-07-07
**Status:** Approved
**Replaces:** `docs/plans/2026-06-25-phase-3-plan-7-project-management-ui.md` (that plan was ~95% already shipped under a different implementation — edit modal, member picker, list polish all exist. This is what actually remains for Task 7.)

## Problem

`ProjectsController.List` / `ProjectService.GetAllAsync` load every project the requesting user is a member of, unbounded — no `skip`/`take`, no search, no sort. The frontend renders all of them as a card grid with no pagination. Works fine at a handful of projects; breaks down (slow load, no way to find anything) once a user has dozens.

## Decisions

- **Desktop: table layout.** Columns: Name (+ archived badge), Members, My Role, Created, Actions. Scales far better than a card grid once there are many rows.
- **Mobile: keep the existing `ProjectCard` grid**, just paginated — matches the board's own desktop-table/mobile-list split pattern (`BoardView.vue` vs `BoardMobileList.vue`), and avoids deprecating `ProjectCard.vue`, which is otherwise fully built and correct (archived badge, member count, three-dot menu).
- **Filters:** text search (name + description, debounced, server-side), archived toggle (already exists, unchanged), sort (Name / Created / Updated, asc/desc), role filter (All / Owner / Member — "only projects I own").
- **Pagination, not infinite scroll.** Real `skip`/`take` with a total count, default page size 20.
- Search/sort/pagination move server-side (EF query-level `WHERE`/`ORDER BY`/`Skip`/`Take`), not in-memory — the current `ListByUserIdAsync` loads the full list into memory before filtering by ID, which is the root of the "unbounded" problem this fixes.

## Backend changes

**`IProjectRepository.ListByUserIdAsync`** gains: `search: string?`, `sortBy: ProjectSortField` (`Name | CreatedAt | UpdatedAt`, default `CreatedAt`), `sortDescending: bool` (default `true`), `role: MemberRole?` (filter to the requester's own role), `skip: int` (default `0`), `take: int` (default `20`, clamp max `100`). Returns `(IReadOnlyList<Project> Items, int TotalCount)`.

EF implementation (`EfProjectRepository.ListByUserIdAsync`): join `ProjectMembers` for both the requester's project-ID scoping (existing) and their per-project `Role` (new — needed for the role filter and to return "my role" per row); case-insensitive search via `EF.Functions.ILike(p.Name, $"%{search}%") || EF.Functions.ILike(p.Description, ...)`; `OrderBy`/`ThenBy` on the resolved sort field; `Skip`/`Take` after ordering; a separate `CountAsync` (or `Skip(0).Take(0)`-style combined query) for `TotalCount`.

**`ProjectService.GetAllAsync`** passes these through, adds `MyRole` (the requester's `MemberRole` for that project) to `ProjectListDto`.

**`ProjectsController.List`** query params: `includeArchived` (existing), `search`, `sortBy`, `sortDescending`, `role`, `skip`, `take`. Response becomes `ProjectListPageResponse { Items: ProjectListResponse[], TotalCount: int }` — `ProjectListResponse` gains `MyRole`.

## Frontend changes

**`projects/index.vue`**: owns pagination/filter state as local refs (search, sortBy, sortDescending, roleFilter, page, includeArchived) — no new Pinia store needed, this page is the only consumer (matches existing YAGNI convention: board state is a store because many components share it, project-list state isn't). Debounced search (300ms, same pattern as board search). Refetches on any filter/page change via `ApiRoutes.Projects.list()` with query params.

**New `ProjectListTable.vue`** (desktop, `hidden md:block`): `UTable` with the 5 columns above; row click navigates to the project board; Actions column reuses the same Edit/Archive/Restore menu logic currently in `ProjectCard.vue` (extract into a small shared composable or duplicate the two handlers — genuinely tiny, duplication is fine here per YAGNI).

**`ProjectList.vue`** (mobile, `md:hidden`): unchanged internals, just receives the already-paginated/filtered project list instead of the full set.

**New `ProjectFilterBar.vue`**: search input, role dropdown, sort dropdown, archived toggle (moved out of the page template into its own component — it's a self-contained unit of filter controls, same reasoning as `BoardFilterBar.vue`).

**Pagination controls**: simple Prev/Next + "Page X of Y" below both the table and the mobile card grid, driven by `TotalCount` from the response.

## Testing

- xUnit (Application): `ProjectService.GetAllAsync` — search matches name/description, sort orders correctly both directions, role filter scopes correctly, skip/take pages correctly, `TotalCount` reflects the pre-pagination filtered count.
- Vitest (component): `ProjectListTable.vue` renders rows + row click navigation; `ProjectFilterBar.vue` emits correct filter state; `projects/index.vue` debounces search and refetches on filter change.
- No new EF model test — no schema/entity change, only repository query logic.

## Out of scope

- Bulk actions on the project list (multi-select archive, etc.).
- Saved/named filter presets.
- Changing `ProjectCard.vue`'s own content/behavior — it's reused as-is for mobile.
