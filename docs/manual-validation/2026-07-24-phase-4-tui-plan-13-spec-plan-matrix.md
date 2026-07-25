## Validate: TUI Spec + Plan Viewer/Editor

### Setup
- [ ] Run API and TUI with a valid user session.
- [ ] Open a card containing one spec, one editable plan, and one Done plan; give each document a non-empty description.
- [ ] Set `$EDITOR` or `$VISUAL` to a working terminal editor.

### Happy Path
1. Press `s` on card detail → Specs/Plans chooser appears.
2. Select `Specs` → card specs load with title, type, version, and updated timestamp.
3. Select a spec and press `Enter` → escaped markdown content renders without markup errors; any content beyond 1,000 characters is truncated with `...`.
4. Press `e`, change spec content, and save editor → API persists new content while title and description remain unchanged; list reloads with incremented version.
5. Return with `Esc` → card detail reloads and renders.
6. Reopen viewer, select `Plans` → card plans load with Pending/Active/Done status badges.
7. Press `e` on an editable plan, change content, and save → API persists new content while title and description remain unchanged; list reloads with incremented version.
8. Press `c` in Specs mode and enter valid type/title/content → spec is created and appears in refreshed list.
9. Press `c` in Plans mode and enter valid title/content → plan is created at next list position and appears in refreshed list.

### Edge Cases
1. Open a card with no specs/plans → empty-state message plus `c` and `Esc` hints render.
2. Press `e` on a Done plan → editor does not launch; `Cannot edit a completed plan.` appears; version remains unchanged.
3. Open an editable document, save without changing content → no update request occurs; `No changes.` appears; version remains unchanged.
4. Unset `$EDITOR`/`$VISUAL` or configure a failing editor → inline content prompt appears; submitted changes can still save.
5. Put `[`/`]` and other Spectre markup characters in title/content → viewer escapes them and does not crash or apply unintended styling.
6. Stop API before loading/creating/updating → error is captured; TUI remains running and accepts further input.

### Regressions
1. Press `Esc` from Specs and Plans modes → returns to same card detail, not board/project list.
2. Press `d` from card detail → dependency panel still opens and returns normally.
3. Edit card title/description from card detail → existing card editor behavior remains unchanged.
4. Navigate spec/plan list with `j`/`k` and arrow keys → selection stays within list bounds.

### Cleanup
- [ ] Restore edited spec/plan content and descriptions if needed.
- [ ] Remove test documents through supported API/UI cleanup flow.
