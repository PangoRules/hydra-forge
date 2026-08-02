# E2E Regression Matrix — Phase 6 LLM Infrastructure

## Plan 4: OpenAiCompatibleAdapter (SSE streaming + cache prefix + GetModels)

### Setup
- [ ] Postgres up (`docker compose up -d postgres`)
- [ ] `Llm:EncryptionKey` set in shell env as base64-encoded 32-byte key
- [ ] Server builds clean: `dotnet build`
- [ ] `dotnet test --filter "FullyQualifiedName~OpenAiCompatibleAdapter"` — 15 pass
- [ ] Real OpenAI-compatible endpoint available (or stub via a local HTTP capture) — adapter is unit-tested without live calls, so manual matrix below covers only what the fakes don't already pin down

### Happy Path
1. Inject `OpenAiCompatibleAdapter(HttpClient, IKeyVault, LlmProvider)` via DI → instance obtained, `AdapterType == AdapterType.OpenAiCompatible`
2. Call `adapter.SupportsToolCalling(any ProviderModelConfigDto)` → returns `true`
3. Call `adapter.GetModelsAsync()` against a stub returning `{"data":[{"id":"gpt-4o","name":"GPT-4o","description":"Fast model"}]}` → `Result.Success` with one `ProviderModelDto`, `ModelId="gpt-4o"`, `Name="GPT-4o"`, `Description="Fast model"`
4. Call `adapter.StreamChatAsync(request)` against a stub emitting `data: {"choices":[{"delta":{"content":"Hi"}}]}` followed by `data: [DONE]` → yields a single `ChatChunk("Hi", null, null)`
5. Pass `CacheBlocks = [CacheBlock("system context", SystemContext)]` → first message in the request body is `"role":"system"` with content matching `[cache:HEX]system context` where `HEX` is a stable 16-char lowercase hex SHA256 prefix
6. Call `StreamChatAsync` twice with identical `CacheBlocks` content → both request bodies have identical `[cache:HEX]` prefixes (prefix stability)
7. `GetModelsAsync` against a stub with `{"data":[{"id":"a","name":null,...}]}` → `ProviderModelDto.Name` falls back to `id` (`Name == "a"`)
8. Provider with `ApiKeyEncrypted` populated → outgoing request carries `Authorization: Bearer <decrypted-key>` header
9. Provider with `ApiKeyEncrypted = ""` or null → no `Authorization` header on outgoing request

### Edge Cases
1. SSE stream contains a comment line (`: heartbeat`) → adapter skips it; no parse attempt, no exception
2. SSE event with malformed JSON (e.g. `data: {not-json}`) → adapter catches `JsonException`, continues to next line; does not throw
3. SSE event with null `choices` array → adapter yields no content chunk; continues
4. Non-2xx HTTP response (e.g. 503) from `/chat/completions` → adapter yields exactly one `ChatChunk(null, ChatChunkFinishReason.Error, null)` and stops
5. Non-2xx HTTP response from `/models` → `Result.Failure` with `Error.Code == "LLM_MODELS_FETCH_FAILED"`
6. `/models` returns invalid JSON → `Result.Failure` with `Error.Code == "LLM_MODELS_PARSE_FAILED"`
7. `/models` returns `{}` (missing `data`) → `Result.Success` with empty list (not an error)
8. Stream emits a `usage` chunk mid-stream → adapter captures it; final yielded chunk carries the `UsageSnapshot`
9. Stream emits `cached_tokens` in usage → final chunk's `UsageSnapshot.CachedTokens` reflects the value
10. `CacheBlocks = []` → request body contains only the original user/assistant messages (no synthetic system messages)
11. `IKeyVault.Decrypt(provider.ApiKeyEncrypted)` throws → exception propagates from the adapter call; no swallow, no fallback
12. Cancellation token cancelled mid-stream → `OperationCanceledException` propagates; stream and reader both disposed via `await using`/`using`

