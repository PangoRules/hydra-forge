# E2E Regression Matrix — Phase 6 — LLM Infrastructure

## Plan 7: DallEAdapter (image generation + inpainting)

### Setup
- [ ] Postgres up (`docker compose up -d postgres`) — adapter itself needs no DB but DI registration chain does
- [ ] `Llm:EncryptionKey` set in shell env as base64-encoded 32-byte key (e.g. `export Llm__EncryptionKey="$(openssl rand -base64 32)"`)
- [ ] `dotnet build` clean — 0 errors, 0 warnings
- [ ] `dotnet test --filter "FullyQualifiedName~DallEAdapter"` — 19 pass (16 `[Fact]` + 3 `[Theory]` size-mapping cases)
- [ ] (Optional live) Real OpenAI API key in env to validate Happy Path 5; otherwise the matrix is exercised against the existing `JsonBodyHandler` / `MultipartBodyHandler` fakes, which pin down shape, auth, and error paths

### Happy Path
1. Inspect `DallEAdapter.AdapterType` → returns `AdapterType.DallE`
2. Call `GenerateImageAsync(ImageRequest(Guid, "dall-e-3", "A sunset over the ocean", ImageSize.Square1024, 1))` against a stub returning `{"data":[]}` → outgoing request: `POST {baseUrl}/images/generations`; body is `{"model":"dall-e-3","prompt":"A sunset over the ocean","n":1,"size":"1024x1024","response_format":"url"}`; `Authorization: Bearer <decrypted-key>` (when `ApiKeyEncrypted` is populated)
3. Call `GenerateImageAsync` with `ImageSize.Landscape1792` → body `size:"1792x1024"`; `ImageSize.Portrait1024` → `size:"1024x1792"`
4. Stub returns `{"data":[{"url":"https://example.com/img1.png"},{"url":"https://example.com/img2.png"}]}` → `Result.Success(new GeneratedImage(["https://example.com/img1.png","https://example.com/img2.png"], "1024x1024"))` (resolution = the mapped size string)
5. Converted b64 variant: stub returns `{"data":[{"b64_json":"SGVsbG8="},{"b64_json":"VGVzdA=="}]}` → `Result.Success` with two `ImageDataUrlsOrKeys` carrying the raw b64 strings (no base64 decode); `url` field empty/null is ignored
6. Mixed variant: stub returns `{"data":[{"b64_json":"abc"},{"url":"https://example.com/x.png"}]}` → both land in `ImageDataUrlsOrKeys` in order
7. Call `InpaintAsync(InpaintRequest(Guid, "dall-e-3", "Remove background", new byte[]{0x89,0x50,0x4E,0x47}, new byte[]{0x01}, ImageSize.Square1024))` against a stub returning `{"data":[{"url":"https://example.com/inpainted.png"}]}` → outgoing request: `POST {baseUrl}/images/edits` with `Content-Type: multipart/form-data`; parts in order: `image` (filename `image.png`, exact bytes, `image/png`), `mask` (filename `mask.png`, exact bytes, `image/png`), `prompt="Remove background"`, `model="dall-e-3"`, `n="1"`, `size="1024x1024"`

