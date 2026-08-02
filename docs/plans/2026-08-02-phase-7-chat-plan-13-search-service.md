# Plan 13: Chat Search Service
**Branch:** `task/chat-search-service`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 13

**Goal:** Search across caller's chat sessions' titles + message content. Postgres ILIKE (not pgvector).

**Files:**
- Create: `src/HydraForge.Application/Chat/ChatSearchService.cs`
- Create: `src/HydraForge.Application/Chat/IChatSearchService.cs`
- Create: `tests/HydraForge.Application.Tests/Chat/ChatSearchServiceTests.cs`

**Steps:**

- [ ] Define `IChatSearchService`: `SearchAsync(userId, query, projectId?)` → `ChatSearchResultDto[]`
- [ ] Implement: ILIKE on `ChatSession.Title` + `ChatMessage.Content` for caller's sessions. `projectId` optional filter
- [ ] Return: session id, title, snippet (first 200 chars around match), matched-on field
- [ ] Write tests: title match, content match, project filter, no results
- [ ] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-13-search-service-matrix.md` — search matches on both session title and message content, scoped to the caller's own sessions

**Acceptance:**
- `dotnet build`
- `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~ChatSearch"`
