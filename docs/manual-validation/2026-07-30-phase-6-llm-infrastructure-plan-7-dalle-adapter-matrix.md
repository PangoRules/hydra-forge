# E2E Manual Validation Matrix — Phase 6 LLM Infrastructure

## Plan 7: DallEAdapter (image generation + inpainting)

### Setup
- [ ] Postgres up (`docker compose up -d postgres`) — adapter itself needs no DB but DI registration chain does
- [ ] `Llm:EncryptionKey` set in shell env as base64-encoded 32-byte key (e.g. `export Llm__EncryptionKey="$(openssl rand -base64 32)"`)
- [ ] `dotnet build` clean — 0 errors, 0 warnings
- [ ] `dotnet test --filter "FullyQualifiedName~DallEAdapter"` — 19 pass (16 `[Fact]` + 3 `[Theory]` size-mapping cases)
- [ ] (Optional live) Real OpenAI API key in env to validate Happy Path 5; otherwise the matrix is exercised against the existing `JsonBodyHandler` / `MultipartBodyHandler` fakes, which pin down shape, auth, and error paths

### Happy Path
1. Inspect `DallEAdapter.AdapterType` → returns `AdapterType.DallE`
2. Call `GenerateImageAsync(ImageRequest(Guid, "dall-e-3", "A sunset over the ocean", ImageSize.Square1024, 1))` against a stub returning `{"data":[]}` → outgoing request: `POST {baseUrl}/images/generations`; body is `{"model":"dall-e-3","prompt":"A sunset over the ocean","n":1,"size":"1024x1024","response_format":"url"}`; `Authorization: Bearer <decrypted-key>` (when `ApiKeyEncrypted` is populated)
3. Call `GenerateImageAsync` with `ImageSize.Landscape1792` → body `size:"1792x1024"`; `ImageSize.Portrait1024` → `size:"1024x1792"`
4. Stub returns `{"data":[{"url":"https://example.com/img1.png"},{"url":"https://example.com/img2.png"}]}` → `Result.Success(new GeneratedImage(["https://example.com/img1.png","https://example.com/img2.png"], "1024x1024"))` (resolution = the mapped size string)
5. Converted b64 variant: stub returns `{"data":[{"b64_json":"SGVsbG8="},{"b64_json":"VGVzdA=="}]}` → `Result.Success` with two `ImageDataUrlsOrKeys` carrying the raw b64 strings (no base64 decode); `url` field empty/null is ignored
6. Mixed variant: stub returns `{"data":[{"b64_json":"abc"},{"url":"https://example.com/x.png"}]}` → both land in `ImageDataUrlsOrKeys` in order
7. Call `InpaintAsync(InpaintRequest(Guid, "dall-e-3", "Remove background", new byte[]{0x89,0x50,0x4E,0x47}, new byte[]{0x01}, ImageSize.Square1024))` against a stub returning `{"data":[{"url":"https://example.com/inpainted.png"}]}` → outgoing request: `POST {baseUrl}/images/edits` with `Content-Type: multipart/form-data`; parts in order: `image` (filename `image.png`, exact bytes, `image/png`), `mask` (filename `mask.png`, exact bytes, `image/png`), `prompt="Remove background"`, `model="dall-e-3"`, `n="1"`, `size="1024x1024"`

