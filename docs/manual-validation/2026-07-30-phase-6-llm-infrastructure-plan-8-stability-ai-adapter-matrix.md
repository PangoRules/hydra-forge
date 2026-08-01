## Validate: Phase 6 Plan 8 — StabilityAiAdapter (generate + inpaint)

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
