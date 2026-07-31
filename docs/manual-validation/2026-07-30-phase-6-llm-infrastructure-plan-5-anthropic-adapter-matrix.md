## Validate: Phase 6 Plan 5 — AnthropicAdapter (SSE streaming, cache blocks, tool use, usage)

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