### Regressions
1. `dotnet ef migrations has-pending-model-changes` → clean (no entity changes in this plan)
2. `LlmServiceCollectionExtensions.AddLlmInfrastructure` still wires `IKeyVault` (AesGcm) + named `HttpClient "openai-compatible"` (120s timeout)
3. Plan 2's `AesGcmKeyVault` round-trip still works (API key decrypt path is shared)
4. `ChatChunkFinishReason` enum still has its pre-existing members plus new `Error` value (no removal/reorder)
5. `LlmDtos` records (ChatRequest, ChatMessage, ChatChunk, UsageSnapshot, ProviderModelDto, etc.) keep their prior shape — only an enum value was added

### Cleanup
- [ ] Unset `Llm:EncryptionKey` after manual test
- [ ] Drop any test `llm_providers` rows inserted via the API

## Plan 12: ContextCompressor service + DI wiring (Plan 12)

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

## Plan 5: AnthropicAdapter (SSE streaming, cache blocks, tool use, usage)

### Setup
- [ ] `Llm:EncryptionKey` set in shell env as base64-encoded 32-byte key (e.g. `export Llm__EncryptionKey="$(openssl rand -base64 32)"`)
- [ ] Postgres up (`docker compose up -d postgres`) — adapter itself needs no DB but DI registration chain does
- [ ] `dotnet build` clean
- [ ] `dotnet test --filter "FullyQualifiedName~AnthropicAdapter"` — 16 pass
- [ ] (Optional live) Real `ANTHROPIC_API_KEY` in env to validate real API calls in Edge Case 6

### Happy Path
1. Inspect `AnthropicAdapter.AdapterType` → returns `AdapterType.Anthropic`
2. Call `AnthropicAdapter.StreamChatAsync` with one `User` message, no `CacheBlocks`, no `Tools` → request body shape: `{ model, max_tokens, stream: true, messages: [{role:"user",content:"..."}] }`; no `system` field; no `tools` field (omitted via `WhenWritingNull`)
3. Call `StreamChatAsync` with `CacheBlock(SystemContext)` + `User` message → request body `system` is array of 1 block with `cache_control: { type: "ephemeral" }`; `messages` has 1 user message with no `cache_control`
4. Call `StreamChatAsync` with `CacheBlock(ProjectSnapshot)` + `User` message → first user message `content` is `[project_snapshot]\n<snapshot>\n\n<original>`; that message has `cache_control: { type: "ephemeral" }`
5. Call `StreamChatAsync` with `CacheBlock(Memory)` + `User` message → `system` array has 1 block with `text` but no `cache_control` field
6. Stream an SSE response containing `content_block_delta` events with `delta.text` → yields one `ChatChunk` per text delta, plus a final `ChatChunk` with `FinishReason` and `Usage`
7. Stream an SSE response with `message_delta.usage` → final chunk `Usage` populated: `InputTokens`, `OutputTokens`, `CachedTokens = cache_creation_input_tokens + cache_read_input_tokens`
8. Stream an SSE response with `message_delta.stop_reason: "end_turn"` → final chunk `FinishReason == ChatChunkFinishReason.Stop`; `"max_tokens"` → `Length`; `"stop_sequence"` → `Stop`; `"content_filtered"` → `ContentFilter`
9. Call `AnthropicAdapter.SupportsToolCalling(any model)` → returns `true`
10. Call `AnthropicAdapter.GetModelsAsync()` → returns `Result.Success(empty array)`; logs a `Warning` containing "does not support listing models"
11. Call `StreamChatAsync` with `Tools = [ToolDefinition("get_weather", desc, params)]` where one param is `IsRequired: true` and one is false → `tools[0].input_schema` is `{ type: "object", properties: {...}, required: ["<required-param-name>"] }` (never serialised as a raw parameter array)

