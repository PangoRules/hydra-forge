# Plan 8: StabilityAiAdapter

**Branch:** `task/stability-ai-adapter`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 8

## Steps

### 1. Create adapter class
- File: `src/HydraForge.Infrastructure/Llm/Adapters/StabilityAiAdapter.cs`
- Implements `IImageClient`. Constructor takes `HttpClient` (named `stability-ai`), `IKeyVault`, `LlmProvider`.
- `AdapterType` returns `AdapterType.StabilityAi`.

### 2. Implement `GenerateImageAsync`
- POST to `{BaseUrl}/v2beta/stable-image/generate/{engine}` (e.g. `sd3.5-large`).
- Auth: `Authorization: Bearer {decrypted key}`, `Accept: image/*` or `application/json`.
- Request body (multipart): `prompt`, `negative_prompt`, `aspect_ratio` (map from `ImageSize`), `output_format: "png"`, `seed: 0` (random).
- Parse response: if `Accept: image/*` → binary image data → base64-encode. If JSON → extract `image` base64 field.
- Return `GeneratedImage` with single image (Stability generates one per call; loop for `Count`).

### 3. Implement `InpaintAsync`
- POST to `{BaseUrl}/v2beta/stable-image/edit/inpaint`.
- Multipart: `image` (base), `mask`, `prompt`, `output_format`.
- Parse same as generate.

### 4. Register HTTP client
- In `LlmServiceCollectionExtensions`, register named `HttpClient` for `stability-ai`.

### 5. Unit tests
- File: `tests/HydraForge.Infrastructure.Tests/Llm/Adapters/StabilityAiAdapterTests.cs`
- Test: generate request shape, multipart fields.
- Test: binary response → base64 conversion.
- Test: inpaint endpoint.

## Verification
- `dotnet build`
- `dotnet test --filter "StabilityAiAdapter"`
- No live API calls.