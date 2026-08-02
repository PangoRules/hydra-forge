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

## Plan 5: Document Upload + Chunking + Embedding

Plan 5 of Phase 7 adds the `IDocumentIngestionService` Application-layer port + implementation: text → chunking → embedding → file storage → `Document` + `DocumentChunk` persistence. **No HTTP controller, no UI, no Infrastructure EF implementation, no DI registration in this plan** — those are deferred to Plan 16 (REST controllers) and the EF model/migration already landed in Plan 2.

This matrix covers what's verifiable at the Application layer today (unit tests + boundary checks) and flags what must be re-validated when Plan 16 ships.

### Setup
- [ ] Branch `task/document-ingestion` checked out, clean worktree at HEAD `9d45d53`
- [ ] `dotnet build` → 0 errors / 0 warnings
- [ ] `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~DocumentIngestion"` → 7/7 pass
- [ ] Full suite `dotnet test` → 913+ tests green (330 Application + 200 Server + 137 Tui + 246 Infrastructure)
- [ ] Confirm PostgreSQL is **not** required (Application-layer tests use in-memory mocks)
- [ ] Confirm MinIO is **not** required (file store is mocked)

### Happy Path — Chunking (Application unit tests)
1. `IngestAsync(userId, "Doc", 500×'x', "text/plain")` → `IsSuccess`, exactly 1 chunk persisted with `ChunkIndex == 0`, `Content == 500×'x'`, embedding is `Vector(new float[1536])`
2. `IngestAsync(userId, "Doc", 4000×'x', "text/plain")` → exactly 3 chunks at indices `0`, `1`, `2`
3. Chunks' character ranges: `[0, 2000)`, `[1600, 3600)`, `[3200, 4000)` — 400-char tail-overlap between consecutive chunks
4. Last 400 chars of chunk N equal first 400 chars of chunk N+1 (the actual overlap invariant — not just chunk count)

### Happy Path — Persistence (Application unit tests)
5. `IngestAsync` returns `Result<Document>.Success(document)` with `document.UserId == userId`, `document.Title == "Doc"`, `document.Content == content`, `document.ContentType == "text/plain"`, `document.Version == 1`, `CreatedAt == UpdatedAt` (UTC)
6. `DocumentChunk.SourceType == "document"` and `DocumentChunk.SourceId == DocumentChunk.DocumentId == document.Id`
7. `DocumentChunk.Embedding` is a `Pgvector.Vector` built from a 1536-dim float array (one vector per chunk)

### Happy Path — File Storage (Application unit tests)
8. With `stream = MemoryStream([1,2,3])` and `content = ""`: `document.FilePath` is non-null, `StartsWith($"{userId}/document/{document.Id}/")` (key per AGENTS.md storage convention: `{userId}/{sourceType}/{sourceId}/{guid}`, no user filename)
9. Storage key contains a `Guid.NewGuid()` segment → unique per upload, never collides

### Edge Cases — Validation Rejection (Application unit tests)
10. `content = ""`, no stream → `IsFailure`, `Error.Code == DomainErrorCodes.Chat.EmbeddingFailed`, no Document or DocumentChunk rows added
11. `content = "   "` (whitespace only), no stream → `IsFailure`, same error code, no persistence
12. `content = null`, `stream = MemoryStream([1,2,3])` → stream content is appended (null treated as empty), non-empty result, ingestion proceeds
13. `content = ""`, `stream = MemoryStream([])` → effective content is empty → failure, no persistence

### Edge Cases — Embedding Failure Path (Application unit tests)
14. Embedding client returns `Result<EmbeddingResult>.Failure` → `IsFailure`, `Error.Code == CHAT_EMBEDDING_FAILED`, `CapturedDocuments` and `CapturedChunks` both empty
15. Embedding client returns 2 vectors for 3 chunks (count mismatch) → `IsFailure`, `CHAT_EMBEDDING_FAILED`, message contains the actual counts; **no Document or DocumentChunk rows added** (the vector-count guard runs before `_documentRepo.AddAsync`)
16. Embedding client returns 4 vectors for 3 chunks → same as #15
17. Embedding failure with a stream provided → no Document persisted AND no file orphaned in the real store (in-memory mock cannot exercise this — see "Deferred to Plan 16" below)