### Edge Cases
1. Call `StreamChatAsync` with multiple `User` messages and one `ProjectSnapshot` block → snapshot prepended to the **first** user message only; subsequent user messages have no `cache_control` and no `[project_snapshot]` prefix
2. Call `StreamChatAsync` with `ChatRole.System` message in `Messages` → that text lands in the `system` array (without `cache_control`); it does not appear inside `messages[]`
3. Configure `provider.ApiKeyEncrypted = ""` → outgoing HTTP request has no `x-api-key` header (but still has `anthropic-version: 2023-06-01`)
4. Configure `provider.ApiKeyEncrypted = "v1:..."` (any non-empty ciphertext) → outgoing request `x-api-key` equals `IKeyVault.Decrypt(ciphertext)`
5. Anthropic returns non-2xx (e.g. 400/500) → adapter logs `Anthropic API error {StatusCode}: {ResponseBody}` at `Error` level; yields a single `ChatChunk(null, FinishReason.Error, null)` then breaks
6. (Live optional) Point `BaseUrl` at `https://api.anthropic.com` with a real `ANTHROPIC_API_KEY`; call `StreamChatAsync` for `claude-sonnet-4-20250514` → yields real text deltas + non-null final `Usage` with non-zero `CachedTokens` after the second call (cache hit)
7. SSE event with malformed JSON payload in a `data:` line → adapter skips that line, does not throw, continues processing subsequent lines
8. Multiple `CacheBlock(ProjectSnapshot)` blocks → all joined with `\n` and prepended as a single prefix to the first user message (still only one cache breakpoint)
9. `MaxOutputTokens` null → request body `max_tokens == 4096` (fallback)
10. `provider.BaseUrl` has trailing slash → request URI is `{baseUrl}/v1/messages` (no double slash)

### Regressions
1. `dotnet ef migrations has-pending-model-changes` → clean (adapter adds no entity changes)
2. `dotnet test` (full suite) — still passes; no other test fixtures broken by the new `anthropic` named HttpClient registration in `AddLlmInfrastructure`
3. `OpenAiCompatibleAdapter` tests still pass — adapter co-exists in same `AddLlmInfrastructure` registration block without collision
4. `IKeyVault` registration in `AddLlmInfrastructure` unchanged — `AesGcmKeyVault` round-trip still works
5. Admin Web UI can render an `LlmProvider` row with `AdapterType = Anthropic` (or, if Phase 6 UI not yet shipped, the provider DTO accepts `AdapterType.Anthropic` without enum-parsing errors)

### Cleanup
- [ ] Unset `Llm:EncryptionKey` after manual test
- [ ] If a real `ANTHROPIC_API_KEY` was used in Edge Case 6, rotate it after testing

## Plan 6: OllamaAdapter (NDJSON streaming + /api/tags + no tool calling)

### Setup
- [ ] Postgres up (`docker compose up -d postgres`)
- [ ] `Llm:EncryptionKey` set in shell env as base64-encoded 32-byte key
- [ ] `dotnet build` clean
- [ ] `dotnet test --filter "FullyQualifiedName~OllamaAdapter"` — 10 pass
- [ ] Ollama running locally (default `http://localhost:11434`) with at least one model pulled (`ollama pull llama3.2`) for live Happy Path 8; adapter is otherwise unit-tested without live calls

