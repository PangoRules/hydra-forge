# Board Filtering & Quick-Add — Design Spec

**Date:** 2026-06-24
**Status:** Draft
**Phase:** 3 (Web UI) — extends Plan 3 (Card Modal Core)

## 1. Overview

Add filter controls and quick-add card button to the board view (desktop + mobile).
Backend already supports `includeArchived`, `type`, `assigneeUserId`, `columnId` query params
on `GET /api/projects/{id}/Cards` — the frontend just never passed them.

**What changes:**
- Global filter bar above columns (desktop) / slide-out panel (mobile)
- Per-column filter controls in column headers (desktop only)
- `+ Add` / `+ Card` button for quick card creation
- Store-level filter state wiring

## 2. Backend — What Exists

`CardsController.List` already accepts:

| Param | Type | Default |
|---|---|---|
| `columnId` | `Guid?` | `null` (all columns) |
| `includeArchived` | `bool` | `false` |
| `assigneeUserId` | `Guid?` | `null` |
| `type` | `CardType?` | `null` |

No backend changes needed for card filtering. `ColumnsController.List` returns all columns
with no archive filter — archiving columns is a future feature (schema needs `ArchivedAt`).

## 3. Desktop Board Layout

```
┌─────────────────────────────────────────────────┐
│ [Search across board...] [Type ▼] [☐ Archived] [+ Card] │ ← global bar
├─────────────────────────────────────────────────┤
│ ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│ │● Backlog 2│  │● Progress│  │● Done 1  │       │
│ │Type Arch ⋮+│  │Type Arch ⋮+│  │Type Arch ⋮+│       │ ← column header row 1
│ │[Filter..] │  │[Filter..] │  │[Filter..] │       │ ← column header row 2
│ │ ┌──────┐  │  │ ┌──────┐  │  │ ┌──────┐  │       │
│ │ │Card 1│  │  │ │Card 3│  │  │ │Card 6│  │       │
│ │ │Card 2│  │  │ │Card 4│  │  │ └──────┘  │       │
│ │ └──────┘  │  │ └──────┘  │  │          │       │
│ └──────────┘  └──────────┘  └──────────┘       │
└─────────────────────────────────────────────────┘
```

### 3.1 Global Top Bar

Controls (left to right):
- **Search input** — text search across all columns (filters card title). Hides columns with zero matches.
- **Type dropdown** — "All", "Task", "Bug", "Epic", "Spec", "Idea". Filters all columns.
- **Archived toggle** — checkbox "Archived". Default OFF. Shows archived cards across all columns.
- **Hide empty** — checkbox "Hide empty columns". Default OFF. When ON, columns with zero visible cards (after all filters applied) are hidden from the board. When OFF, all columns display even if empty.
- **+ Card** — opens card creation form with **column picker dropdown** (user must select target column).

All global filters AND with per-column filters.

### 3.2 Column Header (per-column)

Two rows per column:

**Row 1:** `[color dot] [Column Name] [count badge]` ...spacer... `[Type ▼] [Arch ☐] [⋮] [+ Add]`

**Row 2:** `[Filter cards in this column...]` (full-width text input)

Controls:
- **Type ▼** — filter this column by card type. Independent from global type filter.
- **Arch ☐** — show archived cards in this column only. Independent from global archived toggle.
- **⋮** — column actions dropdown: Sort by (position/date/title/number), Rename column, Archive column, WIP limit.
- **+ Add** — create card in this column. Column is **preselected** (read-only in form).
- **Filter input** — text search within this column only.

### 3.3 + Card Flow

Three entry points, one form:

| Entry point | Column field |
|---|---|
| Per-column + Add | Preselected, read-only |
| Global + Card | User picks from dropdown |
| Mobile + | User picks from dropdown |

The create-card form is shared — just the column selector changes behavior.

## 4. Mobile Board Layout

Uses `BoardMobileList` (stacked columns, not kanban).

