# E2E Regression Matrix — Phase 7 Chat Plan 5: Document Ingestion

Plan 5 of Phase 7 adds the `IDocumentIngestionService` Application-layer port + implementation: text → chunking → embedding → file storage → `Document` + `DocumentChunk` persistence. **No HTTP controller, no UI, no Infrastructure EF implementation, no DI registration in this plan** — those are deferred to Plan 16 (REST controllers) and the EF model/migration already landed in Plan 2.

This matrix covers what's verifiable at the Application layer today (unit tests + boundary checks) and flags what must be re-validated when Plan 16 ships.

## Setup
- [ ] Branch `task/document-ingestion` checked out, clean worktree at HEAD `9d45d53`
- [ ] `dotnet build` → 0 errors / 0 warnings
- [ ] `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~DocumentIngestion"` → 7/7 pass
- [ ] Full suite `dotnet test` → 913+ tests green (330 Application + 200 Server + 137 Tui + 246 Infrastructure)
- [ ] Confirm PostgreSQL is **not** required (Application-layer tests use in-memory mocks)
- [ ] Confirm MinIO is **not** required (file store is mocked)

## Happy Path — Chunking (Application unit tests)
1. `IngestAsync(userId, "Doc", 500×'x', "text/plain")` → `IsSuccess`, exactly 1 chunk persisted with `ChunkIndex == 0`, `Content == 500×'x'`, embedding is `Vector(new float[1536])`
2. `IngestAsync(userId, "Doc", 4000×'x', "text/plain")` → exactly 3 chunks at indices `0`, `1`, `2`
3. Chunks' character ranges: `[0, 2000)`, `[1600, 3600)`, `[3200, 4000)` — 400-char tail-overlap between consecutive chunks
4. Last 400 chars of chunk N equal first 400 chars of chunk N+1 (the actual overlap invariant — not just chunk count)

## Happy Path — Persistence (Application unit tests)
5. `IngestAsync` returns `Result<Document>.Success(document)` with `document.UserId == userId`, `document.Title == "Doc"`, `document.Content == content`, `document.ContentType == "text/plain"`, `document.Version == 1`, `CreatedAt == UpdatedAt` (UTC)
6. `DocumentChunk.SourceType == "document"` and `DocumentChunk.SourceId == DocumentChunk.DocumentId == document.Id`
7. `DocumentChunk.Embedding` is a `Pgvector.Vector` built from a 1536-dim float array (one vector per chunk)

## Happy Path — File Storage (Application unit tests)
8. With `stream = MemoryStream([1,2,3])` and `content = ""`: `document.FilePath` is non-null, `StartsWith($"{userId}/document/{document.Id}/")` (key per AGENTS.md storage convention: `{userId}/{sourceType}/{sourceId}/{guid}`, no user filename)
9. Storage key contains a `Guid.NewGuid()` segment → unique per upload, never collides

## Edge Cases — Validation Rejection (Application unit tests)
10. `content = ""`, no stream → `IsFailure`, `Error.Code == DomainErrorCodes.Chat.EmbeddingFailed`, no Document or DocumentChunk rows added
11. `content = "   "` (whitespace only), no stream → `IsFailure`, same error code, no persistence
12. `content = null`, `stream = MemoryStream([1,2,3])` → stream content is appended (null treated as empty), non-empty result, ingestion proceeds
13. `content = ""`, `stream = MemoryStream([])` → effective content is empty → failure, no persistence

## Edge Cases — Embedding Failure Path (Application unit tests)
14. Embedding client returns `Result<EmbeddingResult>.Failure` → `IsFailure`, `Error.Code == CHAT_EMBEDDING_FAILED`, `CapturedDocuments` and `CapturedChunks` both empty
15. Embedding client returns 2 vectors for 3 chunks (count mismatch) → `IsFailure`, `CHAT_EMBEDDING_FAILED`, message contains the actual counts; **no Document or DocumentChunk rows added** (the vector-count guard runs before `_documentRepo.AddAsync`)
16. Embedding client returns 4 vectors for 3 chunks → same as #15
17. Embedding failure with a stream provided → no Document persisted AND no file orphaned in the real store (in-memory mock cannot exercise this — see "Deferred to Plan 16" below)