### Happy Path
1. Inspect `OllamaAdapter.AdapterType` → returns `AdapterType.Ollama`
2. Call `OllamaAdapter.SupportsToolCalling(any ProviderModelConfigDto)` → returns `false`
3. Call `adapter.StreamChatAsync(request)` with one `User` message → request body shape: `{ model, messages:[{role:"user",content:"..."}], stream:true, options:{ temperature, num_predict } }`; URL is `{baseUrl}/api/chat`; no `Authorization` header
4. Stream an NDJSON response with three content chunks (`Hello`, `!`, ` world`) followed by a final chunk with `done:true, prompt_eval_count:15, eval_count:157` → adapter yields 4 `ChatChunk`s: three with matching `Delta` values and one terminal chunk with `Delta=null`, `FinishReason=Stop`, `Usage=(15,157,0)`
5. Stream an NDJSON response whose final chunk has only `prompt_eval_count` and `eval_count` (no `cached_tokens` field, no `total_duration`) → terminal `ChatChunk.Usage.CachedTokens == 0`
6. Call `GetModelsAsync()` against a stub returning `{"models":[{"name":"llama3.2:latest","modified_at":"2024-01-01T00:00:00Z","size":...,"digest":"sha256:..."}]}` → `Result.Success` with one `ProviderModelDto`; `ModelId` and `Name` both equal `llama3.2:latest`; `Metadata["modified_at"] == "2024-01-01T00:00:00Z"`
7. Call `GetModelsAsync()` against a stub returning `{}` (no `models` key) → `Result.Success` with empty list (not an error)
8. (Live) Point `BaseUrl` at a real local Ollama instance with `llama3.2` pulled → `StreamChatAsync` yields real text deltas and a terminal chunk with non-zero `Usage`; `GetModelsAsync` returns the locally-available tag list including `llama3.2:latest`

### Edge Cases
1. NDJSON response contains an empty line between chunks → adapter skips it (no parse attempt, no exception)
2. NDJSON response contains a malformed JSON line → adapter catches `JsonException`, continues to next line, does not throw
3. NDJSON chunk with `message.content == ""` (interim frames Ollama emits before the final frame) → adapter yields no chunk for that line (empty content guard); final `done:true` frame still produces the terminal chunk
4. Non-2xx HTTP response (e.g. 503) from `/api/chat` → adapter yields exactly one `ChatChunk(null, ChatChunkFinishReason.Error, null)` and stops
5. Non-2xx HTTP response from `/api/tags` → `Result.Failure` with `Error.Code == "LLM_MODELS_FETCH_FAILED"`
6. `/api/tags` returns invalid JSON → `Result.Failure` with `Error.Code == "LLM_MODELS_PARSE_FAILED"`
7. `provider.BaseUrl` has trailing slash → request URI is `{baseUrl}/api/chat` and `{baseUrl}/api/tags` (no double slash)
8. `request.MaxOutputTokens` null and `request.Temperature` null → `options` field is still emitted as `{ temperature:null, num_predict:null }` (acceptable to Ollama; `null` omitted via `WhenWritingNull` if both fields are absent) — verify Ollama accepts the request
9. `request.Tools` populated → adapter does NOT emit a `tools` field; `SupportsToolCalling` is `false`, so the routing layer should never pass tools for an Ollama provider (regression guard for routing logic)
10. `provider.ApiKeyEncrypted` populated (non-empty) → outgoing request still has no `Authorization` header (Ollama is local-only; `IKeyVault` is not injected)

### Regressions
1. `dotnet ef migrations has-pending-model-changes` → clean (no entity changes in this plan)
2. `dotnet test` (full suite) — still passes; no other test fixtures broken by the new `ollama` named HttpClient registration in `AddLlmInfrastructure`
3. `OpenAiCompatibleAdapter` and `AnthropicAdapter` tests still pass — three named HttpClients coexist in the same `AddLlmInfrastructure` registration block without collision
4. `IKeyVault` registration in `AddLlmInfrastructure` unchanged — `AesGcmKeyVault` round-trip still works (Plan 2)
5. `AdapterType` enum still has its pre-existing members plus the `Ollama = 3` value used here (no removal/reorder)
6. `ChatChunkFinishReason` enum still has `Stop`, `Length`, `ContentFilter`, `ToolCalls`, `Error` (Plan 4 added `Error`; Plan 6 reuses it)
7. `LlmDtos` records (ChatRequest, ChatMessage, ChatChunk, UsageSnapshot, ProviderModelDto, etc.) keep their prior shape — no shape changes in this plan

