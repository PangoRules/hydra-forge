# Manual Validation Matrix — Plan 5: Specs/Plans/Realtime

## Setup
- [X] API server running locally (`ASPNETCORE_ENVIRONMENT=Development`)
- [X] Web dev server running (`pnpm dev` at `src/web-ui`)
- [X] Logged in, project with at least one Goal card and one Idea card (and Task/Issue for negative case)

## Task — Type-Conditional Docs Tab (CardModal)
Goal: Verify the Docs tab shows only on Goal/Idea and Plan only on Goal.

1. Open a Task card modal → tabs: Details, Checklist, Comments, Related. No "Docs" tab.
No related, why is this mentioned, other than tat it works.
2. Open an Issue card modal → tabs same as Task. No "Docs" tab.
Like wise no docs tab. Works
3. Open an Idea card modal → "Docs" tab present; clicking it shows Spec editor only (no Plan section, no `USeparator`).
Yep i see Spec editor this works I believe
4. Open a Goal card modal → "Docs" tab present; clicking it shows Spec editor, `USeparator`, then Plan editor.
Yep I can see spec and plans! this works
5. With Goal modal open and Docs tab active → switch card type to Task via metadata panel → active tab auto-resets to Details, Docs tab disappears.
Works as expected


## Task — CardSpec Inline Editor (ownership card)
6. On Goal card → Docs tab → Spec editor visible with title/description/content fields and Save button.
Works, though description was thrown off as it don't make sense.
7. Type a spec title + description + content → click Save → green toast "Spec saved"; refresh page → spec still present.
Works
8. Reopen the card → click "History" button → version list appears on right side with v1 entry.
Works
9. Save again → if History open, versions list refreshes and v2 appears.
Works
10. Click "Hide history" → history panel closes; "History" button label returns.
Works
11. Click "Restore" on a previous version → green toast "Version restored"; current title/content reverts; if history panel open, list refreshes.
Works

## Task — CardPlan Inline Editor (ownership card, Goal only)
12. On Goal card → Docs tab → Plan editor visible below Spec with Save button.
Works
13. Type a plan title + description + content → click Save → green toast "Plan saved"; refresh → plan still present.
Works
14. Repeat history/save/restore cycle (steps 8-11) for Plan → version list works, restore works.
Works

## Task — Spec/Plan API Wrapper Unwrap (regression for prior finding)
15. Confirm History renders actual version entries (not empty) when versions exist on backend.
   - Backstop check via devtools Network: `GET /api/projects/{pid}/specs/{sid}/versions` returns `{ "versions": [...] }`; UI shows them.
   Works!

## Task — SignalR BoardHub Real-time Sync
16. Browser A: logged-in, viewing board → devtools Network shows `WS /hubs/board` upgrade succeeded.
Works
17. Browser B (different user, same project): move a card from Column 1 to Column 2.
Works
18. Browser A: card appears in Column 2 without manual refresh (board store refetched).
Works
19. Open devtools on Browser A → Console → temporarily lose network for 5s → "Reconnecting…" state implied by realtime state.
Works
20. Restore network → connection re-establishes; card-move events resume.
Works

## Task — SignalR PresenceHub Online Users
21. Browser A joins project → presence store updates (visible if app surfaces it).
22. Browser B joins same project → both browsers see each other (UserJoined event).
23. Browser B navigates away or closes → Browser A receives UserLeft; online list updates.

## Task — Card Focus Tracking
24. Browser A opens a card → `FocusCard` invoked (devtools shows `SignalR invoke`).
25. Browser B receives `CardFocused` event for that card → presence store `focusedCards` map updated.
26. Browser A closes the card → no BlurCard sent (acceptable per current hub contract; focus state is best-effort fire-and-forget).

## Task — Realtime Cleanup on Board Unmount (regression for prior finding)
27. Navigate away from board page → SignalR connections for `/hubs/board` and `/hubs/presence` close cleanly (devtools Network shows ws closed).
28. Presence store online users map and focused cards cleared on disconnect (covered by automated test).

## Task — Realtime State Reset on Hub onclose (regression for prior finding)
29. Simulate server restart or token expiry causing SignalR onclose → `isReconnecting.value = false`, `isConnected.value = false`. (Hard to surface without UI; covered by automated test.)

## Regressions
30. Existing tabs (Details, Checklist, Comments, Related) still work on Task/Issue cards.
31. Editing Spec/Plan doesn't break CardDescription / CardMetadata save (concurrency).
32. Board filtering, move-card, archive-card flows unaffected.

## Cleanup
- [ ] None required (test users and cards seeded by dev fixture)
