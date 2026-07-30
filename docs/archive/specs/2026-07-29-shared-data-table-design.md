# Shared Data Table + Board Back-Link

**Date:** 2026-07-29
**Status:** Approved, ready for implementation plan
**Discovered during:** manual review of the navigation redesign (nav-redesign spec, same date)

## Problem

Two issues surfaced testing the new sidebar/topbar:

1. The project board page (`/projects/:id/board`) has no way back to the project list except clicking "Projects" in the sidebar — no in-page back-link.
2. The Projects list (`/projects`) and admin Users list (`/admin/users`) both render via `UTable`, but their surrounding chrome has drifted apart: Projects has a rows-per-page selector, an "X-Y of Z" range readout, and a mobile card-view fallback (`ProjectList.vue` + `ProjectCard.vue`); Users has none of these — fixed page size 20, bare `UPagination` + total count, table-only at every viewport width.

## Goals

- Add a back-link on the board page to `/projects`.
- Extract a shared, generic `DataTable` component that owns the table + pagination chrome + responsive card-view fallback, so any current or future paginated list (Projects, Users, and beyond) gets the same look and the same mobile behavior for free.
- Lift Users up to the richer pagination style (rows-per-page selector + range text) rather than simplifying Projects down.
- Give Users a mobile card-view fallback it doesn't have today, using the same mechanism Projects will use.

## Non-goals

- No change to each page's own header (title + primary action button) or filter bar (`ProjectFilterBar` vs. Users' plain search box) — these stay page-owned via a slot, since their content genuinely differs per page.
- No fix for the Projects-vs-Users search-debounce inconsistency (Projects debounces 300ms, Users doesn't) — that's search-input behavior, not table styling, and out of scope here.
- No change to `useMediaQuery`-driven default `pageSize` logic already in `projects/index.vue`.

## Architecture

### Back-link

`app/pages/projects/[id]/board.vue`'s existing header bar (around line 261) gets a `UButton icon="i-lucide-arrow-left" variant="ghost" :to="UiRoutes.Projects.List"` placed immediately before the `<h1>`. Requires adding `UiRoutes` to the existing `import { ApiRoutes } from '~/lib/routes'` line. No new files.

### `DataTable` component

New: `app/components/shared/DataTable.vue` — a generic component (`<script setup lang="ts" generic="T">`) alongside the existing cross-cutting components in `app/components/shared/` (`ConfirmDialog.vue`, `BulkActionBar.vue`).

**Props:**
```ts
defineProps<{
  data: T[]
  columns: TableColumn<T>[]
  loading: boolean
  page: number
  pageSize: number
  totalCount: number
  pageSizeOptions?: number[]   // default [10, 20, 50]
  rowKey: (item: T) => string  // for :key in the card-view v-for
}>()

defineEmits<{
  'update:page': [number]
  'update:pageSize': [number]
  'select': [T]
}>()
```

**Slots:**
- Cell templates forwarded to the inner `UTable` by dynamic passthrough: `<template v-for="(_, name) in $slots" #[name]="slotProps"><slot :name="name" v-bind="slotProps" /></template>` — excludes the reserved `card` slot name from this forwarding loop (handled separately below).
- `#card="{ item }"` — optional. When provided, the component renders a `md:hidden` responsive card list below the (now `hidden md:block`) table, one `<slot name="card" :item="item" />` per row. When absent, the table renders at all widths (no card view) — safe default for any future consumer that doesn't need one.

**Owned internally (not slotted):**
- Loading state: a single centered spinner block, shown instead of both table and card view when `loading` is true and `data` is empty (matches today's `ProjectList.vue` loading block, now the single shared implementation instead of one per consumer).
- Empty state: "No results" block shown when not loading and `data.length === 0`.
- Pagination footer: rows-per-page `USelect` + "X-Y of Z" range (computed internally from `page`/`pageSize`/`totalCount`) + `UPagination`, rendered once beneath whichever view (table or card) is visible — exact markup lifted from the current `projects/index.vue` footer (`app/pages/projects/index.vue:189-213` as it exists today), so this is a lift-and-shift of already-approved UI, not new design.

### Consumers

**`app/components/project/ProjectListTable.vue`** — keeps its Project-specific `columns` array and desktop cell templates (`#name-cell`, `#myRole-cell`, `#createdAt-cell`, `#actions-cell`), now wrapping `<DataTable>` instead of `<UTable>` directly, and adds `#card="{ item }"` rendering the existing `ProjectCard.vue` unchanged (`:project="item"` plus its existing `@select`/`@toggle-archive`/`@edit` passthrough). `app/components/project/ProjectList.vue` is deleted — superseded by `DataTable`'s built-in card view. `app/pages/projects/index.vue` drops its `hidden md:block` / `md:hidden` dual-render (`ProjectListTable` + `ProjectList` side by side) and the hand-rolled pagination footer block (rows-per-page select, range computeds, `UPagination`) — replaced by passing `page`/`pageSize`/`totalCount` down through `ProjectListTable` into `DataTable`.

**`app/pages/admin/users.vue`** — table/pagination markup replaced by a single `<DataTable>` call (columns stay inline, as they are today — no separate `UserListTable.vue` needed for a page this small). New `#card="{ item }"` slot: a stacked mobile card layout — username + name, email, role badge (`isAdmin`), status badge (`isDisabled`), and the three action buttons (Disable/Enable, Make/Remove Admin, Reset Password) stacked or wrapped. Gains the rows-per-page selector and range text it lacks today, and a fixed `pageSize` ref replaced with a reactive one matching Projects' pattern (still no `useMediaQuery`-based dynamic default — Users doesn't need that nuance, a flat default of 20 is fine).

## Testing

- `DataTable.test.ts`: renders table view at default; renders card view via `#card` slot and hides table (`hidden md:block` class present); pagination range text computed correctly (e.g. page 2, pageSize 10, totalCount 25 → "11-20 of 25"); rows-per-page `USelect` emits `update:pageSize`; `UPagination` emits `update:page`; loading state shows spinner and hides both views; empty state shows when `data` is empty and not loading.
- `ProjectListTable.test.ts` (existing, if present — otherwise new): confirms it still renders Project columns/cells and now also a `ProjectCard` per row via `DataTable`'s card slot.
- `admin/users.test.ts` (new): confirms the mobile card slot renders username/email/role/status and the three action buttons, and clicking each calls the same handlers as the desktop cells.
- Delete any existing `ProjectList.test.ts` (component removed).

## Self-Review

**Placeholder scan:** none — every section has concrete prop/slot signatures and file paths.
**Internal consistency:** `DataTable`'s pagination footer is stated as a lift-and-shift of exact existing markup (cites the source line range) rather than new design, avoiding ambiguity about styling.
**Scope check:** Single cohesive unit — one new component, two consumer rewrites, one file deletion, one one-line addition (back-link). Not decomposed further; the writing-plans step will order these as sequential tasks (component first, since both consumers depend on it).
