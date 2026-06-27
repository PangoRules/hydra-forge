# Manual Validation Matrix — Card Modal Hardening

## Setup
- [X] API server running locally (`ASPNETCORE_ENVIRONMENT=Development`)
- [X] Web dev server running (`pnpm dev` at `src/web-ui`)
- [X] Logged in, project with cards created

## Task 1 — Error Toasts on useApi() Failure

### Card Modal Archive Error
1. Open a card modal → click Archive → confirm → observe toast "Card archived" (happy path)
2. Stop API server → open card modal → click Archive → confirm → observe red error toast "Failed to archive card" (previously: silent nothing)
3. Restart API server → verify happy path still works
Validated this works as expected.

### Card Modal Restore Error
1. Open archived card modal → click Restore → observe toast "Card restored" (happy path)
2. Stop API server → open archived card modal → click Restore → observe red error toast "Failed to restore card" (previously: silent nothing)
3. Restart API server → verify happy path still works
Validated this works as expected.

### Board Card Archive Error
1. From board view → three-dot menu on card → Archive → confirm → observe toast "Card archived" + card disappears (happy path)
2. Stop API server → three-dot menu → Archive → confirm → observe red error toast "Failed to archive card" (previously: silent nothing)
3. Restart API server → verify happy path still works
Validated this works as expected.

### Board Card Restore Error
1. Enable "Include archived" filter → three-dot menu on archived card → Restore → observe toast "Card restored" + card reappears (happy path)
2. Stop API server → three-dot menu on archived card → Restore → observe red error toast "Failed to restore card" (previously: silent nothing)
3. Restart API server → verify happy path still works
Validates this works as expected.

### Card Create Error
1. Click "Add card" → fill form → click Create → observe toast "Card created" + card appears on board (happy path)
2. Stop API server → click "Add card" → fill form → click Create → observe red error toast with message (previously: modal just closed silently)
3. Restart API server → verify happy path still works
Validated this works as epxected.

## Task 2 — Version Ownership (requires Plan 4 Task 16 for full test)

1. Open card modal → type in description → click Save → description saves without error
2. Verify no console errors when saving description multiple times in same modal session
3. Close and re-open modal → description matches saved value

## Task 3 — Shared Utilities & Type Filters

1. Card create modal Type dropdown shows options: Task, Bug, Epic, Spec, Idea
Validated
2. Board filter bar Type dropdown shows: All, Task, Bug, Epic, Spec, Idea
Validated
3. Column header Type dropdown (mobile + desktop) shows: All, Task, Bug, Epic, Spec, Idea
Validated
4. Select "Bug" filter → only Bug cards visible
Validated with Task type
5. Select "All" → all cards visible again
Validated
6. Due date shown on cards formatted as "Mar 15" (short month + day)
Working
7. Past due date card shows red text "text-red-500"
Working

## Task 4 — Accessibility

1. Open card modal → edit description → click Save → screen reader should hear "Saving description…" (visible only to assistive tech via `.sr-only`)
2. Stop API server → edit description → click Save → observe error text "Failed to save" rendered with `role="alert"` (inspect DOM for `[role="alert"]`)

## Regressions

1. Card modal opens, loads card data, displays description
2. Board view shows all cards with correct type icons/labels
3. Board filter bar filtering works across all type, assignee, archive, search options
4. Board mobile list type filter works
5. Build: `cd src/web-ui && pnpm build` succeeds
