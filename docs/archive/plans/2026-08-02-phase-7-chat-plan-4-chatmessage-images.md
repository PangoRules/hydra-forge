# Plan 4: ChatMessage Images Extension (F5)
**Branch:** `task/chatmessage-images`
**Parent branch:** `feat/phase-7-chat`
**Parent spec:** `2026-08-02-phase-7-chat-design.md` — Task 4

**Goal:** Extend `Application.Llm.ChatMessage` with `IReadOnlyList<ImageBlock>? Images`. Add `ImageBlock` DTO + mapper between Domain `ChatMessage.ImagesJson` and Application `ChatMessage.Images`.

**Files:**
- Modify: `src/HydraForge.Application/Llm/LlmDtos.cs`
- Create: `src/HydraForge.Application/Chat/ChatMessageMapper.cs`

**Steps:**

- [x] Add `ImageBlock` record: `(string StorageKey, string MediaType)`
- [x] Extend `Application.Llm.ChatMessage` record: add `IReadOnlyList<ImageBlock>? Images = null` (forward-compat default)
- [x] Create `ChatMessageMapper`: `ToDomainImagesJson(images)` → JSON string, `ToApplicationImages(json)` → `ImageBlock[]`
- [x] Verify `RouteDecision` forward-compat not broken (new optional field uses `= null`)

**Acceptance:**
- `dotnet build src/HydraForge.Application`
- `dotnet test` — existing LLM tests still pass
