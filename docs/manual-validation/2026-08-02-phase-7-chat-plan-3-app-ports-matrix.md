## Validate: Phase 7 Chat — Application Ports + DTOs + Error Codes

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