### Cleanup
- [ ] Unset `Llm:EncryptionKey` after manual test
- [ ] Stop local Ollama if started solely for this matrix (`ollama stop` or kill the process)

## Plan 9: ComfyUiAdapter (workflow API, poll loop, inpaint)

### Setup
- [ ] API server running with `ASPNETCORE_ENVIRONMENT=Development`
- [ ] Live ComfyUI server reachable at `BaseUrl` (or mock test container)
- [ ] `LlmProvider` row in DB with `AdapterType=ComfyUi`, `IsEnabled=true`, valid `BaseUrl`, encrypted `ApiKeyEncrypted`
- [ ] `IKeyVault` provider registered (already wired via `AddLlmInfrastructure`)

### Happy Path
1. `POST` to `/prompt` with text-to-image workflow → returns `{"prompt_id":"..."}` → 200 OK
2. Poll `GET /history/{prompt_id}` until `status.completed=true` → returns history with output images array
3. `GET /view?filename=...&subfolder=...&type=...` for each output image → returns PNG bytes
4. `adapter.GenerateImageAsync(request)` returns `Result<GeneratedImage>` with base64-encoded image
5. `adapter.InpaintAsync(request)` uploads image + mask via `POST /upload/image`, then submits inpaint workflow → returns inpainted image
6. Auth header `Authorization: Bearer <decrypted-key>` present on all three request types (prompt, history, upload, view)

### Edge Cases
1. `ImageRequest.Count > 1` → `EmptyLatentImage.batch_size` equals `request.Count` → image gen produces that many output images
2. `InpaintRequest.Count > 1` → batch_size honored on inpaint latent
3. `InpaintRequest` with `ImageBytes` null/length=0 → returns `COMFYUI_INPAINT_MISSING_INPUT` failure without any HTTP call
4. `InpaintRequest` with `MaskBytes` null/length=0 → returns `COMFYUI_INPAINT_MISSING_INPUT` failure without any HTTP call
5. Polling exceeds 5 minutes → returns `COMFYUI_HISTORY_TIMEOUT` failure (not a thrown exception)
6. Caller `CancellationToken` cancelled mid-poll → propagates as `OperationCanceledException` (not a `Result.Failure`)
7. History response has `status.error.message` set → returns `COMFYUI_GENERATION_ERROR` failure with message in body
8. History response has no output images → returns `COMFYUI_NO_IMAGES` failure
9. ComfyUI returns non-2xx on `/prompt` → returns `COMFYUI_SUBMIT_FAILED` failure
10. ComfyUI returns non-2xx on `/history` → returns `COMFYUI_HISTORY_FAILED` failure
11. ComfyUI returns non-2xx on `/upload/image` → returns `COMFYUI_UPLOAD_FAILED` failure
12. ComfyUI returns malformed JSON → returns `COMFYUI_PARSE_FAILED` failure
13. Provider has `AdapterType=Diffusers` → `adapter.AdapterType` returns `Diffusers` (same code path, same workflow API)

### Workflow Shape (Regression Targets for Cycle 2 Fixes)
1. Text-to-image workflow node 7 (`KSampler`) `latent` input → `["6", 0]` (points to `EmptyLatentImage`) — array-style ref, not `{node_id:"6"}`
2. Text-to-image workflow all node link refs use array form `[node_id, output_index]`, never `{node_id:...}` object form
3. Inpaint workflow node 11 (`KSampler`) `latent` input → `["7", 0]` (points to `VAEEncodeForInpaint`), NOT `["10", 0]` or `EmptyLatentImage`
4. Inpaint workflow no orphaned `EmptyLatentImage` node (was node 10, now removed)
5. Inpaint workflow has `LoadImage` node for the mask (node 14) — separate from the image LoadImage node (node 4)
6. Inpaint workflow node 7 `VAEEncodeForInpaint` `pixels` → `["4", 0]` (image), `mask` → `["14", 0]` (mask)
7. Status polling reads `status.completed` (not `status.executed`) — ComfyUI's actual field name