### Edge Cases
1. `GenerateImageAsync` against a stub returning HTTP 429 → `Result.Failure(Error("DALLE_GENERATE_FAILED", "DallE image generation failed: 429"))`
2. `GenerateImageAsync` against a stub returning HTTP 200 with body `not json {{{` → `Result.Failure(Error("DALLE_PARSE_FAILED", ...))`
3. `GenerateImageAsync` against a stub returning `{"data":[]}` → `Result.Failure(Error("DALLE_EMPTY_RESPONSE", ...))`
4. `GenerateImageAsync` against a stub returning `{"data":[{}]}` (no `b64_json`, no `url`) → `Result.Failure(Error("DALLE_NO_IMAGE_DATA", ...))`
5. `GenerateImageAsync` against a stub returning `{"data":null}` → `Result.Failure(Error("DALLE_EMPTY_RESPONSE", ...))`
6. `GenerateImageAsync` with `provider.ApiKeyEncrypted = ""` → outgoing request has no `Authorization` header (existing `AddAuthHeader` short-circuits on `IsNullOrWhiteSpace`)
7. `InpaintAsync` with `ImageBytes = null!` → `Result.Failure(Error("DALLE_INPAINT_MISSING_INPUT", "ImageBytes is required for inpainting."))`; HTTP request is never sent
8. `InpaintAsync` with `ImageBytes = Array.Empty<byte>()` → `Result.Failure(Error("DALLE_INPAINT_MISSING_INPUT", ...))`; HTTP request is never sent
9. `InpaintAsync` with `MaskBytes = null!` or `Array.Empty<byte>()` → `Result.Failure(Error("DALLE_INPAINT_MISSING_INPUT", "MaskBytes is required for inpainting."))`; HTTP request is never sent
10. `InpaintAsync` against a stub returning HTTP 400 → `Result.Failure(Error("DALLE_INPAINT_FAILED", ...))`
11. `InpaintAsync` passes byte arrays by reference, not by copy — fast-fail runs *before* any HTTP work, so a null/empty payload is cheap to reject (no `MultipartFormDataContent` allocation)
12. `provider.BaseUrl` has trailing slash (e.g. `https://api.openai.com/`) → request URI is `https://api.openai.com/images/generations` and `https://api.openai.com/images/edits` (no double slash); the adapter calls `TrimEnd('/')` once per call
13. (Live optional) Point `BaseUrl` at `https://api.openai.com` with a real `OPENAI_API_KEY`; call `GenerateImageAsync(ImageRequest(..., "dall-e-3", "A sunset over the ocean", ImageSize.Square1024, 1))` → `Result.Success` with one `ImageDataUrlsOrKeys` entry that is a valid `https://*.openai.com/...` URL

### Regressions
1. `dotnet ef migrations has-pending-model-changes` → clean (adapter adds no entity changes; `InpaintRequest` shape change from `ImageKey/MaskKey` to `ImageBytes/MaskBytes` is a record-shape change in Application, not an EF entity)
2. `dotnet test` (full suite) — still passes; no other test fixtures broken by the new `dalle` named HttpClient registration in `AddLlmInfrastructure`
3. `OpenAiCompatibleAdapter`, `AnthropicAdapter`, `OllamaAdapter` tests still pass — four named HttpClients coexist in the same `AddLlmInfrastructure` registration block without collision
4. `IKeyVault` registration in `AddLlmInfrastructure` unchanged — `AesGcmKeyVault` round-trip still works (Plan 2)
5. `AdapterType` enum still has its pre-existing members plus the `DallE = 6` value used here (no removal/reorder)
6. `LlmDtos` records — `InpaintRequest` is now `(ProviderModelConfigId, ModelId, Prompt, ImageBytes, MaskBytes, Size, Count = 1)`; downstream callers that previously constructed `InpaintRequest` with `ImageKey`/`MaskKey` will need to fetch the bytes via `IFileStore` before constructing the request. Verify no other code in `src/` or `tests/` still references the old shape (`grep -r "ImageKey\|MaskKey" src/ tests/` returns no `InpaintRequest` call sites today, but the change is API-breaking and must propagate to any future caller)
7. `IImageClient` interface unchanged — still `GenerateImageAsync` / `InpaintAsync` returning `Result<GeneratedImage>`. The byte-array shape is contained inside `InpaintRequest` and does not leak into the port
8. `GeneratedImage` shape unchanged — `ImageDataUrlsOrKeys` (mixed list of b64 or url strings) and `Resolution` (mapped size string)

### Cleanup
- [ ] Unset `Llm:EncryptionKey` after manual test
- [ ] If a real `OPENAI_API_KEY` was used in Edge Case 13, rotate it after testing
- [ ] No test data persists in DB (adapter does not write)

## Plan 8: StabilityAiAdapter (generate + inpaint)

### Setup
- [ ] Postgres + MinIO up (`docker compose up -d postgres minio`)
- [ ] `Llm:EncryptionKey` set in shell env as base64-encoded 32-byte key
- [ ] `ConnectionStrings__Default` set (or `appsettings.Development.json` pointing at port 5433)
- [ ] `dotnet build` — clean
- [ ] `dotnet test --filter "FullyQualifiedName~StabilityAiAdapter"` — 20 tests pass
- [ ] Full `dotnet test` — Infrastructure suite (186 tests) green
- [ ] An `LlmProvider` row exists with `AdapterType = StabilityAi`, valid encrypted API key, and a real Stability AI API key (or a sandbox account) — required only for live happy-path steps below