### Edge Cases
1. `GenerateImageAsync` against a stub returning HTTP 429 → `Result.Failure(Error("DALLE_GENERATE_FAILED", "DallE image generation failed: 429"))`
2. `GenerateImageAsync` against a stub returning HTTP 200 with body `not json {{{` → `Result.Failure(Error("DALLE_PARSE_FAILED", ...))`
3. `GenerateImageAsync` against a stub returning `{"data":[]}` → `Result.Failure(Error("DALLE_EMPTY_RESPONSE", ...))`
4. `GenerateImageAsync` against a stub returning `{"data":[{}]}` (no `b64_json`, no `url`) → `Result.Failure(Error("DALLE_NO_IMAGE_DATA", ...))`
5. `GenerateImageAsync` against a stub returning `{"data":null}` → `Result.Failure(Error("DALLE_EMPTY_RESPONSE", ...))`
6. `GenerateImageAsync` with `provider.ApiKeyEncrypted = ""` → outgoing request has no `Authorization` header (existing `AddAuthHeader` short-circuits on `IsNullOrWhiteSpace`)
7. `InpaintAsync` with `ImageBytes = null!` → `Result.Failure(Error("DALLE_INPAINT_MISSING_INPUT", "ImageBytes is required for inpainting."))`; HTTP request is never sent
8. `InpaintAsync` with `ImageBytes = Array.Empty<byte>()` → `Result.Failure(Error("DALLE_INPAINT_MISSING_INPUT", ...))`; HTTP request is never sent
9. `InpaintAsync` with `MaskBytes = null!` or `Array.Empty<byte>()` → `Result.Failure(Error("DALLE_INPAINT_MISSING_INPUT", "MaskBytes is required for inpainting."))`; HTTP request is never sent
10. `InpaintAsync` against a stub returning HTTP 400 → `Result.Failure(Error("DALLE_INPAINT_FAILED", ...))`
11. `InpaintAsync` passes byte arrays by reference, not by copy — fast-fail runs *before* any HTTP work, so a null/empty payload is cheap to reject (no `MultipartFormDataContent` allocation)
12. `provider.BaseUrl` has trailing slash (e.g. `https://api.openai.com/`) → request URI is `https://api.openai.com/images/generations` and `https://api.openai.com/images/edits` (no double slash); the adapter calls `TrimEnd('/')` once per call
13. (Live optional) Point `BaseUrl` at `https://api.openai.com` with a real `OPENAI_API_KEY`; call `GenerateImageAsync(ImageRequest(..., "dall-e-3", "A sunset over the ocean", ImageSize.Square1024, 1))` → `Result.Success` with one `ImageDataUrlsOrKeys` entry that is a valid `https://*.openai.com/...` URL

### Regressions
1. `dotnet ef migrations has-pending-model-changes` → clean (adapter adds no entity changes; `InpaintRequest` shape change from `ImageKey/MaskKey` to `ImageBytes/MaskBytes` is a record-shape change in Application, not an EF entity)
2. `dotnet test` (full suite) — still passes; no other test fixtures broken by the new `dalle` named HttpClient registration in `AddLlmInfrastructure`
3. `OpenAiCompatibleAdapter`, `AnthropicAdapter`, `OllamaAdapter` tests still pass — four named HttpClients coexist in the same `AddLlmInfrastructure` registration block without collision
4. `IKeyVault` registration in `AddLlmInfrastructure` unchanged — `AesGcmKeyVault` round-trip still works (Plan 2)
5. `AdapterType` enum still has its pre-existing members plus the `DallE = 6` value used here (no removal/reorder)
6. `LlmDtos` records — `InpaintRequest` is now `(ProviderModelConfigId, ModelId, Prompt, ImageBytes, MaskBytes, Size, Count = 1)`; downstream callers that previously constructed `InpaintRequest` with `ImageKey`/`MaskKey` will need to fetch the bytes via `IFileStore` before constructing the request. Verify no other code in `src/` or `tests/` still references the old shape (`grep -r "ImageKey\|MaskKey" src/ tests/` returns no `InpaintRequest` call sites today, but the change is API-breaking and must propagate to any future caller)
7. `IImageClient` interface unchanged — still `GenerateImageAsync` / `InpaintAsync` returning `Result<GeneratedImage>`. The byte-array shape is contained inside `InpaintRequest` and does not leak into the port
8. `GeneratedImage` shape unchanged — `ImageDataUrlsOrKeys` (mixed list of b64 or url strings) and `Resolution` (mapped size string)

### Cleanup
- [ ] Unset `Llm:EncryptionKey` after manual test
- [ ] If a real `OPENAI_API_KEY` was used in Edge Case 13, rotate it after testing
- [ ] No test data persists in DB (adapter does not write)
