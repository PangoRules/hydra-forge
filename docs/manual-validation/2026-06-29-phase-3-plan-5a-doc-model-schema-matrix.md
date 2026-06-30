## Validate: Plan 5a — Doc Model Schema (DocType + PlanStatus + Multi-Plan)

### Setup
- [ ] API server running (`dotnet run --project src/HydraForge.Server`)
- [ ] Web dev server running (`pnpm dev`)
- [ ] Authenticated user with valid JWT token
- [ ] A project exists where user is a member
- [ ] A Goal card exists in the project (for Spec testing)
- [ ] An Idea card exists in the project (for Concept DocType)
- [ ] An Issue card exists in the project (for Report DocType)
- [ ] A Task card exists in the project (for Plan-only testing)

### .http Smoke Tests

1. [ ] Run `Specs.http` → all requests succeed (create includes `docType`, responses include `docType`)
2. [ ] Run `Plans.http` → all requests succeed (create includes `position`, lifecycle endpoints activate/complete/reactivate work, Done guard returns error, reactivate→update succeeds)
3. [ ] Run `CardRelationships.http` → SpawnedFrom (type 4) relationship creates successfully

### API — Spec DocType

4. [ ] **Create a Spec on a Goal card** → `POST /api/projects/{projectId}/Specs/cards/{cardId}` with body `{ "docType": 1, "title": "Goal Spec", "description": null, "content": "# Goal" }` → `201` with `"docType": 1`
5. [ ] **Create a Spec on an Idea card** → `{ "docType": 2, "title": "Idea Concept", ... }` → `201` with `"docType": 2`
6. [ ] **Create a Spec on an Issue card** → `{ "docType": 3, "title": "Issue Report", ... }` → `201` with `"docType": 3`
7. [ ] **Get a Spec** → `GET .../Specs/{specId}` → response includes `"docType"`
8. [ ] **List Specs for a card** → `GET .../Specs/cards/{cardId}` → each item includes `"docType"`

### API — Plan Status/Position

9. [ ] **Create a Plan** → `POST .../Plans/cards/{cardId}` with `{ "title": "My Plan", "content": "# Steps", "specId": null, "position": 1 }` → `201` with `"status": 1` (Pending), `"position": 1`, `"specId": null`
10. [ ] **Get a Plan** → `GET .../Plans/{planId}` → response includes `"status"`, `"position"`, `"specId"`
11. [ ] **List Plans** → `GET .../Plans/cards/{cardId}` → each item includes `"status"`, `"position"`, `"specId"`
12. [ ] **Activate** → `POST .../Plans/{planId}/activate` → `200` with `"status": 2` (Active)
13. [ ] **Complete** → `POST .../Plans/{planId}/complete` → `200` with `"status": 3` (Done)
14. [ ] **Reactivate** → `POST .../Plans/{planId}/reactivate` → `200` with `"status": 2` (Active)

### API — Plan Done Guard

15. [ ] **Update Done Plan** → `PUT .../Plans/{planId}` (plan is Done) → error code `PLAN_EDIT_FORBIDDEN_WHEN_DONE`
16. [ ] **Restore on Done Plan** → `POST .../Plans/{planId}/restore` with `{ "version": 1 }` → error code `PLAN_EDIT_FORBIDDEN_WHEN_DONE`
17. [ ] **Reactivate, then Update** → reactivate (200, Active), then update (200, success)
18. [ ] **Default position** → create Plan without `position` field → `201` with `"position": 0`

### API — SpawnedFrom

19. [ ] **Create SpawnedFrom** → `POST .../cards/{sourceId}/cardrelationships` with `{ "targetCardId": "...", "type": 4 }` → `201`

### UI — Multi-Plan

20. [ ] **Goal card Docs tab** → shows both Spec (labeled "Specification") and Plans section with "Add Plan" button
21. [ ] **Idea card Docs tab** → shows Spec only (labeled "Concept"), no Plans section
22. [ ] **Issue card Docs tab** → shows Spec (labeled "Report") and Plans section
23. [ ] **Task card Docs tab** → shows Plans only (no Spec section), with "Add Plan" button
24. [ ] **Plan status badge** → newly created plan shows "Pending" badge, Activate button visible
25. [ ] **Activate plan** → click Activate → badge changes to "Active", Activate replaced by Complete button
26. [ ] **Complete plan** → click Complete → badge changes to "Done", Read-only editor, Reactivate button visible
27. [ ] **Reactivate plan** → click Reactivate → badge changes to "Active", editor becomes editable again
28. [ ] **Done plan read-only** → editor disabled, Save button hidden, Activate/Complete hidden, Reactivate shown
29. [ ] **Multiple plans** → create 2+ plans on a Goal card → both shown stacked, ordered by position
30. [ ] **Version history per plan** → click History on any plan → shows versions panel for that specific plan
31. [ ] **Add Plan form** → click "Add Plan" → inline form appears with title + editor + Create/Cancel buttons
32. [ ] **Create new plan** → fill title + content, click Create → plan appears in list, toast success

### Regressions

33. [ ] **Spec update** → `PUT .../Specs/{specId}` returns `200` with `"docType"` included
34. [ ] **Plan update on Active** → update content, `200`, version increments
35. [ ] **Create Spec without docType** → should fail 400 validation (verify actual behavior — may silently default)


