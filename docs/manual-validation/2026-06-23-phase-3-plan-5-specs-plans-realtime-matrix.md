# Manual Validation Matrix — Plan 5: Specs/Plans/Realtime

## Setup
- [ ] API server running locally (`ASPNETCORE_ENVIRONMENT=Development`)
- [ ] Web dev server running (`pnpm dev` at `src/web-ui`)
- [ ] Logged in, project with at least one Goal card and one Idea card (and Task/Issue for negative case)

## Task — Type-Conditional Docs Tab (CardModal)
Goal: Verify the Docs tab shows only on Goal/Idea and Plan only on Goal.

1. Open a Task card modal → tabs: Details, Checklist, Comments, Related. No "Docs" tab.
2. Open an Issue card modal → tabs same as Task. No "Docs" tab.
3. Open an Idea card modal → "Docs" tab present; clicking it shows Spec editor only (no Plan section, no `USeparator`).
4. Open a Goal card modal → "Docs" tab present; clicking it shows Spec editor, `USeparator`, then Plan editor.
5. With Goal modal open and Docs tab active → switch card type to Task via metadata panel → active tab auto-resets to Details, Docs tab disappears.

## Task — CardSpec Inline Editor (ownership card)
6. On Goal card → Docs tab → Spec editor visible with title/description/content fields and Save button.
7. Type a spec title + description + content → click Save → green toast "Spec saved"; refresh page → spec still present.
8. Reopen the card → click "History" button → version list appears on right side with v1 entry.
9. Save again → if History open, versions list refreshes and v2 appears.
10. Click "Hide history" → history panel closes; "History" button label returns.
11. Click "Restore" on a previous version → green toast "Version restored"; current title/content reverts; if history panel open, list refreshes.

## Task — CardPlan Inline Editor (ownership card, Goal only)
12. On Goal card → Docs tab → Plan editor visible below Spec with Save button.
13. Type a plan title + description + content → click Save → green toast "Plan saved"; refresh → plan still present.
14. Repeat history/save/restore cycle (steps 8-11) for Plan → version list works, restore works.

## Task — Spec/Plan API Wrapper Unwrap (regression for prior finding)
15. Confirm History renders actual version entries (not empty) when versions exist on backend.
   - Backstop check via devtools Network: `GET /api/projects/{pid}/specs/{sid}/versions` returns `{ "versions": [...] }`; UI shows them.

## Task — SignalR BoardHub Real-time Sync
16. Browser A: logged-in, viewing board → devtools Network shows `WS /hubs/board` upgrade succeeded.
17. Browser B (different user, same project): move a card from Column 1 to Column 2.
18. Browser A: card appears in Column 2 without manual refresh (board store refetched).
19. Open devtools on Browser A → Console → temporarily lose network for 5s → "Reconnecting…" state implied by realtime state.
20. Restore network → connection re-establishes; card-move events resume.

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