### Regressions
1. `OpenAiCompatibleAdapter`, `AnthropicAdapter`, `OllamaAdapter`, `DallEAdapter`, `StabilityAiAdapter` still resolve correctly via `AddLlmInfrastructure` — single "comfyui" HTTP client does not displace their named clients
2. `IKeyVault` encryption/decryption still works for all adapters
3. No new HTTP client named "diffusers" registered (consolidated to "comfyui" per Decision A in plan)

### Cleanup
- [ ] Remove test `LlmProvider` rows
- [ ] Delete any temp image uploads on ComfyUI side (filenames prefixed `hydraforge` / `hydraforge_inpaint`)

## Plan 15: FeatureRoutingConfig startup seeder

### Setup
- [ ] Start PostgreSQL with schema migrations enabled (`Database:ApplyMigrationsOnStartup=true`).
- [ ] Ensure `feature_routing_configs` is empty before first startup.

### Happy Path
1. Start server → migration completes and startup succeeds.
2. Query `feature_routing_configs` → exactly 11 rows exist, one for each `AiFeature` enum value.
3. Inspect seeded tiers → values match Plan 15: chat/edit/review/image features use specified Standard/Premium pairs; research/pipeline use Premium/null; extraction/classification/gallery use Economy/Standard.

### Edge Cases
1. Restart server with all 11 rows present → startup succeeds and row count remains 11.
2. Query DeepResearch and AgentPipeline rows → `max_user_tier` is SQL NULL, preserving locked-to-default semantics.
3. Start server with a partially populated table → verify behavior is understood before release; current `AnyAsync()` guard skips insertion, so no missing rows are repaired.

### Regressions
1. Run admin seeding on startup → existing admin seed behavior remains successful.
2. Start server with development test-user seeding enabled → test-user seeding still runs after routing seeding.

### Cleanup
- [ ] Remove manually inserted routing rows only if test DB must be reset; otherwise retain seeded baseline.

## Plan 17: /api/account/usage endpoint

### Setup
- [ ] Postgres + MinIO up (`docker compose up -d postgres minio`)
- [ ] `Llm:EncryptionKey` set in shell env (32-byte base64; e.g. the test key `0YEf4ZBA47CpqWSH0ZczKZ62owvbQ7T5IRfcecZ4Vgo=` for local)
- [ ] Server builds clean: `dotnet build`
- [ ] `dotnet test --filter "FullyQualifiedName~AccountControllerTests"` — 3 pass
- [ ] `dotnet run --project src/HydraForge.Server` up with a known seeded user (any user with login + JWT)
- [ ] Issue a JWT for that user (use the Web UI login flow or a local token-issuer script)

### Happy Path
1. `curl -i -H "Authorization: Bearer <jwt>" http://localhost:5000/api/account/usage` → `200 OK`, JSON body with all 7 top-level fields present: `tokensUsed`, `tokensBudget`, `imagesUsed`, `imagesBudget`, `periodStart`, `periodEnd`, `recentCalls`
2. `recentCalls[].feature` and `recentCalls[].model` are non-empty strings; each item also has `tokens` (int ≥ 0), `images` (int ≥ 0), `cost` (decimal ≥ 0), `timestamp` (ISO-8601)
3. Body has `content-type: application/json` and any deserialized `recentCalls` list sorts by `timestamp` descending
4. A user with no usage history and no `UserTokenBudget` row → `tokensUsed=0`, `tokensBudget=0`, `imagesUsed=0`, `imagesBudget=0`, `recentCalls=[]`, both period fields populated to a sensible current-month window

