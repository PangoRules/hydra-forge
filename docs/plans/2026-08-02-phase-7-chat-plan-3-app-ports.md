# Plan 3: Application Ports + DTOs + Error Codes
**Branch:** `task/app-ports`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 3

**Goal:** Define repository interfaces, DTOs, and error codes for all chat resources.

**Files:**
- Create: `src/HydraForge.Application/Chat/IChatSessionRepository.cs`
- Create: `src/HydraForge.Application/Chat/IChatMessageRepository.cs`
- Create: `src/HydraForge.Application/Chat/IChatSessionDocumentRepository.cs`
- Create: `src/HydraForge.Application/Chat/IPromptPresetRepository.cs`
- Create: `src/HydraForge.Application/Chat/IPromptPresetGroupRepository.cs`
- Create: `src/HydraForge.Application/Chat/IAgentPersonalityRepository.cs`
- Create: `src/HydraForge.Application/Chat/ICardChatLinkRepository.cs`
- Create: `src/HydraForge.Application/Chat/IDocumentRepository.cs`
- Create: `src/HydraForge.Application/Chat/IChatRagRetriever.cs`
- Create: `src/HydraForge.Application/Chat/IChatSummaryGenerator.cs`
- Create: `src/HydraForge.Application/Chat/ChatDtos.cs`
- Modify: `src/HydraForge.Domain/Common/DomainErrorCodes.cs`

**Steps:**

- [ ] Define `IChatSessionRepository`: `GetByIdAsync`, `GetActiveByPanelAsync(projectId, openCardId, ownerId)`, `ListAsync(folderId?, projectId?, before?, limit)`, `AddAsync`, `UpdateAsync`
- [ ] Define `IChatMessageRepository`: `GetBySessionAsync(sessionId, before?, limit)`, `AddAsync`, `GetByIdAsync`
- [ ] Define `IChatSessionDocumentRepository`: `GetBySessionAsync`, `AddAsync`, `RemoveAsync`, `ExistsAsync`
- [ ] Define `IPromptPresetRepository` + `IPromptPresetGroupRepository`: CRUD + group-nulling on archive
- [ ] Define `IAgentPersonalityRepository`: CRUD + `SetDefaultAsync`
- [ ] Define `ICardChatLinkRepository`: `GetByCardAsync`, `AddAsync`, `ArchiveAsync`
- [ ] Define `IDocumentRepository`: `GetByIdAsync`, `ListByUserAsync`, `AddAsync`, `ArchiveAsync`
- [ ] Define `IChatRagRetriever`: `RetrieveAsync(sessionId, queryEmbedding, searchAllMyDocs, k)` → `IReadOnlyList<DocumentChunk>`
- [ ] Define `IChatSummaryGenerator`: `GenerateSummaryAsync(sessionId)` → `Result<string, Error>`
- [ ] Define DTOs: `ChatSessionDto`, `ChatSessionDetailDto`, `ChatSessionPageDto`, `ChatMessageDto`, `ChatMessagePageDto`, `ChatFolderDto`, `PromptPresetDto`, `PromptPresetGroupDto`, `AgentPersonalityDto`, `CardChatLinkDto`, `DocumentDto`, `ChatSearchResultDto`, `ChatPermissionDto`. `ChatSessionDto`/`ChatSessionDetailDto` include `personalityArchived: bool` (derived from `AgentPersonality.ArchivedAt`, not stored — see plan-6/plan-11). Session create request DTO includes optional `forkedFromSessionId: Guid?` (fork action, spec §7 "Summarize → start my own")
- [ ] Add error codes: `CHAT_SESSION_NOT_FOUND`, `CHAT_MESSAGE_NOT_FOUND`, `CHAT_SESSION_CLOSED`, `CHAT_SESSION_ARCHIVED`, `CHAT_SESSION_NOT_OWNER`, `CHAT_FOLDER_MAX_DEPTH`, `CHAT_DOCUMENT_NOT_OWNED`, `CHAT_STREAM_IN_PROGRESS`, `CHAT_MODEL_NO_VISION`, `CHAT_SUMMARY_FAILED`, `CHAT_PRESET_GROUP_NOT_FOUND`, `CHAT_PERSONALITY_NOT_FOUND`, `CHAT_CARD_NOT_IN_PROJECT`, `CHAT_DOCUMENT_ALREADY_ATTACHED`, `CHAT_EMBEDDING_FAILED` (used by plan-5's ingestion failure path — was referenced there without being defined here)

**Acceptance:**
- `dotnet build` — all projects compile
