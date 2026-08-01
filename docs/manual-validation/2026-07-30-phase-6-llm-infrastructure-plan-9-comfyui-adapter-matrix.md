## Validate: ComfyUiAdapter (Plan 9)

### Setup
- [ ] API server running with `ASPNETCORE_ENVIRONMENT=Development`
- [ ] Live ComfyUI server reachable at `BaseUrl` (or mock test container)
- [ ] `LlmProvider` row in DB with `AdapterType=ComfyUi`, `IsEnabled=true`, valid `BaseUrl`, encrypted `ApiKeyEncrypted`
- [ ] `IKeyVault` provider registered (already wired via `AddLlmInfrastructure`)

### Happy Path
1. `POST` to `/prompt` with text-to-image workflow → returns `{"prompt_id":"..."}` → 200 OK
2. Poll `GET /history/{prompt_id}` until `status.completed=true` → returns history with output images array
3. `GET /view?filename=...&subfolder=...&type=...` for each output image → returns PNG bytes
4. `adapter.GenerateImageAsync(request)` returns `Result<GeneratedImage>` with base64-encoded image
5. `adapter.InpaintAsync(request)` uploads image + mask via `POST /upload/image`, then submits inpaint workflow → returns inpainted image
6. Auth header `Authorization: Bearer <decrypted-key>` present on all three request types (prompt, history, upload, view)

### Edge Cases
1. `ImageRequest.Count > 1` → `EmptyLatentImage.batch_size` equals `request.Count` → image gen produces that many output images
2. `InpaintRequest.Count > 1` → batch_size honored on inpaint latent
3. `InpaintRequest` with `ImageBytes` null/length=0 → returns `COMFYUI_INPAINT_MISSING_INPUT` failure without any HTTP call
4. `InpaintRequest` with `MaskBytes` null/length=0 → returns `COMFYUI_INPAINT_MISSING_INPUT` failure without any HTTP call
5. Polling exceeds 5 minutes → returns `COMFYUI_HISTORY_TIMEOUT` failure (not a thrown exception)
6. Caller `CancellationToken` cancelled mid-poll → propagates as `OperationCanceledException` (not a `Result.Failure`)
7. History response has `status.error.message` set → returns `COMFYUI_GENERATION_ERROR` failure with message in body
8. History response has no output images → returns `COMFYUI_NO_IMAGES` failure
9. ComfyUI returns non-2xx on `/prompt` → returns `COMFYUI_SUBMIT_FAILED` failure
10. ComfyUI returns non-2xx on `/history` → returns `COMFYUI_HISTORY_FAILED` failure
11. ComfyUI returns non-2xx on `/upload/image` → returns `COMFYUI_UPLOAD_FAILED` failure
12. ComfyUI returns malformed JSON → returns `COMFYUI_PARSE_FAILED` failure
13. Provider has `AdapterType=Diffusers` → `adapter.AdapterType` returns `Diffusers` (same code path, same workflow API)

### Workflow Shape (Regression Targets for Cycle 2 Fixes)
1. Text-to-image workflow node 7 (`KSampler`) `latent` input → `["6", 0]` (points to `EmptyLatentImage`) — array-style ref, not `{node_id:"6"}`
2. Text-to-image workflow all node link refs use array form `[node_id, output_index]`, never `{node_id:...}` object form
3. Inpaint workflow node 11 (`KSampler`) `latent` input → `["7", 0]` (points to `VAEEncodeForInpaint`), NOT `["10", 0]` or `EmptyLatentImage`
4. Inpaint workflow no orphaned `EmptyLatentImage` node (was node 10, now removed)
5. Inpaint workflow has `LoadImage` node for the mask (node 14) — separate from the image LoadImage node (node 4)
6. Inpaint workflow node 7 `VAEEncodeForInpaint` `pixels` → `["4", 0]` (image), `mask` → `["14", 0]` (mask)
7. Status polling reads `status.completed` (not `status.executed`) — ComfyUI's actual field name

### Regressions
1. `OpenAiCompatibleAdapter`, `AnthropicAdapter`, `OllamaAdapter`, `DallEAdapter`, `StabilityAiAdapter` still resolve correctly via `AddLlmInfrastructure` — single "comfyui" HTTP client does not displace their named clients
2. `IKeyVault` encryption/decryption still works for all adapters
3. No new HTTP client named "diffusers" registered (consolidated to "comfyui" per Decision A in plan)

### Cleanup
- [ ] Remove test `LlmProvider` rows
- [ ] Delete any temp image uploads on ComfyUI side (filenames prefixed `hydraforge` / `hydraforge_inpaint`)