### Edge Cases
1. No `Authorization` header → `401 Unauthorized`
2. `Authorization: Bearer <expired-jwt>` → `401 Unauthorized`
3. `Authorization: Bearer <garbage>` → `401 Unauthorized`
4. `tokensBudget == 0` in DB (unlimited) → response echoes `tokensBudget: 0`; client interprets 0 as unlimited per DTO contract
5. Period window straddles a month boundary (periodStart=2026-07-15, periodEnd=2026-08-14) → only `token_usage_records` / `image_usage_records` with `created_at` inside the window are summed and surfaced in `recentCalls`
6. A user with >20 token+image rows in the period → `recentCalls` is capped at 20 items, sorted by timestamp desc across both types (interleaved, not "20 tokens then images")
7. Two different users with overlapping usage → each user's `GET /api/account/usage` only returns their own rows (no cross-user bleed)

### Regressions
1. `dotnet ef migrations has-pending-model-changes` → clean (no schema change in this plan)
2. `dotnet test` — full suite still green (previous plan suites unaffected)
3. `ILlmAdminService` other methods (provider CRUD, model CRUD, routing, budget) still resolve via DI in `WebApplicationFactory<Program>` test fixtures
4. JWT role-claim fix (Plan 5) unaffected — `[Authorize(Policy = AuthPolicies.UserIdRequired)]` still extracts the userId from `NameIdentifier`

### Cleanup
- [ ] Unset `Llm:EncryptionKey` after manual test

## Plan 21: Hangfire + SystemSettings.AiNarrativeGenerationTimeUtc

### Setup
- [ ] Server running (`docker compose up` + `dotnet run --project src/HydraForge.Server`)
- [ ] Web dev server running (`cd src/web-ui && pnpm dev`)
- [ ] Logged in as admin (role = `Admin`)
- [ ] Browser has `auth_token` cookie set by login (verify in devtools)
- [ ] DB migrated — `system_settings` row has `ai_narrative_generation_time_utc = '00:00:00'` (verify: `psql -p 5433 -d hydraforge -c "SELECT \"AiNarrativeGenerationTimeUtc\" FROM system_settings;"`)

