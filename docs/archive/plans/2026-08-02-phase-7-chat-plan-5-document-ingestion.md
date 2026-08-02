# Plan 5: Document Upload + Chunking + Embedding
**Branch:** `task/document-ingestion`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 5

**Goal:** Upload → chunk → embed → persist pipeline. Text/markdown/code/csv/html only (no PDF in Phase 7).

**Files:**
- Create: `src/HydraForge.Application/Chat/DocumentIngestionService.cs`
- Create: `src/HydraForge.Application/Chat/IDocumentIngestionService.cs`
- Create: `tests/HydraForge.Application.Tests/Chat/DocumentIngestionServiceTests.cs`

**Steps:**

- [x] Define `IDocumentIngestionService`: `IngestAsync(userId, title, content, contentType, stream?)` → `Result<Document, Error>`
- [x] Implement chunking: split text into ~500-token chunks (~2000 chars) with ~100-token overlap. Chunk index 0..N
- [x] Implement embedding: batch all chunks through `IEmbeddingClient.EmbedAsync`. On failure → `CHAT_EMBEDDING_FAILED`, don't persist
- [x] Store file via `IFileStore` (key: `{userId}/document/{documentId}/{guid}`)
- [x] Persist `Document` row + `DocumentChunk[]` rows with `SourceType="document"`, `SourceId=DocumentId`
- [x] Write tests: chunk boundary, overlap, embedding failure path, empty content
- [ ] Manual validation: `docs/manual-validation/2026-08-02-phase-7-chat-plan-5-document-ingestion-matrix.md` — upload text/markdown/csv/html, confirm chunks + embeddings persisted; force an embedding failure and confirm no `Document` or `DocumentChunk` rows are left orphaned

**Acceptance:**
- `dotnet build`
- `dotnet test tests/HydraForge.Application.Tests --filter "FullyQualifiedName~DocumentIngestion"`
