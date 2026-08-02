# Plan 12: ContextCompressor + unit tests

**Branch:** `task/context-compressor`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 12

## Steps

### 1. Create `ContextCompressor` service
- File: `src/HydraForge.Application/Llm/ContextCompressor.cs`
- Implements `IContextCompressor`. Depends on `IModelRouter` (to pick Economy model for summarization).
- **No circular DI**: compressor depends on router, router depends on DB — no cycle. Compressor does NOT depend on `ILlmClientFactory` directly; it calls router → factory internally.

### 2. Implement `CompressAsync`
1. Estimate total tokens via `TokenEstimator.EstimateTokens` across all blocks.
2. Threshold: `modelMaxTokens * thresholdRatio` (default 0.75, configurable via `Llm:ContextCompressionThresholdRatio`).
3. If under threshold → return `CompressedContext(blocks, estimatedTokens, WasCompressed: false)`.
4. If over threshold:
   - Identify oldest non-pinned memory blocks (skip `CacheBlockType.SystemContext` and `ProjectSnapshot`).
   - Pinned blocks (`MemoryEntry.IsPinned`) are never compressed — but compressor doesn't have access to `MemoryEntry`. Instead, add `IsPinned` flag to `CacheBlock` DTO (Task 3 update).
   - Summarize oldest non-pinned blocks via a cheap `ILlmClient` call (Economy tier, `MemoryExtraction` feature).
   - Route via `IModelRouter.ResolveAsync(AiFeature.MemoryExtraction, ...)` → get `ILlmClient` via factory.
   - Call `StreamChatAsync` with summarization prompt, collect full response.
   - Replace summarized blocks with single `CacheBlock(CacheBlockType.Memory, summary)`.
5. Return `CompressedContext(newBlocks, newEstimatedTokens, WasCompressed: true)`.

### 3. No-op/passthrough default
- If no Economy model available (router returns error), return uncompressed blocks with log warning. Never fail the call because compression is unavailable.

### 4. Update `CacheBlock` DTO
- File: `src/HydraForge.Application/Llm/LlmDtos.cs`
- Add `bool IsPinned = false` to `CacheBlock` record.

### 5. Register DI
- `services.AddScoped<IContextCompressor, ContextCompressor>()`

### 6. Unit tests
- File: `tests/HydraForge.Application.Tests/Llm/ContextCompressorTests.cs`
- Test cases:
  - Under threshold → no compression, `WasCompressed = false`.
  - Over threshold → compression triggered, blocks reduced.
  - Pinned blocks preserved.
  - No Economy model → passthrough (no error).
  - Empty blocks list → no-op.
  - Threshold ratio config respected.

## Verification
- `dotnet build`
- `dotnet test --filter "ContextCompressor"`
- Verify no circular DI at runtime.