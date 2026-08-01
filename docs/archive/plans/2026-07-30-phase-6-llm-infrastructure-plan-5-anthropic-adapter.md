# Plan 5: AnthropicAdapter

**Branch:** `task/anthropic-adapter`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 5

## Steps

### 1. Create adapter class
- File: `src/HydraForge.Infrastructure/Llm/Adapters/AnthropicAdapter.cs`
- Implements `ILlmClient`. Constructor takes `HttpClient` (named `anthropic`), `IKeyVault`, `LlmProvider`.
- `AdapterType` returns `AdapterType.Anthropic`.

### 2. Implement `StreamChatAsync`
- POST to `{BaseUrl}/v1/messages` with `stream: true`.
- Anthropic-specific headers: `x-api-key` (decrypted from `IKeyVault`), `anthropic-version: 2023-06-01`.
- Request body: `model`, `max_tokens`, `messages` array, optional `system` (string or array), optional `tools`.
- Cache blocks: emit `cache_control: { type: "ephemeral" }` on system message and project-snapshot blocks. System context → `system` field with `cache_control`. Project snapshot → first user message with `cache_control`.
- Parse SSE stream: `data:` lines → JSON events. Yield `ChatChunk` with `Delta` from `delta.text` (content_block_delta events).
- Final chunk: extract `UsageSnapshot` from `message_delta.usage` (input_tokens, output_tokens). `CachedTokens` from `usage.cache_creation_input_tokens` + `cache_read_input_tokens`.
- Map `stop_reason` to `ChatChunkFinishReason`.

### 3. Implement `GetModelsAsync`
- Anthropic has no public `/models` endpoint returning structured data. Return empty list or hardcoded known models list. Log warning.

### 4. Implement `SupportsToolCalling`
- Return `true` (Anthropic supports tool use).

### 5. Register HTTP client
- In `LlmServiceCollectionExtensions`, register named `HttpClient` for `anthropic`.

### 6. Unit tests
- File: `tests/HydraForge.Infrastructure.Tests/Llm/Adapters/AnthropicAdapterTests.cs`
- Test: `cache_control` block emission on system + project-snapshot blocks.
- Test: `message_delta` usage mapping → `UsageSnapshot`.
- Test: SSE stream parsing for content_block_start/delta/stop events.

## Verification
- `dotnet build`
- `dotnet test --filter "AnthropicAdapter"`
- No live API calls.