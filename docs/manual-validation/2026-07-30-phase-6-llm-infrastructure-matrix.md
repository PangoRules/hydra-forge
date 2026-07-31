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