# Plan 8: ChatMessage Service
**Branch:** `task/chatmessage-service`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 8

**Goal:** History pagination, user-message persist (the only persist path — hub never persists).

**Files:**
- Create: `src/HydraForge.Application/Chat/ChatMessageService.cs`
- Create: `src/HydraForge.Application/Chat/IChatMessageService.cs`
- Create: `tests/HydraForge.Application.Tests/Chat/ChatMessageServiceTests.cs`

**Steps:**

- [ ] Define `IChatMessageService`: `SendUserMessageAsync(sessionId, userId, content, images?)` → `Result<ChatMessage, Error>`, `GetHistoryAsync(sessionId, before?, limit)` → `ChatMessagePageDto`
- [ ] `SendUserMessageAsync`: validate session Active + owner, persist `ChatMessage(Role=User, Content=content, ImagesJson=mapped)`, return `messageId`. This is the **only** persist path
- [ ] `GetHistoryAsync`: cursor pagination (`before` = `CreatedAt` of oldest in page), owner or project member auth
- [ ] Write tests: Closed session reject, non-owner reject, pagination boundary
- [ ] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-8-chatmessage-service-matrix.md` — history pagination loads older pages on scroll-up; message send on a Closed session is rejected

**Acceptance:**
- `dotnet build`
- `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~ChatMessageService"`
