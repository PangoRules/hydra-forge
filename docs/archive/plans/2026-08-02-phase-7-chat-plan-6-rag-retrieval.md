# Plan 6: RAG Retrieval Service
**Branch:** `task/rag-retrieval`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 6

**Goal:** Implement `IChatRagRetriever` — embed query, pgvector similarity search, scope toggle (F1), cache-block assembly.

**Files:**
- Create: `src/HydraForge.Application/Chat/ChatRagRetriever.cs`
- Create: `tests/HydraForge.Application.Tests/Chat/ChatRagRetrieverTests.cs`

**Steps:**

- [ ] Implement `RetrieveAsync`: embed user message via `IEmbeddingClient`, build candidate set per `SearchAllMyDocs` (session-scoped via `ChatSessionDocument` join vs all-user-docs), pgvector `ORDER BY embedding <=> @queryEmbedding LIMIT @k` (k=8, configurable `Llm:Rag:TopK`)
- [ ] Build `CacheBlock(SystemContext, Content=concatenated chunk texts)` — no `cache_control` (RAG content changes per message)
- [ ] For project chats, prepend `CacheBlock(ProjectSnapshot)` only on session's first message (check if any prior `ChatMessage` exists)
- [ ] Personality system prompt: fetch `AgentPersonality`, check `ArchivedAt`, prepend `ChatMessage(Role=System)` if active
- [ ] Prompt preset injection: prepend preset content to user message wrapped in `<preset>` tags
- [ ] Embedding failure at retrieval → log warning, skip RAG, proceed (don't fail send)
- [ ] Write tests: scope toggle (session vs all-my-docs), empty docs, embedding failure fallback
- [ ] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-6-rag-retrieval-matrix.md` — session-scoped vs all-my-docs toggle returns different retrieval sets; project snapshot block appears only on the session's first message, not subsequent ones

**Acceptance:**
- `dotnet build`
- `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~ChatRagRetriever"`
