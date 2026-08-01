# Plan 10: LlmClientFactory

**Branch:** `task/llm-client-factory`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 10

## Steps

### 1. Create factory class
- File: `src/HydraForge.Infrastructure/Llm/LlmClientFactory.cs`
- Implements `ILlmClientFactory`. Constructor takes `IHttpClientFactory`, `IKeyVault`.

### 2. Implement `For(LlmProvider)` → `ILlmClient`
- Switch on `provider.AdapterType`:
  - `OpenAiCompatible` → new `OpenAiCompatibleAdapter(httpClientFactory.CreateClient("openai-compatible"), keyVault, provider)`
  - `Anthropic` → new `AnthropicAdapter(httpClientFactory.CreateClient("anthropic"), keyVault, provider)`
  - `Ollama` → new `OllamaAdapter(httpClientFactory.CreateClient("ollama"), provider)`
- Cache adapters in `ConcurrentDictionary<Guid, ILlmClient>` keyed by provider ID (singleton per provider).
- API key decrypted at call time inside each adapter — never cached in factory state.

### 3. Implement `ImageFor(LlmProvider)` → `IImageClient`
- Switch on `provider.AdapterType`:
  - `DallE` → new `DallEAdapter(...)`
  - `StabilityAi` → new `StabilityAiAdapter(...)`
  - `ComfyUi` or `Diffusers` → new `ComfyUiAdapter(httpClientFactory.CreateClient("comfyui"), keyVault, provider)`
- Cache in `ConcurrentDictionary<Guid, IImageClient>`.

### 4. Implement `EmbeddingFor(LlmProvider)` → `IEmbeddingClient`
- For now, `OpenAiCompatibleAdapter` also implements `IEmbeddingClient` (OpenAI embeddings endpoint). Return same adapter cast.
- Future: dedicated embedding adapter if needed.

### 5. Register named HTTP clients
- In `LlmServiceCollectionExtensions`:
  - `openai-compatible`, `anthropic`, `ollama`, `dalle`, `stability-ai`, `comfyui`.
  - Base timeout: 120s for image adapters, 60s for text.

### 6. Register factory in DI
- `services.AddSingleton<ILlmClientFactory, LlmClientFactory>()`

### 7. Unit tests
- File: `tests/HydraForge.Infrastructure.Tests/Llm/LlmClientFactoryTests.cs`
- Test: `For()` returns correct adapter type per `AdapterType`.
- Test: `ImageFor()` returns correct image adapter.
- Test: `ComfyUi` and `Diffusers` both resolve to `ComfyUiAdapter`.
- Test: adapter caching (same provider ID → same instance).

## Verification
- `dotnet build`
- `dotnet test --filter "LlmClientFactory"`
- No live API calls.