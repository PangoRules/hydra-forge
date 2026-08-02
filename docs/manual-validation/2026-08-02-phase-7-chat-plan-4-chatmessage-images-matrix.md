## Validate: Phase 7 Chat Plan 4 — ChatMessage Images Extension

### Setup
- [ ] Solution builds clean: `dotnet build`
- [ ] All tests pass: `dotnet test`
- [ ] `src/HydraForge.Application/Llm/LlmDtos.cs` contains `ImageBlock` record
- [ ] `src/HydraForge.Application/Chat/ChatMessageMapper.cs` exists with both mapper methods

### Happy Path
1. Construct `ChatMessage(ChatRole.User, "hi")` (2-arg) → compiles, `Images` defaults to `null`
2. Construct `ChatMessage(ChatRole.User, "hi", [new ImageBlock("k1", "image/png")])` → compiles, `Images` has 1 block
3. Call `ChatMessageMapper.ToDomainImagesJson([block1, block2])` → returns camelCase JSON array string with both blocks
4. Round-trip: `ToApplicationImages(ToDomainImagesJson(blocks))` → returns `ImageBlock[]` with same `StorageKey`/`MediaType` values

### Edge Cases
1. `ToDomainImagesJson(null)` → returns `"[]"`
2. `ToDomainImagesJson([])` → returns `"[]"`
3. `ToApplicationImages(null)` → returns `[]`
4. `ToApplicationImages("")` → returns `[]`
5. `ToApplicationImages("not json")` → returns `[]` (no throw)
6. `ToApplicationImages("[]")` → returns `[]`
7. `RouteDecision` still constructs without `Provider` arg → `Provider` defaults to `null`

### Regressions
1. Existing `ChatMessage(Role, Content)` callers → still compile, no breaking change
2. TUI NSwag codegen → still produces `Generated/Contracts.cs` (build re-runs NSwag)
3. Existing LLM Application tests (`HydraForge.Application.Tests`) → still pass
4. `RouteDecision` consumers (model router, callers) → unchanged signature, no compile errors

### Cleanup
- [ ] None — pure DTO/mapper additions, no DB rows or runtime side effects
