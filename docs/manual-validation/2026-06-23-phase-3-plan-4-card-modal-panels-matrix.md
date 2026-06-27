# Manual Validation Matrix — Card Modal Panels (Plan 4)

## Setup
- [x] API server running locally (`ASPNETCORE_ENVIRONMENT=Development`)
- [x] Web dev server running (`pnpm dev` at `src/web-ui`)
- [x] Logged in, project with cards and at least 2 members
- [x] At least one card with parent epic available (for metadata editor parent pick)
- [x] MinIO running (attachment upload needs it)

## Task 12 — Card Checklist

1. Open any card modal → checklist panel visible in desktop right sidebar
Valid it's working
2. Type "Test item 1" in add field → click Add → item appears in list, input clears
Typed asdf appeared both in right panel and in the checklist menu item after selecting it. (not sure how to describe that this worked as expected)
3. Add 3+ items → verify progress bar updates (0% → X/N as you complete)
Yep I have 8 items where 3 are completed, it shows 3/8 and the green progress is updated
4. Click checkbox on item 1 → item shows strikethrough, progress % increases
Correct clicking item 1 strikethrugh happens and progress increases. There is a weird thing where it doesn't look smooth but it does happens smooth, not sure if am explaining, could be more smoother, that is what I was trying to say wihtout being dispectful
5. Stop API server → click checkbox on item 2 → checkbox reverts, red error toast "Failed to update item"
Yep I got Failed to update item this passes
6. Restart API → toggle item 2 → checkbox persists (happy path restored)
Yep passes
7. Click up arrow on item 3 → item 3 swaps with item 2 in UI
this works as expectd
8. Stop API → click up arrow on item 3 → list snaps back to server order, red error toast "Failed to reorder item"
Yep it doesn't snap back to server order I get a toast saying failed to reorder item and an error being displayed stating that there was an error when attempting to fetch the resource.
9. Click trash icon on an item → item disappears from list
Works!
10. Stop API → click trash → item reappears at original position, red error toast "Failed to delete item"
Works!
11. Mobile viewport → open card modal → switch to "Related" tab → checklist renders there
Related has attachments and dependencies, Checklist is rendered as a clickable tab so we're good

## Task 13 — Card Comments

1. Open any card modal → comments panel visible
Works! mobile and desktop
2. Type comment text → click Post → comment appears in list with author + timestamp
Works!
3. Empty input → Post button disabled
Works!
4. Stop API → type comment → click Post → input not cleared, red error toast
Works
5. Page refresh → comments persist (loaded from API, not just local state)
Works
6. Mobile → comments render in "Related" tab
Works

## Task 14 — Card Attachments

1. Open any card modal → attachments panel visible
Works
2. Click upload → select small image (< 5MB) → file uploads, appears in list with filename + size
It allowed me to upload a pdf and a small image.
3. Click downloaded file icon → file downloads with correct content
Works!
4. Click delete on an attachment → confirmation → attachment disappears from list
Works!
5. Stop API → click upload → red error toast, file not added
Works!
6. Try uploading > 10MB file → expect validation rejection (size limit)
Works!
7. MinIO stopped → upload → graceful failure with error toast, no crash
Works!
8. Mobile → attachments render in "Related" tab
Works!

## Task 15 — Card Dependencies

1. Open card with no dependencies → "No dependencies" empty state shown
Works!
2. Create dependency via API (link another card as blocks/blocked-by/relates-to) → reopen modal → badge appears with relationship type label
3. Each linked card shows title + clickable to navigate (if routing wired)
4. Mobile → dependencies render in "Related" tab

## Task 16 — Card Metadata Editor

1. Open any card modal → metadata sidebar shows: Type, Column, Assignees, Due date, Parent epic
2. Change Type from Task → Bug → save → modal refreshes, type shown as Bug, no 409 error
3. Stop API → change type → save → red error toast, type reverts
4. Change Column dropdown → save → card column updates
5. Add an assignee (project member) → appears in assignees list
6. Remove an assignee → disappears
7. Set due date in future → due date shows on card on board
8. Set due date in past → board card shows red text
9. Pick a parent epic (different card) → parent appears in metadata
10. Save metadata → open another modal in same session → no stale version: edit description first then metadata → both save without 409 CARD_CONCURRENCY_MISMATCH
11. Open archived card modal → metadata fields disabled / not editable
12. Mobile → metadata renders in "Related" tab

## Project Members — Self-Remove Confirmation (cycle 3 fix)

1. Log in as a non-owner member of a project with another active member present
2. Open project members list (either Project edit modal or board page MemberManagementPanel)
3. Click remove (trash icon) on own row → confirm dialog appears (was broken before: removed immediately without confirmation)
4. Click Cancel in confirm dialog → member still in list, no API call fired
5. Click remove on own row again → confirm dialog → click Confirm → member removed, toast shown
6. Log in as project Owner → click remove on own row → no confirm dialog, member removed immediately (owners bypass the warning)
7. Click remove on another non-owner member → no confirm dialog, removed immediately

## Project List — Archive / Restore + Filter Toggle

1. Project list page (`/projects`) → three-dot menu on a project → Archive → confirm → project disappears from default list
2. Enable "Include archived" filter toggle → archived project reappears with "Archived" badge
3. From three-dot menu on archived project → Restore → confirm → project returns to active state
4. Stop API → click Archive on a project → red error toast, project still visible
5. Stop API → click Restore on archived project → red error toast, project stays archived

## Regressions

1. Card modal opens, loads card data, description editor + auto-save still work
2. Card description saves without `409 CARD_CONCURRENCY_MISMATCH` when metadata also edited
3. Board view renders all cards with type icons + due-date formatting
4. Board filter bar: Type, Assignee, Archive, Search all still work
5. Card create modal still works (uses shared `cardTypeToApiString`/`formatDueDate` utilities)
6. Session expiry warning system still functions (refresh button)
7. Build: `cd src/web-ui && pnpm build` succeeds
8. Typecheck: `cd src/web-ui && pnpm typecheck` passes
9. Lint: `cd src/web-ui && pnpm lint` passes

## Cleanup

- [ ] Restore any cards archived during validation
- [ ] Delete any test comments/attachments uploaded
- [ ] Undo any test dependency links