### Happy Path
1. Server starts with `stability-ai` named `HttpClient` registered → no DI errors
2. `IImageClient` resolved for `AdapterType.StabilityAi` returns a `StabilityAiAdapter` instance with `AdapterType == AdapterType.StabilityAi`
3. Call `GenerateImageAsync` with `Prompt = "a sunset"`, `ModelId = "sd3.5-large"`, `Size = Square1024`, `Count = 1` → returns `Result<GeneratedImage>` with 1 base64 string in `ImageDataUrlsOrKeys`
4. Repeat with `Count = 4` → returns `Result<GeneratedImage>` with 4 distinct base64 strings
5. Call `InpaintAsync` with a PNG `ImageBytes` + PNG `MaskBytes` + prompt → returns `Result<GeneratedImage>` with 1 base64 string
6. `ImageUsageRecordInput` row written for each successful call with `ImageCount` matching request `Count`, `Resolution = "generated"` (literal — known divergence from DallEAdapter)

### Edge Cases
1. `GenerateImageAsync` with `Size = Landscape1792` → request includes `aspect_ratio = "16:9"`
2. `GenerateImageAsync` with `Size = Portrait1024` → request includes `aspect_ratio = "9:16"`
3. Stability AI returns HTTP 429 (rate limit) → `Result.Failure("STABILITY_GENERATE_FAILED")`, error logged via `ILogger.LogError`
4. Stability AI returns HTTP 400 on inpaint → `Result.Failure("STABILITY_INPAINT_FAILED")`, error logged
5. Stability AI returns JSON `{}` (no `image` field) → `Result.Failure("STABILITY_EMPTY_RESPONSE")`
6. Stability AI returns malformed JSON → `Result.Failure("STABILITY_PARSE_FAILED")` (caught `JsonException`)
7. Stability AI returns binary PNG bytes (Content-Type `image/png`) → adapter base64-encodes raw bytes into `ImageDataUrlsOrKeys[0]`
8. `InpaintAsync` with `ImageBytes = null` → `Result.Failure("STABILITY_INPAINT_MISSING_INPUT")` without hitting network
9. `InpaintAsync` with `ImageBytes = []` → same failure code, no network call
10. `InpaintAsync` with `MaskBytes = null` or `MaskBytes = []` → same failure code, no network call
11. `LlmProvider.ApiKeyEncrypted` empty/whitespace → request sent without `Authorization` header; upstream returns 401 → surfaces as `STABILITY_GENERATE_FAILED` (known, matches sibling adapters)
12. `LlmProvider.BaseUrl` has trailing `/` (e.g. `https://api.stability.ai/`) → URL is normalized (`TrimEnd('/')`) to avoid double slash
13. `GenerateImageAsync` with `Count = 0` → returns `Success` with empty `ImageDataUrlsOrKeys` (loop body never executes; not asserted by tests)

### Regressions
1. `dotnet ef migrations has-pending-model-changes` → clean (no entity changes on this branch)
2. Existing image adapters (`DallEAdapter`, `OllamaAdapter`) still resolve and run — `dotnet test --filter "FullyQualifiedName~Adapter"` green
3. `OpenAiCompatibleAdapter`, `AnthropicAdapter` chat flows unaffected (no shared mutable state)
4. All 14 `WebApplicationFactory` test fixtures still boot with `Llm:EncryptionKey` configured
5. `LlmServiceCollectionExtensions.AddLlmInfrastructure` still registers all five named `HttpClient`s (`openai-compatible`, `anthropic`, `ollama`, `dalle`, `stability-ai`) each with 120s timeout
6. Domain layer unchanged — `grep -rlE "using (Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore|System\.Net\.Http)" src/HydraForge.Domain/` returns only generated `GlobalUsings.g.cs` (no source-file violations)

### Cleanup
- [ ] Unset `Llm:EncryptionKey` after manual test
- [ ] Delete any test `LlmProvider` rows created with a real Stability AI key

## Plan 10: LlmClientFactory

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

## Plan 11: ModelRouter (routing resolution + fallback chain)

### Setup
- [ ] Postgres up (`docker compose up -d postgres`)
- [ ] `Llm:EncryptionKey` set in shell env as base64-encoded 32-byte key
- [ ] Server builds clean: `dotnet build`
- [ ] `dotnet test --filter "FullyQualifiedName~ModelRouter"` — 11 pass (all algorithm cases via `FakeRoutingConfigProvider`)
- [ ] Apply latest EF migrations so `feature_routing_configs`, `llm_providers`, `provider_model_configs` tables exist (migration `20260730214033_AddFeatureRoutingConfigAndAdapterTypes` and any later ones)
- [ ] Seed at least one `FeatureRoutingConfig` + one `LlmProvider` + one enabled `ProviderModelConfig` via the DB or the LLM admin endpoints

