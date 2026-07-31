# Plan 3: Application ports (ILlmClient, IImageClient, IEmbeddingClient, IModelRouter, IContextCompressor, IUsageRecorder, ILlmAdminService, ILlmClientFactory + DTOs)

**Branch:** `task/llm-ports`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 3

## Steps

### 1. Create `src/HydraForge.Application/Llm/` directory
- New namespace `HydraForge.Application.Llm`.

### 2. Define DTOs/records
- File: `src/HydraForge.Application/Llm/LlmDtos.cs`
- `ChatRequest`, `ChatMessage`, `CacheBlock`, `ChatChunk`, `UsageSnapshot`, `ChatChunkFinishReason`, `ChatRole`, `CacheBlockType` — exactly as spec §Interfaces.
- `ImageRequest`, `GeneratedImage`, `ImageSize`, `InpaintRequest`.
- `EmbeddingRequest`, `EmbeddingResult`.
- `RouteDecision`, `FallbackProvider`.
- `CompressedContext`.
- `TokenUsageRecordInput`, `ImageUsageRecordInput`.
- `ProviderModelDto` (for admin probe results).

### 3. Define port interfaces
- `ILlmClient.cs`: `StreamChatAsync`, `GetModelsAsync`, `SupportsToolCalling`, `AdapterType` property.
- `IImageClient.cs`: `GenerateImageAsync`, `InpaintAsync`, `AdapterType` property.
- `IEmbeddingClient.cs`: `EmbedAsync`.
- `IModelRouter.cs`: `ResolveAsync(AiFeature, userId, projectId?, estimatedTokens, ct)`.
- `IContextCompressor.cs`: `CompressAsync(blocks, modelMaxTokens, ct)`.
- `IUsageRecorder.cs`: `RecordTokenAsync`, `RecordImageAsync`, `AccrueTokenUsageAsync`, `AccrueImageUsageAsync`.
- `ILlmClientFactory.cs`: `For(LlmProvider)`, `ImageFor(LlmProvider)`, `EmbeddingFor(LlmProvider)`.
- `ILlmAdminService.cs`: `ListProvidersAsync`, `CreateProviderAsync`, `UpdateProviderAsync`, `DisableProviderAsync`, `ProbeModelsAsync`, model config CRUD, routing CRUD, usage queries, budget get/set.

### 4. Define admin DTOs
- File: `src/HydraForge.Application/Llm/LlmAdminDtos.cs`
- `ProviderDto`, `ProviderPageDto`, `CreateProviderInput`, `UpdateProviderInput`, `ProviderModelDto`, `CreateModelInput`, `UpdateModelInput`, `FeatureRoutingDto`, `UpdateRoutingInput`, `TokenUsagePageDto`, `ImageUsagePageDto`, `UserBudgetDto`, `UpdateBudgetInput`.

### 5. Token estimation helper
- File: `src/HydraForge.Application/Llm/TokenEstimator.cs`
- Static method `EstimateTokens(string text) => text.Length / 4` (decision B).

## Verification
- `dotnet build` — Application project compiles (ports only, no impl yet).
- No tests needed for pure interface/DTO definitions.