### Edge Cases — Stream Handling (Application unit tests)
18. Non-seekable stream (e.g. `NetworkStream` in real usage) is buffered into a `MemoryStream` before content extraction — the `await stream.CopyToAsync(ms, ct)` line at `DocumentIngestionService.cs:38` guarantees seekability before the file-store call
19. Stream content read via `StreamReader(Encoding.UTF8, leaveOpen: true)` then `ms.Position = 0` rewind before passing to `_fileStore.StoreAsync(seekableStream, ...)` — no double-consume, no reader-held buffer issue at disposal
20. Empty stream + empty content → caught by whitespace check (#13)

### Boundary Checks
21. Application layer does **not** import Infrastructure:
    `grep -rlE "HydraForge\.Infrastructure|Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore" src/HydraForge.Application/Chat/DocumentIngestionService.cs src/HydraForge.Application/Chat/IDocumentIngestionService.cs src/HydraForge.Application/Chat/IDocumentChunkRepository.cs` → no matches
22. Service is in `HydraForge.Application.Chat` namespace; imports `HydraForge.Application.{Attachments,Llm}` + `HydraForge.Domain.{Common,Entities.PersonalSpace,Enums}` only — no layer violation
23. `IDocumentIngestionService` interface implemented by `DocumentIngestionService` (`public sealed class DocumentIngestionService : IDocumentIngestionService`) — verified by build
24. EF migration drift: `PATH="$PATH:$HOME/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server` → no changes (this plan adds no entities)

### Regressions
25. Existing Application tests (`dotnet test tests/HydraForge.Application.Tests`) → 330/330 pass (7 new `DocumentIngestion*` tests + 323 pre-existing)
26. Domain tests unchanged
27. Infrastructure EF model contract tests (`AssertProperties` for `Document`/`DocumentChunk`) → still pass (entities untouched)
28. Server.Tests factory wiring: `grep -l ConfigureServices tests/HydraForge.Server.Tests/` does **not** require updating — `IDocumentIngestionService` is consumed only by Application-layer tests in this plan; factories only need updating when the REST controller (Plan 16) lands
29. TUI NSwag codegen unaffected — no new DTOs in this plan
30. `LlmProvider` test setup uses `AdapterType.OpenAiCompatible` — does not conflict with `FeatureAllowedModel` allowlist introduced in Phase 6 (D-65 follow-up) because ingestion calls `ResolveAsync(AiFeature.PersonalChat, ...)` which routes to a configured embedding-capable model

### Deferred to Plan 16 — Re-validate When REST Controller Lands
The following cannot be exercised today because there's no HTTP endpoint, no Infrastructure repository implementation, and no DI registration. Re-run this matrix's deferred section once Plan 16 ships:

- [ ] **Real DB persistence:** spin up Postgres, hit the upload endpoint with a real text/markdown/csv/html file, then `SELECT count(*) FROM documents` and `SELECT count(*) FROM document_chunks` — counts match the expected chunk count (text length / 2000-char windows)
- [ ] **Real MinIO storage:** check the MinIO console (`http://localhost:9001`) for a new object under `bucket/{userId}/document/{documentId}/{guid}` after upload
- [ ] **Real embedding integration:** configure an OpenAI-compatible provider, upload a 5KB text file, confirm `document_chunks.embedding` is populated with valid 1536-dim vectors (not the zero-vector default)
- [ ] **Cross-content-type upload:** upload `.txt`, `.md`, `.csv`, `.html` files (PDF explicitly excluded per plan) — all accepted, all chunked identically (no type-specific routing)
- [ ] **Orphan verification under real failure:** inject an embedding failure (e.g. disable the provider mid-request), confirm via `SELECT * FROM documents WHERE id = X` and `SELECT * FROM document_chunks WHERE document_id = X` that **no** Document or DocumentChunk rows are written; confirm MinIO that **no** file is stored under the would-be key
- [ ] **Transaction atomicity:** confirm `_documentRepo.AddAsync` and `_chunkRepo.AddRangeAsync` share a `SaveChangesAsync` call in the Infrastructure implementation; if they don't, this plan has a residual orphan risk between the two calls that the unit tests cannot catch — flag as a finding against Plan 16 if the impl uses per-repo `SaveChanges`
- [ ] **Concurrency:** upload the same document twice in parallel — confirm no duplicate `Document` rows, no duplicate `DocumentChunk` rows (relies on DB unique constraints — verify they exist in the EF model)
- [ ] **Large-file streaming:** upload a 10MB markdown file — confirm the buffered `MemoryStream` doesn't OOM the server, confirm chunks are created for the entire content

### Cleanup
- [ ] None required — this plan makes no DB writes and no persistent runtime changes

## Plan 6: RAG Retrieval Service

Plan 6 adds `IChatRagRetriever`/`ChatRagRetriever` — embeds the user's query, runs a pgvector similarity search scoped by the session's `SearchAllMyDocs` toggle (F1), and assembles the resulting chunks plus (on the session's first message only) the project snapshot into `CacheBlock`s for the chat request. No HTTP controller, no UI — this is consumed by the (not-yet-built) `SendMessage` hub flow in Plan 15.

### Setup
- [ ] Branch `task/rag-retrieval` checked out, clean worktree
- [ ] `dotnet build` returns 0 errors / 0 warnings (besides the pre-existing `GenerateAiNarrativeTests.cs:347` warning)
- [ ] `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~ChatRagRetriever"` → 10/10 pass
- [ ] `dotnet test tests/HydraForge.Infrastructure.Tests --filter "FullyQualifiedName~AnthropicAdapter"` → 18/18 pass

### Happy Path — Application unit tests
1. `SearchAllMyDocs == false`: `ChatSessionDocument` rows for the session resolve to a specific `documentId` list, passed to `IDocumentChunkRepository.SearchAsync` (not `null`)
2. `SearchAllMyDocs == true`: `SearchAsync` called with `sessionDocumentIds = null` (all of the owner's documents are candidates)
3. Project chat, session's first message (`IChatMessageRepository.GetBySessionAsync` returns empty): result includes a `CacheBlockType.ProjectSnapshot` block first, then a `CacheBlockType.RagContext` block with the concatenated chunk content
4. Project chat, **not** the first message (a prior `ChatMessage` exists): result omits the `ProjectSnapshot` block entirely — only `RagContext`
5. Non-project chat (`ProjectId == null`): never includes a `ProjectSnapshot` block regardless of message position
6. `RagContext` block content is a `"\n\n"`-joined concatenation of every returned chunk's `Content`, in the order `IDocumentChunkRepository.SearchAsync` returned them

### Edge Cases
1. Session not found (`GetByIdAsync` returns `null`) → empty block list, no embedding call attempted
2. Embedding call fails (`Result.Failure`) → warning logged, empty (or snapshot-only, if first message) result — retrieval failure never throws, chat send proceeds without RAG
3. Embedding succeeds but returns zero vectors (`EmbeddingResult.Vectors.Count == 0`, e.g. zero-token query) → warning logged, `IDocumentChunkRepository.SearchAsync` is **not** called, empty (or snapshot-only) result
4. Session-scoped search (`SearchAllMyDocs == false`) with no `ChatSessionDocument` rows attached → skips the chunk search entirely (nothing to search), snapshot-only or empty result — does not fall back to searching all docs
5. Chunk search returns zero results (query embedded fine, but no matching chunks) → no `RagContext` block added, snapshot block (if any) still included

### Regressions — Anthropic cache-block correctness
1. **`CacheBlockType.RagContext` must never receive `cache_control` from `AnthropicAdapter`** — RAG content changes every message, so caching it costs the Anthropic cache-write surcharge with zero cache-hit benefit. Verified by `AnthropicAdapterTests.StreamChatAsync_RagContextBlock_GoesToSystemArray_WithoutCacheControl`. (This was a real bug found in review: the first implementation used `CacheBlockType.SystemContext` for RAG chunks, which `AnthropicAdapter` unconditionally stamps with `cache_control` — silently defeating the plan's own "no cache_control" requirement. Fixed by introducing a dedicated `RagContext` enum value handled the same as `Memory`.)
2. `OpenAiCompatibleAdapter` treats all `CacheBlockType` values uniformly (prefix-hash + system message) — adding `RagContext` required no adapter change there; confirm `OpenAiCompatibleAdapterTests` still 100% pass
3. `ContextCompressor`'s eviction logic only targets `CacheBlockType.Memory` — confirm `RagContext` blocks are never evicted/summarized by `ContextCompressorTests` (RAG blocks are already top-K scoped per message; they don't need long-term compression)
4. Full solution build + full test suite green (`dotnet build && dotnet test`) after the `CacheBlockType` enum addition — no other switch/pattern-match on `CacheBlockType` left un-updated

### Deferred to Plan 15/16 — Re-validate When Hub/Controllers Land
- [ ] **Real pgvector search:** spin up Postgres with seeded `DocumentChunk` rows (real embeddings), call `ChatRagRetriever.RetrieveAsync` end-to-end, confirm `EfDocumentChunkRepository.SearchAsync`'s `ORDER BY embedding <=> @queryEmbedding LIMIT @k` returns the expected nearest chunks
- [ ] **Scope toggle E2E:** via the Web UI or `.http` smoke test, confirm toggling a session's "search all my docs" setting changes the retrieved chunk set for the same query
- [ ] **Snapshot-first-message E2E:** send two messages in the same project session, confirm (via request logging or a debug endpoint) the project snapshot block appears only on the first

### Cleanup
- [ ] None — pure Application-layer unit tests, no DB writes, no persistent runtime state

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
