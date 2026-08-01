## Validate: ContextCompressor service + DI wiring (Plan 12)

Scope: `ContextCompressor` Application service + 7 unit tests + `IContextCompressor` DI registration + `LlmOptions` binding via `AddOptions().Bind()`. No HTTP surface; no UI; no callers yet (consumer arrives in Phase 7 chat service).

### Setup
- [ ] `dotnet build` — clean (only pre-existing `EfProjectRepository.cs` CS8073 warning).
- [ ] `dotnet test --filter "FullyQualifiedName~ContextCompressor"` — 7/7 pass.
- [ ] `dotnet csharpier check .` — clean.
- [ ] `src/HydraForge.Server/appsettings.{json,Development.json}` has `Llm:EncryptionKey` (base64 32-byte) — required for server startup since `AesGcmKeyVault` validates key at boot; missing key throws `InvalidOperationException` and server refuses to start.

### Happy Path (unit tests already cover)
1. `CompressAsync_UnderThreshold_ReturnsUncompressed` — small blocks, `WasCompressed=false`, original blocks returned unchanged.
2. `CompressAsync_OverThreshold_TriggersCompression` — 2 large blocks over threshold → summarize → 1 summary block, content stitched from streamed delta chunks.
3. `CompressAsync_PinnedBlocks_Preserved` — pinned block at index 0 survives; non-pinned blocks at indices 1,2 collapsed into summary; result count = 2.
4. `CompressAsync_ThresholdRatioConfig_Respected` — `ContextCompressionThresholdRatio=0.01` triggers compression at low total tokens.

### Edge Cases (unit tests already cover)
1. `CompressAsync_EmptyBlocks_ReturnsNoOp` — empty list → `WasCompressed=false`, no router call.
2. `CompressAsync_NoEconomyModel_ReturnsPassthrough` — router returns `NoModelForFeature` → warn log, return uncompressed (no exception).
3. `CompressAsync_ErrorChunk_ReturnsPassthrough` — `ChatChunkFinishReason.Error` mid-stream → return original uncompressed blocks; `ContentFilter` same path (covered by same code branch).
4. `OperationCanceledException` from `StreamChatAsync` — rethrown (not swallowed); cancellation propagates to caller.

### Wiring / Integration (manual)
1. Server boot — `IContextCompressor` resolves from DI container when requested; no DI cycle (compressor → `IModelRouter` + `ILlmClientFactory`; router → `IRoutingConfigProvider` + `IWarnLogger`; factory → adapters; no back-edge to compressor).
2. `LlmOptions` binding — `AddOptions<LlmOptions>().Bind(configuration.GetSection("Llm"))` resolves `ContextCompressionThresholdRatio` from config (default 0.75 if section absent or partial).
3. `RouteDecision` consumers — `ModelRouter.cs` line 86 now populates `Provider` field. Existing `RouteDecision` consumers (none besides `ContextCompressor` yet) still compile via the new `LlmProvider? Provider = null` parameter being optional.

### Regressions
1. `ModelRouter` tests — still pass (no behavior change in `ResolveAsync`; only the `RouteDecision` constructor call adds a new parameter).
2. `AesGcmKeyVault` tests — still pass (file now imports `LlmOptions` from `HydraForge.Application.Llm` instead of `HydraForge.Infrastructure.Llm`; behavior unchanged).
3. Other `ILlmClientFactory` consumers — unchanged (`ContextCompressor` is a new consumer, not a modification).

### Cleanup
- [ ] None — pure Application-layer service, no DB schema, no persistent state.