## Edge Cases — Stream Handling (Application unit tests)
18. Non-seekable stream (e.g. `NetworkStream` in real usage) is buffered into a `MemoryStream` before content extraction — the `await stream.CopyToAsync(ms, ct)` line at `DocumentIngestionService.cs:38` guarantees seekability before the file-store call
19. Stream content read via `StreamReader(Encoding.UTF8, leaveOpen: true)` then `ms.Position = 0` rewind before passing to `_fileStore.StoreAsync(seekableStream, ...)` — no double-consume, no reader-held buffer issue at disposal
20. Empty stream + empty content → caught by whitespace check (#13)

## Boundary Checks
21. Application layer does **not** import Infrastructure:
    `grep -rlE "HydraForge\.Infrastructure|Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore" src/HydraForge.Application/Chat/DocumentIngestionService.cs src/HydraForge.Application/Chat/IDocumentIngestionService.cs src/HydraForge.Application/Chat/IDocumentChunkRepository.cs` → no matches
22. Service is in `HydraForge.Application.Chat` namespace; imports `HydraForge.Application.{Attachments,Llm}` + `HydraForge.Domain.{Common,Entities.PersonalSpace,Enums}` only — no layer violation
23. `IDocumentIngestionService` interface implemented by `DocumentIngestionService` (`public sealed class DocumentIngestionService : IDocumentIngestionService`) — verified by build
24. EF migration drift: `PATH="$PATH:$HOME/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server` → no changes (this plan adds no entities)

## Regressions
25. Existing Application tests (`dotnet test tests/HydraForge.Application.Tests`) → 330/330 pass (7 new `DocumentIngestion*` tests + 323 pre-existing)
26. Domain tests unchanged
27. Infrastructure EF model contract tests (`AssertProperties` for `Document`/`DocumentChunk`) → still pass (entities untouched)
28. Server.Tests factory wiring: `grep -l ConfigureServices tests/HydraForge.Server.Tests/` does **not** require updating — `IDocumentIngestionService` is consumed only by Application-layer tests in this plan; factories only need updating when the REST controller (Plan 16) lands
29. TUI NSwag codegen unaffected — no new DTOs in this plan
30. `LlmProvider` test setup uses `AdapterType.OpenAiCompatible` — does not conflict with `FeatureAllowedModel` allowlist introduced in Phase 6 (D-65 follow-up) because ingestion calls `ResolveAsync(AiFeature.PersonalChat, ...)` which routes to a configured embedding-capable model

## Deferred to Plan 16 — Re-validate When REST Controller Lands
The following cannot be exercised today because there's no HTTP endpoint, no Infrastructure repository implementation, and no DI registration. Re-run this matrix's deferred section once Plan 16 ships:

- [ ] **Real DB persistence:** spin up Postgres, hit the upload endpoint with a real text/markdown/csv/html file, then `SELECT count(*) FROM documents` and `SELECT count(*) FROM document_chunks` — counts match the expected chunk count (text length / 2000-char windows)
- [ ] **Real MinIO storage:** check the MinIO console (`http://localhost:9001`) for a new object under `bucket/{userId}/document/{documentId}/{guid}` after upload
- [ ] **Real embedding integration:** configure an OpenAI-compatible provider, upload a 5KB text file, confirm `document_chunks.embedding` is populated with valid 1536-dim vectors (not the zero-vector default)
- [ ] **Cross-content-type upload:** upload `.txt`, `.md`, `.csv`, `.html` files (PDF explicitly excluded per plan) — all accepted, all chunked identically (no type-specific routing)
- [ ] **Orphan verification under real failure:** inject an embedding failure (e.g. disable the provider mid-request), confirm via `SELECT * FROM documents WHERE id = X` and `SELECT * FROM document_chunks WHERE document_id = X` that **no** Document or DocumentChunk rows are written; confirm MinIO that **no** file is stored under the would-be key
- [ ] **Transaction atomicity:** confirm `_documentRepo.AddAsync` and `_chunkRepo.AddRangeAsync` share a `SaveChangesAsync` call in the Infrastructure implementation; if they don't, this plan has a residual orphan risk between the two calls that the unit tests cannot catch — flag as a finding against Plan 16 if the impl uses per-repo `SaveChanges`
- [ ] **Concurrency:** upload the same document twice in parallel — confirm no duplicate `Document` rows, no duplicate `DocumentChunk` rows (relies on DB unique constraints — verify they exist in the EF model)
- [ ] **Large-file streaming:** upload a 10MB markdown file — confirm the buffered `MemoryStream` doesn't OOM the server, confirm chunks are created for the entire content

## Cleanup
- [ ] None required — this plan makes no DB writes and no persistent runtime changes
