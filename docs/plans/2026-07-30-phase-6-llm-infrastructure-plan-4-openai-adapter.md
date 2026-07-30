# Plan 4: OpenAiCompatibleAdapter

**Branch:** `task/openai-adapter`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 4

## Steps

### 1. Create adapter class
- File: `src/HydraForge.Infrastructure/Llm/Adapters/OpenAiCompatibleAdapter.cs`
- Implements `ILlmClient`. Constructor takes `HttpClient` (named `openai-compatible`), `IKeyVault`, `LlmProvider`.

### 2. Implement `StreamChatAsync`
- POST to `{BaseUrl}/chat/completions` with `stream: true`.
- Request body: model, messages array, optional tools, `max_tokens`, `temperature`.
- Cache blocks: render as repeated system messages with stable hash prefix (OpenAI auto-caches by prefix match). Each `CacheBlock` → system message with `[cache:{hash}]` prefix.
- Parse SSE stream: read lines, extract `data: {...}` JSON chunks, yield `ChatChunk` with `Delta` (from `choices[0].delta.content`).
- Final chunk: extract `usage` from last SSE event → `UsageSnapshot`, set `FinishReason` from `choices[0].finish_reason`.
- Handle `[DONE]` sentinel.

### 3. Implement `GetModelsAsync`
- GET `{BaseUrl}/models` → parse JSON array → return `List<ProviderModel>` (id + name).

### 4. Implement `SupportsToolCalling`
- Return `true` for all models (OpenAI-compatible providers support function calling).

### 5. Register HTTP client
- In `LlmServiceCollectionExtensions`, register named `HttpClient`:
  ```csharp
  services.AddHttpClient("openai-compatible", client => { /* base timeout */ });
  ```

### 6. Unit tests
- File: `tests/HydraForge.Infrastructure.Tests/Llm/Adapters/OpenAiCompatibleAdapterTests.cs`
- Use `HttpMessageHandler` fake to assert request shape, SSE parsing, `UsageSnapshot` extraction.
- Test: cache block prefix hashing, tool calling support flag, model list parsing.

## Verification
- `dotnet build` — Infrastructure compiles.
- `dotnet test --filter "OpenAiCompatibleAdapter"` — adapter tests pass.
- No live API calls in tests.