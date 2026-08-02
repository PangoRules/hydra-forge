# E2E Regression Matrix — Phase 7 Chat (General & Project) Design

## Plan 1: Domain Entities + Enums

Plan 1 of Phase 7 adds pure Domain layer (entities, enums, state methods, unit tests). No UI, no API, no DbContext changes in this plan — those are deferred to Plan 2 (EF migration) and later.

### Setup
- [ ] Check out branch `task/domain-entities` (commit `474ada7`)
- [ ] Confirm PostgreSQL container is not required (Domain layer is pure logic)

### Happy Path — State Transitions
1. Construct a fresh `ChatSession` → `Status == Active`, `AiEditMode == PerMutation`, `SearchAllMyDocs == false`, `ClosedAt == null`, `Summary == null`
2. Call `session.Open(personalityId, cardId, AiEditMode.Blanket)` → `Status == Active`, `PersonalityId`/`OpenCardId`/`AiEditMode` all set
3. Call `session.Close("done")` → `Status == Closed`, `ClosedAt` non-null UTC, `Summary == "done"`
4. Call `session.SetAiEditMode(AiEditMode.Blanket)` while Active → `AiEditMode == Blanket`, no exception
5. Call `session.ToggleSearchAllMyDocs(true)` then `(false)` → flag flips both ways

### Edge Cases — Guard Rejection
1. Close an already-Closed session → no exception, `Status`/`Summary` unchanged (idempotent)
2. Call `SetAiEditMode(...)` on a Closed session → throws `InvalidOperationException`, state unchanged
3. Call `ToggleSearchAllMyDocs(...)` on a Closed session → throws `InvalidOperationException`, state unchanged
4. Call `Open(...)` on a Closed session → re-opens cleanly, `ClosedAt`/`Summary` cleared, `Status` back to Active

### Regressions
1. Existing domain test suite still green: `dotnet test tests/HydraForge.Domain.Tests` → 82/82 pass (72 pre-existing + 10 new)
2. Full solution build: `dotnet build` → succeeds with zero errors (the `CS8603` warning in `GenerateAiNarrativeTests.cs:347` is pre-existing and unrelated)
3. Domain layer imports check:
   `grep -rlE "using (Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore|System\.Net\.Http)" src/HydraForge.Domain` → returns only `obj/` build artifacts, no source matches
4. EF migration drift: `dotnet ef migrations has-pending-model-changes` reports drift → expected and **deferred to Plan 2 (EF Migration)**; do not flag against this plan

### Cleanup
- [ ] None — no DB writes, no temp data
