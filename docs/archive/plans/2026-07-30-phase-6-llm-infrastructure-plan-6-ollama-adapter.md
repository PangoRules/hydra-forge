# Plan 6: OllamaAdapter

**Branch:** `task/ollama-adapter`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 6

## Steps

### 1. Create adapter class
- File: `src/HydraForge.Infrastructure/Llm/Adapters/OllamaAdapter.cs`
- Implements `ILlmClient`. Constructor takes `HttpClient` (named `ollama`), `LlmProvider`.
- `AdapterType` returns `AdapterType.Ollama`.

### 2. Implement `StreamChatAsync`
- POST to `{BaseUrl}/api/chat` with `stream: true`.
- Request body: `model`, `messages` array (role + content), optional `options` (temperature, num_predict).
- No API key needed (local) — `IKeyVault` injected but unused.
- Parse NDJSON stream: each line is a complete JSON object. Yield `ChatChunk` with `Delta` from `message.content`.
- Final message has `done: true` with `total_duration`, `eval_count`, `prompt_eval_count`.
- Extract `UsageSnapshot`: `InputTokens = prompt_eval_count`, `OutputTokens = eval_count`, `CachedTokens = 0`.
- No prompt caching support.

### 3. Implement `GetModelsAsync`
- GET `{BaseUrl}/api/tags` → parse `models` array → return `List<ProviderModel>` (name + modified_at as metadata).

### 4. Implement `SupportsToolCalling`
- Return `false` by default (Ollama tool calling is model-dependent and newer). Can be overridden per model later.

### 5. Register HTTP client
- In `LlmServiceCollectionExtensions`, register named `HttpClient` for `ollama`.

### 6. Unit tests
- File: `tests/HydraForge.Infrastructure.Tests/Llm/Adapters/OllamaAdapterTests.cs`
- Test: NDJSON stream parsing, final chunk usage extraction.
- Test: `GetModelsAsync` tag list parsing.
- Test: `CachedTokens` always 0.

## Verification
- `dotnet build`
- `dotnet test --filter "OllamaAdapter"`
- No live API calls.