### Happy Path
1. Resolve `IModelRouter` from DI scope → instance obtained, `IRoutingConfigProvider` resolves alongside it
2. Call `router.ResolveAsync(AiFeature.DeepResearch, userId, null, 1000, ct)` against a `FeatureRoutingConfig` with `DefaultTier=Standard`, one enabled Standard model with `MaxTokens=8192` → `Result.Success`, `Primary.Tier == Standard`, primary model is the alphabetically-first enabled model
3. Provider A (`FallbackProviderId = B`) at Standard tier, Provider B has one enabled Standard model → `Fallbacks.Count == 1`, fallback model is B's Standard model
4. Chain A → B → C, all Standard models enabled → `Fallbacks.Count == 2`, ordered A's-B then B's-C; primary still A's model
5. Same setup as #2 with `MaxTokens=512` on the Standard model and `estimatedTokens=1000` → router auto-bumps to a higher-tier enabled model with `MaxTokens >= 1000`; result is `Success` with the higher-tier primary
6. Provider chain A → B (one Standard model) → A again → router detects cycle, logs a warning via `IWarnLogger`, returns `Success` with primary=A's model and exactly 1 fallback (B's model); does not loop infinitely

### Edge Cases
1. Feature has no `FeatureRoutingConfig` row → `Result.Failure` with `Error.Code == Llm.NoModelForFeature`
2. `FeatureRoutingConfig` exists but no enabled provider/model at any tier → `Result.Failure` with `Error.Code == Llm.NoModelForFeature` (not ContextWindowExceeded)
3. `estimatedTokens` exceeds every enabled model's `MaxTokens` at every tier → `Result.Failure` with `Error.Code == Llm.ContextWindowExceeded`
4. `FeatureRoutingConfig.DefaultTier = Premium` with `MaxUserTier = Standard` and only a Standard-tier model enabled → `Success`; primary is the Standard model (ceiling respected)
5. `MaxUserTier == null` → routing stays at `DefaultTier` regardless of higher-tier availability
6. Provider at tier is `IsEnabled == false` → skipped; router selects the next enabled provider at that tier
7. `FallbackProviderId` points to a provider with no enabled model at the primary tier → fallback chain ends at that hop (no entry, no crash)
8. `FallbackProviderId` chain reaches a provider with no further fallback → chain stops naturally; remaining `Fallbacks` entries are returned in walk order
9. `IRoutingConfigProvider.GetProviderAsync(unknownGuid)` → returns `null`; fallback chain terminates gracefully (no NRE)

### Regressions
1. `dotnet ef migrations has-pending-model-changes` → clean (no entity changes in this plan)
2. `LlmServiceCollectionExtensions.AddLlmInfrastructure` still registers the prior: `IKeyVault` (AesGcm), `ILlmClientFactory`, named `HttpClient`s (`openai-compatible`, `anthropic`, `ollama`, `dalle`, `stability-ai`, `comfyui`)
3. New `IModelRouter` and `IRoutingConfigProvider` registered as `Scoped`; `IWarnLogger` registered (default `NullWarnLogger`)
4. `IRoutingConfigProvider` signatures return `IReadOnlyList<>` (not `List<>`) and queries are sorted by `Provider.Name` only (not `Provider.Tier`; that ordering was intentionally dropped)
5. `ModelRouter.ResolveAsync` does not throw on missing/invalid routing data — all expected failures surface via `Result.Failure` with `Llm.*` error codes
6. No new HTTP/network calls introduced by `ModelRouter.ResolveAsync` — it only reads routing config via `IRoutingConfigProvider`
7. Subsequent routing request in same scope reuses the scoped `IRoutingConfigProvider` (or re-resolves via the provider's own scope; no double-registration)
8. Plans 1–10 (adapters, error codes, key vault, client factory) still pass their existing tests after this merge

### Cleanup
- [ ] Drop seeded `FeatureRoutingConfig` + provider + model rows created for manual testing
- [ ] Unset `Llm:EncryptionKey` env var when done