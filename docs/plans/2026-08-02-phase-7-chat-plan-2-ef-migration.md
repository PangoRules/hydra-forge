# Plan 2: EF Migration + Model Config
**Branch:** `task/ef-migration`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 2

**Goal:** Add `AddPhase7Chat` migration. Configure EF for new/modified entities. Write `AssertProperties` tests.

**Files:**
- Modify: `src/HydraForge.Infrastructure/Persistence/HydraForgeDbContext.cs`
- Create: `src/HydraForge.Infrastructure/Migrations/*_AddPhase7Chat.cs`
- Create: `tests/HydraForge.Infrastructure.Tests/Model/ChatModelTests.cs`

**Steps:**

- [ ] Add `DbSet<ChatSessionDocument>`, `DbSet<PromptPresetGroup>`, `DbSet<PromptPreset>` to DbContext
- [ ] Configure `ChatSession`: enum-to-int for Status/AiEditMode, FK `PersonalityId → AgentPersonality` with `OnDelete: SetNull`, index on `OwnerId`
- [ ] Configure `ChatMessage`: `ImagesJson` as `nvarchar(max) NULL`
- [ ] Configure `ChatSessionDocument`: table `chat_session_documents`, unique index `(SessionId, DocumentId)`, FK cascade on session delete
- [ ] Configure `PromptPresetGroup`: table `prompt_preset_groups`, index on `UserId`
- [ ] Configure `PromptPreset`: table `prompt_presets`, FK `GroupId → PromptPresetGroup` with `OnDelete: Cascade`, index on `UserId`
- [ ] Run `dotnet ef migrations add AddPhase7Chat`
- [ ] Write `AssertProperties` tests for `ChatSessionDocument`, `PromptPresetGroup`, `PromptPreset`, modified `ChatSession`/`ChatMessage`

**Acceptance:**
- `dotnet ef migrations has-pending-model-changes` → no pending changes
- `dotnet test tests/HydraForge.Infrastructure.Tests --filter "FullyQualifiedName~ChatModel"`