```
┌───────────────────────┐
│ [Search...] [Filter] [+]│ ← global bar
├───────────────────────┤
│ Filters: [Type ▼] [☐ Arch]│ ← collapsible panel (shown when Filter tapped)
├───────────────────────┤
│ ● Backlog (2)      ▼ │ ← expanded column
│   ┌───────────────┐   │
│   │ #42 Fix login  │   │
│   │ #43 Dark mode  │   │
│   └───────────────┘   │
├───────────────────────┤
│ ● In Progress (3) ▶ │ ← collapsed column
├───────────────────────┤
│ ● Done (1)        ▼ │ ← expanded column
│   ┌───────────────┐   │
│   │ #41 Set up CI  │   │
│   └───────────────┘   │
└───────────────────────┘
```

### 4.1 Mobile Controls

- **Search** — same as desktop: filters across all columns.
- **Filter button** — toggles slide-out bar with Type dropdown + Archived checkbox + Hide empty checkbox.
- **+ button** — opens card creation form with column picker.
- **Columns** — accordion style. Tap header to expand/collapse. Arrow (▼/▶) shows state.
- **Per-column controls** — dropped on mobile (too cramped). Only global filters.

## 5. Board Store Changes

`useBoardStore` gains:

```ts
// Filter state
boardFilters: Ref<BoardFilters>

interface BoardFilters {
  search: string            // global text search
  type: CardType | null     // global type filter
  includeArchived: boolean  // show archived cards
  hideEmptyColumns: boolean // hide columns with zero visible cards
}

// Per-column filter state (local to BoardColumn component)
columnSearch: Ref<string>
columnType: Ref<CardType | null>
columnArchived: Ref<boolean>
```

`fetchBoard(projectId, filters?)` — passes query params to `GET /api/projects/{id}/Cards`:
- `type` → `CardType?` query param
- `includeArchived` → `bool` query param
- Column-level filters applied client-side from already-fetched cards (or re-fetched per-column)

Decision: **fetch all cards once** for the board, apply column-level filters client-side
against cards already in memory. This avoids N+1 API calls and keeps filtering instant.

**Filter persistence:** When `fetchBoard` is re-called (after archive/restore/move),
the current filter state is preserved — `boardFilters` is not cleared.

## 6. Implementation Steps

### 6.1 Board Store — filter state + fetchBoard params

- Add `boardFilters` ref with search, type, includeArchived
- `fetchBoard` passes type + includeArchived as query params
- Add text search client-side post-fetch (filter by title/cardNumber)

### 6.2 Global Top Bar Component

- New: `BoardFilterBar.vue`
- Props: global filter state (v-model)
- Emits: `update:filters`, `add-card`
- On filter change → store filters → `fetchBoard`

### 6.3 Column Header Updates

- Modify: `ColumnHeader.vue` → add Type dropdown, Arch toggle, ⋮ menu, + Add
- New: `ColumnFilterRow.vue` → inline search input
- Per-column filters are local state, not stored globally

### 6.4 + Add Card Flow

- Reuse existing card creation (if available) or create simple inline form
- Column prop: `preselected: boolean`, `columnId: string | null`
- Desktop + on column: preselected=true, columnId filled
- Global/Mobile +: preselected=false, column picker shown

### 6.5 Mobile Accordion

- Modify: `BoardMobileList.vue` → columns become collapsible sections
- New: `FilterSlideOut.vue` — collapsible panel with Type + Archived
- Column state: `expandedColumns: Set<string>` tracking which are open

### 6.6 Column ⋮ Menu

- Dropdown actions: Sort, Rename, Archive column, WIP limit
- Sort: reorders cards client-side (position, due date, title, card number)
- Rename/WIP: opens inline edit or modal
- Archive column: future feature (needs `ArchivedAt` on Column entity)

## 7. Tests

- Board store: filter params passed to API, client-side text search
- BoardFilterBar: filter state propagation, type dropdown values, archived toggle
- ColumnHeader: per-column filter state, + Add preselected flow
- BoardMobileList: accordion expand/collapse, filter panel toggle
- + Card form: column picker, preselected state

## 8. Out of Scope

- **Archive columns** — needs `ArchivedAt` on Column entity (schema migration), backend endpoint, not in this spec
- **Column ⋮ menu full actions** — sort only for now; rename/archive/WIP deferred
- **Server-side text search** — client-side only (title/cardNumber match)
- **Assignee filter** — backend supports it, frontend UI deferred to Plan 6 (polish)