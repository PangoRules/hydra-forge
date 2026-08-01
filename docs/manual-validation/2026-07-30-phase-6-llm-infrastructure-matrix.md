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
10. `provider.ApiKeyEncrypted` populated (non-empty) → outgoing request still has no `Authorization` header (Ollama is local-only; `IKeyVault` is injected but unused)

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