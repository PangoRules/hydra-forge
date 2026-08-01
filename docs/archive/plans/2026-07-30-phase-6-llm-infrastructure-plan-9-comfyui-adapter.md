# Plan 9: ComfyUiAdapter

**Branch:** `task/comfyui-adapter`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 9

## Steps

### 1. Create adapter class
- File: `src/HydraForge.Infrastructure/Llm/Adapters/ComfyUiAdapter.cs`
- Implements `IImageClient`. Constructor takes `HttpClient` (named `comfyui`), `IKeyVault`, `LlmProvider`.
- Serves both `AdapterType.ComfyUi` and `AdapterType.Diffusers` (decision A). `AdapterType` property returns the provider's actual type.

### 2. Implement `GenerateImageAsync`
- POST to `{BaseUrl}/prompt` with ComfyUI workflow JSON.
- Workflow template: load checkpoint, encode prompt (CLIPTextEncode), empty latent image, KSampler, VAE decode, save image.
- Inject `prompt` into positive CLIPTextEncode node, `negative_prompt` (default "bad quality") into negative node.
- Set latent dimensions from `ImageSize` mapping.
- Response: `{ "prompt_id": "..." }`.
- Poll `{BaseUrl}/history/{prompt_id}` until complete (status `"success"` or `"error"`). Timeout after 5 minutes.
- Extract output images from history → base64-encode → return `GeneratedImage`.

### 3. Implement `InpaintAsync`
- Same workflow API but with inpaint-specific nodes: load image + mask, VAE encode for inpaint, KSampler with denoise.
- Upload base image and mask as temporary files via `{BaseUrl}/upload/image`.

### 4. Diffusers path
- When `AdapterType == Diffusers`, use same workflow API but with a diffusers-specific checkpoint node. Default: same workflow surface (open question 3 resolved to "workflow API for both").

### 5. Register HTTP client
- In `LlmServiceCollectionExtensions`, register named `HttpClient` for `comfyui`.

### 6. Unit tests
- File: `tests/HydraForge.Infrastructure.Tests/Llm/Adapters/ComfyUiAdapterTests.cs`
- Test: workflow JSON construction with injected prompt.
- Test: history polling loop (mock success/error/timeout).
- Test: image extraction from history response.

## Verification
- `dotnet build`
- `dotnet test --filter "ComfyUiAdapter"`
- No live API calls.