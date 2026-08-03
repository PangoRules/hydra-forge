# Plan 7: ChatSession Service
**Branch:** `task/chatsession-service`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 7

**Goal:** CRUD, F6 implicit close (async background job), close-with-summary, permission-state read.

**Files:**
- Create: `src/HydraForge.Application/Chat/ChatSessionService.cs`
- Create: `src/HydraForge.Application/Chat/IChatSessionService.cs`
- Create: `tests/HydraForge.Application.Tests/Chat/ChatSessionServiceTests.cs`

**Steps:**

- [ ] Define `IChatSessionService`: `CreateAsync`, `GetAsync`, `ListAsync`, `UpdateAsync`, `CloseAsync`, `ArchiveAsync`, `GetPermissionAsync`, `AttachDocumentAsync`, `DetachDocumentAsync`, `ListDocumentsAsync`
- [ ] `CreateAsync`: create Active session. If `projectId` set → membership guard (admin bypass). If `openCardId` set → validate card belongs to project. **F6**: if existing Active session for same `(projectId, openCardId, ownerId)`, enqueue background job to close old session (summary + CardChatLink, one LLM call), return new session immediately. **Fork** (spec §7 "Summarize → start my own"): if `forkedFromSessionId` set instead, validate the source session is a shared project chat the caller can read, create a new `Active` session owned by the caller, generate a summary of the source session via `IChatSummaryGenerator` and pre-populate it as the new session's first message (one LLM call) — independent of F6, mutually exclusive with it (a create request has either a panel tuple or a `forkedFromSessionId`, not both)
- [ ] `CloseAsync` (F3): generate summary via `IChatSummaryGenerator`, create `CardChatLink` if `OpenCardId+ProjectId` set, set `Status=Closed`, revoke AI edit. Idempotent. Empty session → no summary, no CardChatLink
- [ ] `GetPermissionAsync`: return `{ granted: bool, mode: AiEditMode }` — granted when Active + ProjectId != null
- [ ] `UpdateAsync`: only while Active. Patch title, personalityId, searchAllMyDocs, aiEditMode
- [ ] Write tests: F6 implicit close ordering, close idempotency, empty session close, Closed session rejects update, fork creates independent session owned by caller
- [ ] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-7-chatsession-service-matrix.md` — new-chat-from-panel returns immediately while old session's summary/CardChatLink appear moments later; explicit close is idempotent; fork produces a new session with a pre-filled summary message

**Acceptance:**
- `dotnet build`
- `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~ChatSessionService"`
