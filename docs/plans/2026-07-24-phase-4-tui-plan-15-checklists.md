# Plan 15: Checklists — Toggle Completion from Keyboard

**Branch:** `task/tui-checklists`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 15

**Goal:** Toggle checklist item completion with Space key from card detail view.

**Depends on:** Task 3 (ApiClientFactory), Task 9 (CardDetailScreen already has checklist display and toggle).

---

## Step 1: Verify existing checklist functionality in `CardDetailScreen`

The `CardDetailScreen` from Task 9 already includes:
- `BuildChecklistPanel()` — renders checklist with [x]/[ ] checkboxes, strikethrough for completed
- `ToggleChecklistItemAsync()` — toggles first incomplete item (or first item), PATCHes API
- `LoadChecklistAsync()` — fetches checklist from API
- `Space` key wired to `ToggleChecklistItemAsync()` when checklist section is active

No additional code needed. This task is already implemented by Task 9.

## Step 2: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 3: Commit

No new code — task is complete from Task 9. Mark as done.