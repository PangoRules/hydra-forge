## Validate: Phase 6 Plan 10 — LlmClientFactory + IEmbeddingClient on OpenAiCompatibleAdapter

### Setup
- [ ] Postgres + MinIO up (`docker compose up -d postgres minio`)
- [ ] `Llm__EncryptionKey` env var set (base64-encoded 32-byte key)
- [ ] At least one `LlmProvider` row per adapter type configured via admin API: OpenAiCompatible (with valid API key), Anthropic (with valid API key), Ollama (local instance on `http://localhost:11434`)
- [ ] `dotnet build` clean
- [ ] `dotnet test --filter "FullyQualifiedName~LlmClientFactoryTests"` — 15/15 pass
- [ ] Server boots: `dotnet run --project src/HydraForge.Server`

### Happy Path
1. `dotnet test --filter "FullyQualifiedName~LlmClientFactoryTests"` → 15 pass (adapter resolution per type, caching same/different IDs, unknown type throws, EmbeddingFor cast + non-OpenAI throw)
2. Resolve `ILlmClientFactory` from a request scope via a test endpoint or temporary controller injection → singleton instance, same reference across scopes
3. Call `factory.For(provider)` twice for the same provider → returns the **same** `OpenAiCompatibleAdapter` instance (`Assert.Same` holds)
4. Call `factory.ImageFor(provider)` for a `DallE` provider → returns `DallEAdapter`; call twice → same instance
5. Configure two providers with different IDs but the same `AdapterType=OpenAiCompatible` → `For()` returns **different** instances (cache key is provider ID, not adapter type)
6. Configure a `Diffusers` provider → `ImageFor()` returns `ComfyUiAdapter` (mapping works, plan step 3)
7. Configure a `ComfyUi` provider → `ImageFor()` returns `ComfyUiAdapter` (same mapping as Diffusers)
8. Trigger a chat completion via `OpenAiCompatibleAdapter` (e.g. via a controller that uses `IModelRouter` → `ILlmClientFactory.For(...)`) → upstream OpenAI returns 200 with streaming chunks; API key decrypted at call time (no key logged)
9. Trigger an embedding request via `factory.EmbeddingFor(provider)` for the OpenAiCompatible provider → upstream `/v1/embeddings` returns 200; `EmbeddingResult.Vectors` populated as `IReadOnlyList<ReadOnlyMemory<float>>` with correct dimensionality
10. Trigger an image generation via `factory.ImageFor(dalleProvider)` → DALL·E returns image URL(s) wrapped in `GeneratedImage`

### Edge Cases
1. `factory.For(provider)` with `AdapterType = DallE` → throws `NotSupportedException("No LLM client adapter for provider type: DallE")` (image adapter, not text)
2. `factory.ImageFor(provider)` with `AdapterType = Ollama` → throws `NotSupportedException("No image client adapter for provider type: Ollama")` (text-only adapter)
3. `factory.EmbeddingFor(provider)` with `AdapterType = Anthropic` → logs warning + throws `NotSupportedException("Embedding is only supported for OpenAiCompatible adapters. Got: Anthropic")`
4. `factory.EmbeddingFor(provider)` with `AdapterType = Ollama` → same `NotSupportedException`
5. Embedding request when upstream returns 4xx/5xx → `Result<EmbeddingResult>.Failure` with `Error("EMBEDDING_FAILED", ...)`; no exception thrown to caller
6. Embedding request when upstream returns 200 but malformed JSON → `Result<EmbeddingResult>.Failure` with `Error("EMBEDDING_PARSE_FAILED", ...)`
7. Embedding request when upstream returns 200 with `data: []` → `Result<EmbeddingResult>.Failure` with `Error("EMBEDDING_EMPTY", ...)`
8. OpenAiCompatible provider with empty `ApiKeyEncrypted` → request sent without `Authorization` header (Ollama local mode or keyless providers)
9. Provider with invalid API key → upstream returns 401 → adapter returns `Result.Failure` (no exception leak)
10. `provider.BaseUrl` with trailing slash → URL assembled correctly (factory uses `provider.BaseUrl.TrimEnd('/')` in adapter)

### Regressions
1. `dotnet ef migrations has-pending-model-changes` → clean (no schema drift from this plan)
2. All 14 `WebApplicationFactory` test fixtures boot with `ILlmClientFactory` resolved via DI; `AesGcmKeyVault` shared instance still works
3. `LlmServiceCollectionExtensions.AddLlmInfrastructure` still registers `IKeyVault` singleton + 6 named HTTP clients (`openai-compatible`, `anthropic`, `ollama`, `dalle`, `stability-ai`, `comfyui`) with correct timeouts (60s text, 120s dalle/stability, 300s comfyui)
4. Pre-existing adapters (`OpenAiCompatibleAdapter` text completion, `AnthropicAdapter`, `OllamaAdapter`, `DallEAdapter`, `StabilityAiAdapter`, `ComfyUiAdapter`) still pass their own unit tests
5. `IModelRouter` (consumed via factory in next plan) can resolve a primary client via `ILlmClientFactory.For()` without DI errors
6. Server `GET /health` returns 200 — `LlmProviderHealthProbe` still functional
7. `dotnet test` (full suite) — no regressions in Domain/Application/Infrastructure/Server/Tui test projects

### Cleanup
- [ ] Remove any temporary LlmProvider rows created for manual validation
- [ ] Unset `Llm__EncryptionKey` env var