### Happy Path
1. Navigate to `/hangfire` → Hangfire dashboard loads, shows empty job list (no recurring jobs registered yet — that's expected; this plan only wires the infrastructure)
2. Navigate to `/admin/settings` → "AI Narrative Generation Time" time picker shows `00:00` (default)
3. Change time to `03:30`, click "Save Retention" → toast "Settings saved...", reload page → picker shows `03:30`
4. PUT `{"aiNarrativeGenerationTimeUtc": "01:15:00"}` to `/api/admin/settings` via curl → 200 OK, DB row updated to `01:15:00`, `GET /api/admin/settings` reflects `01:15:00`
5. Set time to `23:59`, save → DB row updated, persists across reload

### Edge Cases
1. PUT `/api/admin/settings` with body `{}` → 200 OK, all fields unchanged (including AiNarrativeGenerationTimeUtc). Regression check for the Cycle 7 bug where empty body cleared the schedule
2. PUT `/api/admin/settings` with `{"archivedItemRetentionDays": 500}` (only retention, no aiNarrativeTime field) → 200 OK, AiNarrativeGenerationTimeUtc preserved at prior value (not nulled)
3. PUT `/api/admin/settings` with `{"aiNarrativeGenerationTimeUtc": null}` → 200 OK, DB row's AiNarrativeGenerationTimeUtc becomes NULL (explicit clear works)
4. PUT `/api/admin/settings` with body `null` (literal JSON null) → 400 BadRequest with ProblemDetails "Request body is required."
5. PUT `/api/admin/settings` with malformed JSON (e.g. `{`) → 400 BadRequest with ProblemDetails "Invalid JSON in request body."
6. PUT `/api/admin/settings` with empty body → 400 BadRequest "Request body is required."
7. Non-admin user navigates to `/hangfire` → 401/403 redirect, dashboard does not load (AdminRequiredAuthFilter rejects)
8. Logged-out user navigates to `/hangfire` → redirected to login (cookie auth fails, no `auth_token` cookie)
9. While logged in as admin, navigate directly to `/hangfire` in browser → dashboard loads (cookie auth path in JWT bearer events fires, filter passes)
10. Time picker cleared (set to empty) and saved → DB row's AiNarrativeGenerationTimeUtc becomes NULL

### Regressions
1. All existing `/api/admin/*` endpoints still work (users, projects, audit-log, settings GET)
2. `GET /api/admin/settings` response includes `aiNarrativeGenerationTimeUtc` field
3. Existing settings fields (retention, ntfy, searxng, branding) still save correctly via the form
4. Non-admin cannot reach `/admin/settings` (redirected to `/projects`)
5. Server tests still pass: `dotnet test` shows 933 passed
6. EF migration model is clean: `dotnet ef migrations has-pending-model-changes` reports "No changes"
7. Hangfire dashboard does NOT load in Test environment (factory `WebApplicationFactory<Program>` skips `AddHangfire` + `UseHangfireDashboard` via `IsEnvironment("Test")` check — verify in `AdminControllerTests` that admin endpoints still respond)
8. Other admin pages (`/admin/providers`, `/admin/provider-models`, `/admin/routing`, `/admin/users`) still load
9. SignalR hubs, board view, project list still functional

### Cleanup
- [ ] Reset `AiNarrativeGenerationTimeUtc` to `00:00:00` if desired

## Plan 22: AiNarrative recurring job

### Setup
- [ ] Server running via `docker compose up -d postgres minio` and `dotnet run --project src/HydraForge.Server` with `ASPNETCORE_ENVIRONMENT=Development`
- [ ] Hangfire dashboard reachable at `/hangfire` with admin credentials
- [ ] At least one active project exists with a populated `ProjectContextSnapshot.TemplateContent` (state changes trigger snapshot refresh)
- [ ] Admin → AI page shows an enabled LLM provider + project-chat route

### Happy Path
1. Trigger `ai-narrative-gen` from Hangfire dashboard → job executes, completes successfully (no red entries) within ~30s for one project.
2. Query DB: `SELECT "AiNarrative", "AiNarrativeGeneratedAt" FROM project_context_snapshots WHERE "ProjectId" = <id>` → `AiNarrative` is 3–5 sentence narrative describing the project; `AiNarrativeGeneratedAt` within last few minutes (UTC).
3. Open Web UI on that project's board page → narrative appears in any narrative display surface (or `GET /api/projects/{id}/snapshot` returns it).
4. Wait for cron time matching `SystemSettings.AiNarrativeGenerationTimeUtc`, OR restart server to register cron at different time → Hangfire shows `ai-narrative-gen` with the configured `Cron.Daily(h, m)` schedule.

### Edge Cases
1. Project exists with no `ProjectContextSnapshot` row → job iterates active projects, skips projects missing snapshot (no narrative written). No exception.
2. Archived project (`ArchivedAt IS NOT NULL`) → skipped; `AiNarrative` unchanged for that project.
3. LLM provider unreachable / `StreamChatAsync` throws → job logs error for that project, continues to next project, completes overall.
4. `StreamChatAsync` returns empty stream (no deltas) → warning logged for that project, snapshot not updated (`AiNarrative` not overwritten with empty).
5. `ModelRouter.ResolveAsync` returns `Failure` → warning logged, project skipped, no crash.

### Regressions
1. Existing mutation-triggered `ProjectSnapshotRefresher.RefreshAsync` still populates `TemplateContent` instantly on card/column changes (unchanged).
2. `GET /api/projects/{projectId}/ProjectSnapshot` still returns the full snapshot including any updated `AiNarrative`.
3. Admin settings page (Phase 5/6) still saves `AiNarrativeGenerationTimeUtc` without breaking the field schema.
4. Other Hangfire recurring jobs (audit, housekeeping) still registered and run.
5. All 7 application tests + 4 new `GenerateAiNarrative` tests still pass with `dotnet test`.

### Cleanup
- [ ] No test data needs removal; the job only writes `AiNarrative` and timestamp on real snapshots.