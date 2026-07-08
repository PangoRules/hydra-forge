## Validate: Plan 5a — Doc Model Schema (UI only)

API behavior (DocType, PlanStatus, SpawnedFrom, SetStatus lifecycle) is covered by
`ProjectServiceTests`/`PlanServiceTests`/`SpecServiceTests`/`PlansControllerTests`/`SpecsControllerTests`
(xUnit) and `.http` smoke files — not re-validated manually here. This matrix only
covers what those don't: does the actual UI expose and wire the feature correctly.

### Setup
- [ ] API server running (`dotnet run --project src/HydraForge.Server`)
- [ ] Web dev server running (`pnpm dev`)
- [ ] Authenticated user, member of a project
- [ ] A Goal card exists in the project
- [ ] An Idea card exists in the project
- [ ] An Issue card exists in the project
- [ ] A Task card exists in the project

### UI — Docs tab visibility per card type

Actual gating logic (`CardModal.vue`): Spec shown for Goal/Idea/Issue, Plans shown for
Goal/Issue/Task — so every type gets a Docs tab, but the sections inside differ.

1. [X] **Goal card → Docs tab** → Spec section labeled "Specification" + Plans section with "Add Plan"
2. [X] **Idea card → Docs tab** → Spec section labeled "Concept" only, no Plans section
3. [ ] **Issue card → Docs tab** → Spec section labeled "Report" + Plans section with "Add Plan"
4. [ ] **Task card → Docs tab** → Plans section only (no Spec section), "Add Plan" present

### UI — Spec section (Goal / Idea / Issue)

5. [ ] **Create spec (empty state)** → title input + editor + "Create" button; fill both, click Create → spec saves, toast "Spec saved", button label switches to "Save"
6. [ ] **Save disabled when clean** → immediately after create/load, Save is disabled (nothing edited yet)
7. [ ] **Edit spec** → change title or content → Save enables → click Save → toast "Spec saved"
8. [ ] **Version history** → click "History" → versions panel opens showing past versions with author + timestamp
9. [ ] **Restore version** → click Restore on an older version → title/content revert, toast "Version restored", history refreshes
10. [ ] **Readonly (archived card)** → no Create/Save button, no title/content editing

### UI — Plans section (Goal / Issue / Task)

11. [ ] **Add Plan** → click "Add Plan" → inline form (title + editor + Create/Cancel) appears
12. [ ] **Create disabled until titled** → Create button disabled while title is empty
13. [ ] **Create plan** → fill title + content, click Create → plan appears in list with "Pending" badge, toast "Plan created"
14. [ ] **Expand/collapse** → click a plan's header row → chevron rotates, editor (and history panel if open) shows/hides
15. [ ] **Status dropdown** → click the status badge → dropdown lists the other two statuses only (e.g. from Pending: Active, Done)
16. [ ] **Set Active** → pick Active from dropdown → badge updates to "Active" immediately, no page reload
17. [ ] **Set Done** → pick Done from dropdown → badge "Done", editor becomes read-only, Save button disappears, plan row dims (opacity)
18. [ ] **Done → Pending/Active directly** → from a Done plan, status dropdown still works and allows jumping straight back to Pending or Active (no separate "Reactivate" button — single dropdown handles all transitions)
19. [ ] **Save gated by dirty state** → Save button disabled until title or content actually changes; disabled entirely while status is Done
20. [ ] **Multiple plans** → create 2+ plans on one card → all shown stacked, ordered by position, each independently expandable/editable
21. [ ] **Version history per plan** → click History on one plan → panel shows versions for that plan only (not other plans)
22. [ ] **Restore blocked on Done** → open history on a Done plan → Restore button disabled
23. [ ] **Restore on non-Done plan** → Restore an older version → content reverts, toast "Version restored"
24. [ ] **Readonly (archived card)** → no "Add Plan" button, status dropdown disabled, no Save buttons

### Regressions (UI)

25. [ ] **Tab switch preserves data** → edit a Spec/Plan without saving, switch to another tab and back → unsaved edits still present (no unexpected refetch wipes them)
26. [ ] **Closing and reopening the card** → re-fetches fresh Spec/Plan state from the server (saved changes persist, no stale cache)
27. [ ] **Idea card never shows a Plans section** → confirms no regression of the Idea-has-no-plans rule (D-44) after any of the above interactions
