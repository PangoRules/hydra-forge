## Validate: Plan 5a UX Polish — Collapsible Plans, Status Dropdown, Fullscreen Editor

### Setup
- [ ] API server running (`dotnet run --project src/HydraForge.Server`)
- [ ] Web dev server running (`pnpm dev`)
- [ ] Authenticated user with valid JWT token
- [ ] A project exists where user is a member
- [ ] A Goal card exists with at least 2 plans (one Pending, one Active, one Done)
- [ ] A Goal card exists with a Spec

### Collapsible Plan Sections

1. [ ] **Default collapsed** → open card modal → Plans section shows headers only, no editor / Save / History buttons visible
2. [ ] **Click header to expand** → click plan header row → editor + Save + History appear below header
3. [ ] **Chevron rotates** → chevron rotates 180° when expanded, returns to 0° when collapsed
4. [ ] **Click header to collapse** → click expanded header → body hides, only header remains
5. [ ] **Multiple plans can be expanded independently** → expand Plan A → expand Plan B → both bodies visible
6. [ ] **Click Save/History button does NOT collapse** → expand plan → click Save → plan stays expanded (no toggle)
7. [ ] **Click title input does NOT collapse** → expand plan → click title field to focus → plan stays expanded
8. [ ] **Click status dropdown does NOT collapse** → expand plan → click status badge to open menu → plan stays expanded
9. [ ] **Read-only mode** → open card as archived/non-member → header still clickable but body shows read-only editor; Save/Activate/Complete/Reactivate hidden
10. [ ] **Done plan opacity** → Done plan section renders with `opacity-75` class

### Interactive Status Dropdown

11. [ ] **Dropdown opens on badge click** → click status badge → menu shows the 2 other statuses (excludes current)
12. [ ] **Pending → Active via dropdown** → plan status Pending → dropdown → select "Active" → API call fires, badge changes to "Active"
13. [ ] **Active → Done via dropdown** → plan status Active → dropdown → select "Done" → API call fires, badge changes to "Done", editor becomes read-only
14. [ ] **Done → Active via dropdown** → plan status Done → dropdown → select "Active" → API call fires, badge changes to "Active", editor becomes editable
15. [ ] **Pending → Done via dropdown** → plan status Pending → dropdown → select "Done" → BOTH activate and complete fire sequentially, final status is "Done"
16. [ ] **Revert to Pending not supported** → click "Pending" in dropdown → error toast "Cannot revert to Pending. Create a new plan instead." (red toast, 6s)
17. [ ] **Dropdown disabled in readonly mode** → open archived card → status badge not clickable, no menu opens
18. [ ] **Search input hidden** → open dropdown → no search text input visible (just the 2 status options)
19. [ ] **Failed activate** → simulate 500 error → error toast "Failed to activate plan", plan status remains unchanged

### Fullscreen Toggle on MarkdownEditor

20. [ ] **Fullscreen button visible** → expand a plan → editor toolbar shows maximize icon at right
21. [ ] **Click fullscreen** → click maximize icon → editor fills viewport (fixed inset-0, z-50, bg-background), icon switches to minimize
22. [ ] **Editor usable in fullscreen** → can type in editor area; toolbar buttons (bold, italic, etc.) work
23. [ ] **Source toggle works in fullscreen** → toggle to source mode in fullscreen → source textarea fills available height, minimize button remains in source bar? (button only in WYSIWYG toolbar — verify source mode toggle works)
24. [ ] **Escape exits fullscreen** → press Escape → editor returns to inline mode, icon switches back to maximize
25. [ ] **Minimize button exits** → click minimize icon → editor returns to inline mode
26. [ ] **Fullscreen works in CardSpec** → open spec, click fullscreen → spec editor fills viewport, Escape exits
27. [ ] **Multiple editors, only one fullscreen at a time** → expand plan A fullscreen → expand plan B → each editor has its own fullscreen state independently

### Regressions

28. [ ] **Save plan still works** → expand plan → edit title/content → click Save → toast success, version increments
29. [ ] **Activate/Complete/Reactivate buttons still work** → click button (not dropdown) → same API behavior
30. [ ] **History panel still works** → click History → versions panel renders, Restore still works
31. [ ] **Plan create form still works** → click Add Plan → inline form appears, Create button works
32. [ ] **Spec section unaffected** → Spec section above plans still renders, edits still save
33. [ ] **No console errors** → open card modal → no Vue warnings in dev console