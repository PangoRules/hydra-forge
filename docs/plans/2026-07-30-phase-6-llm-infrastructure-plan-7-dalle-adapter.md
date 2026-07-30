# Plan 7: DallEAdapter

**Branch:** `task/dalle-adapter`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 7

## Steps

### 1. Create adapter class
- File: `src/HydraForge.Infrastructure/Llm/Adapters/DallEAdapter.cs`
- Implements `IImageClient`. Constructor takes `HttpClient` (named `dalle`), `IKeyVault`, `LlmProvider`.
- `AdapterType` returns `AdapterType.DallE`.

### 2. Implement `GenerateImageAsync`
- POST to `{BaseUrl}/images/generations`.
- Auth: `Authorization: Bearer {decrypted key}`.
- Request body: `model` (e.g. `dall-e-3`), `prompt`, `n` (count), `size` (map `ImageSize` enum to `1024x1024`/`1792x1024`/`1024x1792`), `response_format: "b64_json"` or `"url"`.
- Parse response: extract `data[].b64_json` or `data[].url` → return `GeneratedImage` with `ImageDataUrlsOrKeys` and `Resolution`.

### 3. Implement `InpaintAsync`
- POST to `{BaseUrl}/images/edits` (multipart form).
- Fields: `image` (base image file), `mask` (transparent areas to fill), `prompt`, `n`, `size`.
- Parse response same as generate.

### 4. Register HTTP client
- In `LlmServiceCollectionExtensions`, register named `HttpClient` for `dalle`.

### 5. Unit tests
- File: `tests/HydraForge.Infrastructure.Tests/Llm/Adapters/DallEAdapterTests.cs`
- Test: generate request shape (model, prompt, size mapping).
- Test: response parsing (b64_json and url variants).
- Test: inpaint multipart form construction.

## Verification
- `dotnet build`
- `dotnet test --filter "DallEAdapter"`
- No live API calls.