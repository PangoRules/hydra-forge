# Plan 1: Domain Entities + Enums
**Branch:** `task/domain-entities`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 1

**Goal:** Add `ChatSessionStatus`, `AiEditMode` enums. Extend `ChatSession`/`ChatMessage`. Create `ChatSessionDocument`, `PromptPresetGroup`, `PromptPreset` entities with state-transition methods.

**Files:**
- Create: `src/HydraForge.Domain/Enums/ChatSessionStatus.cs`
- Create: `src/HydraForge.Domain/Enums/AiEditMode.cs`
- Modify: `src/HydraForge.Domain/Entities/Chat/ChatSession.cs`
- Modify: `src/HydraForge.Domain/Entities/Chat/ChatMessage.cs`
- Create: `src/HydraForge.Domain/Entities/Chat/ChatSessionDocument.cs`
- Create: `src/HydraForge.Domain/Entities/Chat/PromptPresetGroup.cs`
- Create: `src/HydraForge.Domain/Entities/Chat/PromptPreset.cs`
- Create: `tests/HydraForge.Domain.Tests/Chat/ChatSessionTests.cs`

**Steps:**

- [ ] Create `ChatSessionStatus` enum: `Active = 1, Closed = 2`
- [ ] Create `AiEditMode` enum: `PerMutation = 1, Blanket = 2`
- [ ] Extend `ChatSession`: add `Status`, `AiEditMode`, `SearchAllMyDocs`, `PersonalityId`, `OpenCardId`, `ClosedAt`, `Summary`. Add state methods: `Open(personalityId?, openCardId?, aiEditMode)`, `Close(summary)`, `ToggleSearchAllMyDocs(value)`, `SetAiEditMode(mode)` — only while Active
- [ ] Extend `ChatMessage`: add `ImagesJson` (string?, JSON column for `ImageBlock[]`)
- [ ] Create `ChatSessionDocument`: `Id`, `SessionId`, `DocumentId`, `AddedByUserId`, `AddedAt`
- [ ] Create `PromptPresetGroup`: `Id`, `UserId`, `Name`, `CreatedAt`, `UpdatedAt`, `ArchivedAt`
- [ ] Create `PromptPreset`: `Id`, `UserId`, `GroupId?`, `Name`, `Content`, `CreatedAt`, `UpdatedAt`, `ArchivedAt`
- [ ] Write domain tests: `Open` sets Active, `Close` idempotent, `SetAiEditMode` rejected when Closed, `ToggleSearchAllMyDocs` toggles

**Acceptance:**
- `dotnet build src/HydraForge.Domain`
- `dotnet test tests/HydraForge.Domain.Tests --filter "FullyQualifiedName~ChatSession"`
