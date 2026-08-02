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

## Plan 4: ChatMessage Images Extension

### Setup
- [ ] Solution builds clean: `dotnet build`
- [ ] All tests pass: `dotnet test`
- [ ] `src/HydraForge.Application/Llm/LlmDtos.cs` contains `ImageBlock` record
- [ ] `src/HydraForge.Application/Chat/ChatMessageMapper.cs` exists with both mapper methods

### Happy Path
1. Construct `ChatMessage(ChatRole.User, "hi")` (2-arg) → compiles, `Images` defaults to `null`
2. Construct `ChatMessage(ChatRole.User, "hi", [new ImageBlock("k1", "image/png")])` → compiles, `Images` has 1 block
3. Call `ChatMessageMapper.ToDomainImagesJson([block1, block2])` → returns camelCase JSON array string with both blocks
4. Round-trip: `ToApplicationImages(ToDomainImagesJson(blocks))` → returns `ImageBlock[]` with same `StorageKey`/`MediaType` values

### Edge Cases
1. `ToDomainImagesJson(null)` → returns `"[]"`
2. `ToDomainImagesJson([])` → returns `"[]"`
3. `ToApplicationImages(null)` → returns `[]`
4. `ToApplicationImages("")` → returns `[]`
5. `ToApplicationImages("not json")` → returns `[]` (no throw)
6. `ToApplicationImages("[]")` → returns `[]`
7. `RouteDecision` still constructs without `Provider` arg → `Provider` defaults to `null`

### Regressions
1. Existing `ChatMessage(Role, Content)` callers → still compile, no breaking change
2. TUI NSwag codegen → still produces `Generated/Contracts.cs` (build re-runs NSwag)
3. Existing LLM Application tests (`HydraForge.Application.Tests`) → still pass
4. `RouteDecision` consumers (model router, callers) → unchanged signature, no compile errors

### Cleanup
- [ ] None — pure DTO/mapper additions, no DB rows or runtime side effects

### Setup
- [ ] Branch `task/app-ports` checked out, clean worktree
- [ ] `dotnet build` returns 0 errors / 0 warnings
- [ ] `src/HydraForge.Application/Chat/` contains 11 files (10 interfaces + 1 DTO file)

### Happy Path — Compile-time contract
1. Open `IChatSessionRepository.cs` → confirm `ListAsync` has `Guid ownerId` as first parameter before `Guid? folderId`, `Guid? projectId`, `DateTime? before`, `int limit`
2. Open `IChatSummaryGenerator.cs` → confirm `using HydraForge.Domain.Common;` and return type is `Task<Result<string>>` (bare `Result<string>`, NOT `Result<string, Error>`)
3. Open `ChatDtos.cs` `CreateChatSessionRequest` → confirm fields in order: `Title`, `Guid? FolderId`, `Guid? ProjectId`, `Guid? OpenCardId`, `Guid? PersonalityId`, `AiEditMode? AiEditMode`, `bool SearchAllMyDocs`, `Guid? ForkedFromSessionId`
4. Open `ChatDtos.cs` `UpdateChatSessionRequest` → confirm `string Title`, `Guid? FolderId`, `Guid? PersonalityId`, `AiEditMode? AiEditMode`, `bool SearchAllMyDocs` — no `ProjectId`/`OpenCardId`
5. Open `ChatDtos.cs` `ChatSessionDto` → confirm `bool PersonalityArchived` field present
6. Open `ChatDtos.cs` `ChatSessionDetailDto` → confirm `bool PersonalityArchived`, `Guid OwnerId`, `IReadOnlyList<ChatMessageDto> Messages` present
7. Open `ChatDtos.cs` `PromptPresetGroupDto` → confirm trailing `IReadOnlyList<PromptPresetDto> Presets` field
8. Open `ChatDtos.cs` `CardChatLinkDto` → confirm `Guid OwnerId` and `string OwnerUsername` present
9. Open `ChatDtos.cs` `ChatSearchResultDto` → confirm `string MatchedOn` (NOT `DateTime MatchedAt`)
10. Open `ChatDtos.cs` `ChatPermissionDto` → confirm `bool Granted` + `AiEditMode Mode`
11. Open `DomainErrorCodes.cs` → confirm `Chat` nested class has exactly 15 constants with the listed names

### Edge Cases — Naming / typo guard
1. `DomainErrorCodes.Chat` constant strings match exact spelling (e.g. `CHAT_SESSION_NOT_FOUND`, `CHAT_EMBEDDING_FAILED`) — case-sensitive
2. DTO positional record field order matches downstream NSwag codegen consumer expectations (e.g. TUI Generated/Contracts.cs)
3. All repository methods returning `IReadOnlyList<T>` use `IReadOnlyList<T>` not `List<T>` or `IEnumerable<T>`
4. All `CancellationToken` parameters have `= default` default value and are last
5. `IChatSessionRepository.GetActiveByPanelAsync` accepts `Guid? openCardId` (nullable, since panel may be open on project without card)

### Regressions
1. Existing `DomainErrorCodes` nested classes (`Auth`, `Projects`, `Cards`, `Llm`, etc.) → unchanged, still compile
2. Application layer boundary → no Infrastructure or EF Core imports in any new file
3. `dotnet build` for `HydraForge.Tui` → still 0 errors (TUI Generated/Contracts.cs shows -12 lines in diff, must reconcile with new server DTOs)

### Cleanup
- [ ] None — pure code, no DB or runtime state to reset
